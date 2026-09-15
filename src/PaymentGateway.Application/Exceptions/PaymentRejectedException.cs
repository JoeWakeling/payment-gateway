namespace PaymentGateway.Application.Exceptions;

/// <summary>
/// Thrown when a payment breaks a business rule and is rejected without calling the acquiring bank.
/// </summary>
public class PaymentRejectedException(string reason) : Exception($"Rejected: {reason}");
