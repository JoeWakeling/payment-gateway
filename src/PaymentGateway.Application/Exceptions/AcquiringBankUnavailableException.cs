namespace PaymentGateway.Application.Exceptions;

/// <summary>
/// Thrown when the acquiring bank reports it is unavailable (503 Service Unavailable).
/// </summary>
public class AcquiringBankUnavailableException(string message) : AcquiringBankException(message);
