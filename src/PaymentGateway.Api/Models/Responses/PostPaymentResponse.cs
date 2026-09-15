using PaymentGateway.Domain;

namespace PaymentGateway.Api.Models.Responses;

/// <summary>The result of processing a payment.</summary>
public class PostPaymentResponse
{
    /// <summary>The payment identifier, used to retrieve the payment later.</summary>
    public Guid Id { get; set; }
    /// <summary>The payment outcome: Authorized or Declined.</summary>
    public PaymentStatus Status { get; set; }
    /// <summary>The last four digits of the card number.</summary>
    public string CardNumberLastFour { get; set; }
    /// <summary>The card expiry month (1-12).</summary>
    public int ExpiryMonth { get; set; }
    /// <summary>The card expiry year.</summary>
    public int ExpiryYear { get; set; }
    /// <summary>The ISO 4217 currency code, in uppercase (GBP, USD or EUR).</summary>
    public string Currency { get; set; }
    /// <summary>The payment amount in the minor currency unit (e.g. 1050 is £10.50 in GBP).</summary>
    public int Amount { get; set; }
}
