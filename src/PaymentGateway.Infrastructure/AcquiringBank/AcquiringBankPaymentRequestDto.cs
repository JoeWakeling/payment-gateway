using System.Text.Json.Serialization;

namespace PaymentGateway.Infrastructure.AcquiringBank;

internal record AcquiringBankPaymentRequestDto(
    [property: JsonPropertyName("card_number")] string CardNumber,
    [property: JsonPropertyName("expiry_date")] string ExpiryDate,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("amount")] int Amount,
    [property: JsonPropertyName("cvv")] string Cvv);
