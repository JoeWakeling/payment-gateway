namespace PaymentGateway.Api.Models.Requests;

/// <summary>The details of a card payment to process.</summary>
public class PostPaymentRequest
{
    /// <summary>The card number, 14-19 numeric characters.</summary>
    public string CardNumber { get; set; } = String.Empty;
    /// <summary>The card expiry month (1-12).</summary>
    public int ExpiryMonth { get; set; }
    /// <summary>The card expiry year. Together with the month, must be in the future.</summary>
    public int ExpiryYear { get; set; }
    /// <summary>The ISO 4217 currency code: GBP, USD or EUR (case-insensitive).</summary>
    public string Currency { get; set; } = String.Empty;
    /// <summary>The payment amount in the minor currency unit (e.g. 1050 is £10.50 in GBP). Must be greater than 0.</summary>
    public int Amount { get; set; }
    /// <summary>The card verification value, 3-4 numeric characters.</summary>
    public string Cvv { get; set; } = String.Empty;
}