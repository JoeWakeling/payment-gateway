namespace PaymentGateway.Application.Models;

public record AcquiringBankPaymentResponse(bool Authorized, string? AuthorizationCode);
