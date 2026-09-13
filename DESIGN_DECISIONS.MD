## Project structure

Split into four projects plus test projects, following a simple layered architecture:

```
PaymentGateway.Domain          (Class Library)
PaymentGateway.Application     (Class Library)
PaymentGateway.Infrastructure  (Class Library)
PaymentGateway.Api             (ASP.NET Core Web API)

PaymentGateway.Application.Tests
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

Api is the composition root — it references Infrastructure only to wire up DI registrations (e.g. `IPaymentsRepository` → `PaymentsRepository`), never to use concrete types directly. Application defines the interfaces that Infrastructure implements; Application itself depends only on Domain and its own abstractions, never on concrete infrastructure. This keeps core business rules decoupled from how the bank is actually called or payments actually stored.

### Why not fewer projects
A single project with folders would have been an equally legitimate, simpler choice. Four projects was chosen to make dependency boundaries compiler-enforced (e.g. Domain genuinely can't reference Infrastructure) rather than relying on folder discipline