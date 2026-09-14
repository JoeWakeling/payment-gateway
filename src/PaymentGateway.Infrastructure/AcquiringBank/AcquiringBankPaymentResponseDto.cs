using System.Text.Json.Serialization;

namespace PaymentGateway.Infrastructure.AcquiringBank;

internal record AcquiringBankPaymentResponseDto(
    [property: JsonPropertyName("authorized")] bool Authorized,
    [property: JsonPropertyName("authorization_code")] string? AuthorizationCode);
