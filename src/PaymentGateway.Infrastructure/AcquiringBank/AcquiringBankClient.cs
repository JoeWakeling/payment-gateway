using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using PaymentGateway.Application.Exceptions;
using PaymentGateway.Application.Interfaces;
using PaymentGateway.Application.Models;

namespace PaymentGateway.Infrastructure.AcquiringBank;

public class AcquiringBankClient(HttpClient httpClient, ILogger<AcquiringBankClient> logger) : IAcquiringBankClient
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

        logger.LogDebug("Sending payment processing request to acquiring bank for {Amount} {Currency}",
            request.Amount, request.Currency);

        var startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            using var response = await httpClient.PostAsJsonAsync(PaymentsPath, requestDto, cancellationToken);

            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                logger.LogWarning("Acquiring bank is unavailable (503) after {ElapsedMs}ms", ElapsedMs(startTimestamp));
                throw new AcquiringBankUnavailableException("Acquiring bank is unavailable.");
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                logger.LogWarning("Acquiring bank returned unexpected status code {StatusCode} after {ElapsedMs}ms",
                    (int)response.StatusCode, ElapsedMs(startTimestamp));
                throw new AcquiringBankException(
                    $"Acquiring bank returned unexpected status code {(int)response.StatusCode} ({response.StatusCode}).");
            }

            var responseDto =
                await response.Content.ReadFromJsonAsync<AcquiringBankPaymentResponseDto>(cancellationToken);

            if (responseDto is null)
            {
                logger.LogWarning("Acquiring bank returned an empty response body after {ElapsedMs}ms",
                    ElapsedMs(startTimestamp));
                throw new AcquiringBankException("Acquiring bank returned an empty response body.");
            }

            logger.LogInformation("Acquiring bank responded Authorized={Authorized} in {ElapsedMs}ms",
                responseDto.Authorized, ElapsedMs(startTimestamp));

            return new AcquiringBankPaymentResponse(responseDto.Authorized, responseDto.AuthorizationCode);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Failed to communicate with the acquiring bank after {ElapsedMs}ms",
                ElapsedMs(startTimestamp));
            throw new AcquiringBankException("Failed to communicate with the acquiring bank.", ex);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Acquiring bank returned an invalid response body after {ElapsedMs}ms",
                ElapsedMs(startTimestamp));
            throw new AcquiringBankException("Acquiring bank returned an invalid response body.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Request to the acquiring bank timed out after {ElapsedMs}ms",
                ElapsedMs(startTimestamp));
            throw new AcquiringBankException("Request to the acquiring bank timed out.", ex);
        }
    }

    private static long ElapsedMs(long startTimestamp) =>
        (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
}