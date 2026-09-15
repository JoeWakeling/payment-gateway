## Project structure

Split into four projects plus test projects, following a simple layered architecture:

```
PaymentGateway.Domain          (Class Library)
PaymentGateway.Application     (Class Library)
PaymentGateway.Infrastructure  (Class Library)
PaymentGateway.Api             (ASP.NET Core Web API)

PaymentGateway.Api.Tests
PaymentGateway.Application.Tests
PaymentGateway.Infrastructure.Tests
PaymentGateway.IntegrationTests
```

Kept deliberately shallow — one clear responsibility per project — to keep validation, orchestration, external I/O, and HTTP concerns independently testable.

### Domain
Core entities/enums only (`Payment`, `PaymentStatus`). No dependencies on any other project.
### Application
Business logic rules, orchestration, and the interfaces (`IAcquiringBankClient`, `IPaymentsRepository`) that Infrastructure implements. Depends only on Domain and its own abstractions, never on concrete infrastructure — so orchestration logic can be unit tested with mocks, no real HTTP calls or data store needed.

### Infrastructure
Concrete implementations of Application's interfaces: the bank simulator HTTP client and the in-memory repository. Isolating these behind interfaces means they're easy to swap — mocked in tests, or replaced with other concrete implementations with DI. If this was in production, a real database and bank integration would replace these.

### Api
The only executable project — controllers, DTOs, DI wiring. Composes the other three but holds no business logic itself; controllers stay thin and are easy to test.

### Dependency direction

```
Api            → Application, Infrastructure, Domain
Infrastructure → Application, Domain
Application    → Domain
Domain         → (nothing)
```

Api is the composition root — it references Infrastructure only to wire up DI registrations (e.g. `IPaymentsRepository` → `InMemoryPaymentsRepository`), never to use concrete types directly. Application defines the interfaces that Infrastructure implements; Application itself depends only on Domain and its own abstractions, never on concrete infrastructure. This keeps core business rules decoupled from how the bank is actually called or payments actually stored.

