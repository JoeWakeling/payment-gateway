using PaymentGateway.Domain;

namespace PaymentGateway.Api.Models.Responses;

/// <summary>The result of processing a payment.</summary>
public class PostPaymentResponse
{
    /// <summary>The payment identifier, used to retrieve the payment later.</summary>
    /// <example>0f8fad5b-d9cb-469f-a165-70867728950e</example>
    public Guid Id { get; set; }
    /// <summary>The payment outcome: Authorized or Declined.</summary>
    /// <example>Authorized</example>
    public PaymentStatus Status { get; set; }
    /// <summary>The last four digits of the card number.</summary>
    /// <example>8877</example>
    public string CardNumberLastFour { get; set; }
    /// <summary>The card expiry month (1-12).</summary>
    /// <example>4</example>
    public int ExpiryMonth { get; set; }
    /// <summary>The card expiry year.</summary>
    /// <example>2030</example>
    public int ExpiryYear { get; set; }
    /// <summary>The ISO 4217 currency code, in uppercase (GBP, USD or EUR).</summary>
    /// <example>GBP</example>
    public string Currency { get; set; }
    /// <summary>The payment amount in the minor currency unit (e.g. 1050 is £10.50 in GBP).</summary>
    /// <example>1050</example>
    public int Amount { get; set; }
}
