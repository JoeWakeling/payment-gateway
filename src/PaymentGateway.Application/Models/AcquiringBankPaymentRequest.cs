namespace PaymentGateway.Application.Models;

public record AcquiringBankPaymentRequest(
    string CardNumber,
    int ExpiryMonth,
    int ExpiryYear,
    string Currency,
    long Amount,
    string Cvv);
