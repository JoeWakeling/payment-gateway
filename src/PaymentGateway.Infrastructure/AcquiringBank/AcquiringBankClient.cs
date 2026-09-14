using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using PaymentGateway.Application.Exceptions;
using PaymentGateway.Application.Interfaces;
using PaymentGateway.Application.Models;

namespace PaymentGateway.Infrastructure.AcquiringBank;

public class AcquiringBankClient(HttpClient httpClient) : IAcquiringBankClient
{
    private const string PaymentsPath = "payments";

    public async Task<AcquiringBankPaymentResponse> ProcessPaymentAsync(
        AcquiringBankPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var requestDto = new AcquiringBankPaymentRequestDto(
            request.CardNumber,
            $"{request.ExpiryMonth:D2}/{request.ExpiryYear}",
            request.Currency,
            request.Amount,
            request.Cvv);

        try
        {
            using var response = await httpClient.PostAsJsonAsync(PaymentsPath, requestDto, cancellationToken);

            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                throw new AcquiringBankUnavailableException("Acquiring bank is unavailable.");
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new AcquiringBankException(
                    $"Acquiring bank returned unexpected status code {(int)response.StatusCode} ({response.StatusCode}).");
            }

            var responseDto =
                await response.Content.ReadFromJsonAsync<AcquiringBankPaymentResponseDto>(cancellationToken);

            if (responseDto is null)
            {
                throw new AcquiringBankException("Acquiring bank returned an empty response body.");
            }

            return new AcquiringBankPaymentResponse(responseDto.Authorized, responseDto.AuthorizationCode);
        }
        catch (HttpRequestException ex)
        {
            throw new AcquiringBankException("Failed to communicate with the acquiring bank.", ex);
        }
        catch (JsonException ex)
        {
            throw new AcquiringBankException("Acquiring bank returned an invalid response body.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AcquiringBankException("Request to the acquiring bank timed out.", ex);
        }
    }
}