namespace PaymentGateway.Application.Models;

public record ProcessPaymentRequest(
    Guid Id,
    string CardNumber,
    int ExpiryMonth,
    int ExpiryYear,
    string Currency,
    long Amount,
    string Cvv);