### Why not fewer projects
A single project with folders would have been an equally legitimate, simpler choice. Four projects was chosen to make dependency boundaries compiler-enforced (e.g. Domain genuinely can't reference Infrastructure) rather than relying on folder discipline

### Mapping between layers
Mapping between models is done inline in the methods that need it: the controller maps request/response DTOs, `PaymentsService` maps to the bank request and `Payment`, and `AcquiringBankClient` maps to/from the simulator's DTOs. There are no dedicated mapper classes or mapping libraries (e.g. AutoMapper). Each mapping is only used in one place and is a simple property-by-property copy, so a separate abstraction would add indirection without removing any duplication. In a bigger project, with more endpoints reusing the same models or more complex transformations, extracting mappers (or extension methods) would become worthwhile.

## Validation

Two-step validation for the payment processing request, split across layers:

1. **Request shape** — FluentValidation `AbstractValidator`s against the request DTO (card number/CVV length + numeric, expiry month and year range, currency length, amount etc.). Pure structural checks, no external state/dependencies.
2. **Business rules** — Application layer (expiry month+year is in the future). Needs external state (e.g. clock via TimeProvider), so it doesn't belong in a shape validator.

Currency is validated case-insensitively against 3 currencies (GBP, USD, EUR) at the shape layer, then normalised to uppercase in Application — so `"gbp"` and `"GBP"`, for example, are both valid.

Request shape failures return `400 Bad Request` with the FluentValidation errors as a validation problem response.

Business rule failures throw a `PaymentRejectedException` (rather than a generic `ArgumentException`), which the controller maps to `422 Unprocessable Entity` with a `ProblemDetails` body (e.g. `"detail": "Rejected: card has expired"`). The request was well-formed, so `422` keeps it distinct from shape failures, and the bank is never called.

## Acquiring bank client

`IAcquiringBankClient` (Application) is implemented by `AcquiringBankClient` (Infrastructure), a typed `HttpClient` registered in `Program.cs` with the base URL read from `AcquiringBank:BaseUrl` config. The client maps to/from the simulator's snake_case contract using internal DTOs, so the Application models stay free of transport concerns.

### Failure mapping
A `200 OK` authorization decision (authorized or declined) is returned as an `AcquiringBankPaymentResponse`. A `503 Service Unavailable` is an expected outcome per the simulator spec, so it is thrown as a distinct `AcquiringBankUnavailableException`, letting orchestration map "bank unavailable" separately from authorized/declined. Anything else — other status codes, network errors, timeouts, malformed/empty bodies — is thrown as the base `AcquiringBankException`, so callers never need to know about HTTP. A `400` falls into this bucket deliberately: the simulator only returns it for incomplete requests, which our own validation should prevent, so it would indicate a gateway bug rather than a business outcome. Cancellation requested by the caller is not wrapped and propagates as `OperationCanceledException`.

`PaymentsService` catches any `AcquiringBankException` (including the unavailable case) and treats it as `Declined`: without an authorization decision from the bank the payment can't be considered authorized, so it's stored and returned as declined (`200 OK`) rather than surfacing as a `500`. The client still logs the underlying failure at Warning, so bank outages stay visible.

This is a deliberate simplification, and the two failure shapes it collapses are not equivalent:

- **No payment was attempted** — a `503`, a refused connection, a bad status returned before the request was processed. The bank made no decision and nothing was charged, so `Declined` is an honest answer and a retry is safe.
- **The outcome is unknown** — a timeout, or a connection dropped mid-request. The request may have reached the bank and been authorized. `Declined` then asserts an outcome the gateway does not actually have, and a merchant reconciling against the bank's own records would find a charge with no matching authorized payment.

The requirements define a closed status set (`Authorized`, `Declined`), so there is no value available to express "unknown" without extending the merchant-facing contract. Declining keeps the response within spec and keeps the failure visible in logs, which is the right trade for this exercise. In production the timeout case would need its own path: a `Pending` status reconciled asynchronously against the bank — which is precisely what the idempotency keys discussed below would make safe — or a `502 Bad Gateway` with nothing persisted, so the merchant knows to query rather than assume.

There is a smaller cost too. A merchant currently cannot tell "the card was declined, don't retry" from "we couldn't reach the bank, a retry may work", because both are `200 OK` with `Declined`. A machine-readable decline reason on the response would fix that without changing the status set.

### Retries
No retry logic was implemented for bank communication failures. In production, transient failures (network errors, 503s) would typically warrant a retry policy with backoff (e.g. via Polly), but the bank simulator's 503 response is deterministic based on card number rather than transient, so retries wouldn't change the outcome in this test harness. Idempotency would also need considering before adding retries to a payment flow, to avoid double-charging on a retried request that actually succeeded.

### Idempotency
Not implemented, as the bank simulator has no support for idempotency keys. In production this would be important — perhaps an idempotency key passed through to the bank so that retried requests can't result in duplicate charges.

## Persistence

`InMemoryPaymentsRepository` stands in for real persistence. It sits behind `IPaymentsRepository`, so a database-backed version could be swapped in through DI without touching Application.

### Thread safety
The repository is registered as a singleton, so ASP.NET Core requests hit it concurrently. The provided `List<T>`-backed version could lose writes or throw under concurrent adds, so it was replaced with a `ConcurrentDictionary<Guid, Payment>`. Keying by payment id also makes `GetAsync` an O(1) lookup, instead of the O(n) scan the list needed.

### Async interface
`AddAsync`/`GetAsync` return `Task` even though the in-memory implementation completes synchronously. A real database call would be I/O-bound and async, so the interface matches that now, and a database-backed implementation won't force signature changes through the service and controller. It also keeps the pipeline consistently async alongside the acquiring bank client.

## Unhandled exceptions

Every deliberate error response is RFC 7807 problem details (`400` from validation, `422` from a rejected payment, `404` from an unknown payment), so unexpected failures use the same shape. `AddProblemDetails()` plus `UseExceptionHandler()` turn anything that escapes the pipeline into a `500` with an `application/problem+json` body, instead of the bare `500` with no body that ASP.NET Core returns otherwise. The body carries only `type`, `title` and `status` — no message, type name or stack trace, checked by a test — so a gateway bug can't leak internals to a caller.

No custom handler or exception-to-status-code mapping was added: the failures that have a meaningful HTTP status are already handled where they belong (`AcquiringBankException` → `Declined`/`200` in `PaymentsService`, `PaymentRejectedException` → `422` in the controller), so anything reaching this middleware is by definition unexpected and a `500` is the correct answer.

The middleware is registered first, deliberately outside `UseSerilogRequestLogging`. The exception therefore passes through the request logging middleware before being handled, so the request completion event is still written — at Error, with the exception and status code `500` attached — rather than being logged separately as a plain Information-level `500`.

## Logging

Serilog is used because the built-in `Microsoft.Extensions.Logging` has no file provider. Only the Api project depends on Serilog; Application and Infrastructure log through `ILogger<T>`.

What's logged:
- **Every request:** method, path, status code and duration, plus unhandled exceptions.
- **Acquiring bank calls:** the authorized/declined decision with latency, and every failure (503, bad status, bad body, network error, timeout) at Warning.
- **Validation failures:** each field name and error message, never the attempted values.
- **Payments:** stored or rejected, with the `PaymentId`; lookups for unknown payment ids.
- **Never logged:** card number and CVV, checked by a test.
