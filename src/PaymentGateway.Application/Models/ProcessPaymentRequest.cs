namespace PaymentGateway.Application.Models;

public record ProcessPaymentRequest(
    Guid Id,
    string CardNumber,
    int ExpiryMonth,
    int ExpiryYear,
    string Currency,
    int Amount,
    string Cvv);
