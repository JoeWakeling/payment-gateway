namespace PaymentGateway.Application.Exceptions;

/// <summary>
/// Thrown when the acquiring bank does not return a usable authorization decision.
/// </summary>
public class AcquiringBankException : Exception
{
    public AcquiringBankException(string message) : base(message) { }

    public AcquiringBankException(string message, Exception innerException) : base(message, innerException) { }
}