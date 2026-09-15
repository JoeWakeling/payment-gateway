using Microsoft.Extensions.Logging;

using PaymentGateway.Application.Exceptions;
using PaymentGateway.Application.Interfaces;
using PaymentGateway.Application.Models;
using PaymentGateway.Domain;

namespace PaymentGateway.Application;

public class PaymentsService(
    IPaymentsRepository paymentsRepository,
    IAcquiringBankClient acquiringBankClient,
    TimeProvider timeProvider,
    ILogger<PaymentsService> logger)
    : IPaymentsService
{
    public async Task<Payment> ProcessPaymentAsync(ProcessPaymentRequest request, CancellationToken cancellationToken)
    {
        using var _ = logger.BeginScope(new Dictionary<string, object> { ["PaymentId"] = request.Id });

        if (IsCardExpired(request.ExpiryMonth, request.ExpiryYear))
        {
            logger.LogInformation("Payment rejected: card expired");
            throw new PaymentRejectedException("card has expired");
        }

        var currency = request.Currency.ToUpperInvariant();

        var authorized = await ProcessWithBankAsync(request, currency, cancellationToken);

        var payment = new Payment
        {
            Id = request.Id,
            Status = authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            CardNumberLastFour = request.CardNumber[^4..],
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            Currency = currency,
            Amount = request.Amount
        };

        await paymentsRepository.AddAsync(payment, cancellationToken);

        logger.LogInformation("Payment stored with status {Status}", payment.Status);

        return payment;
    }

    public Task<Payment?> GetPaymentByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return paymentsRepository.GetAsync(id, cancellationToken);
    }

    private async Task<bool> ProcessWithBankAsync(
        ProcessPaymentRequest request,
        string currency,
        CancellationToken cancellationToken)
    {
        try
        {
            var bankResponse = await acquiringBankClient.ProcessPaymentAsync(
                new AcquiringBankPaymentRequest(
                    request.CardNumber,
                    request.ExpiryMonth,
                    request.ExpiryYear,
                    currency,
                    request.Amount,
                    request.Cvv),
                cancellationToken);

            return bankResponse.Authorized;
        }
        catch (AcquiringBankException)
        {
            // No usable authorization decision from the bank, so the payment cannot be treated as authorized.
            logger.LogWarning("Acquiring bank did not return an authorization decision; declining payment");
            return false;
        }
    }

    private bool IsCardExpired(int expiryMonth, int expiryYear)
    {
        var now = timeProvider.GetUtcNow();

        // Compared as year/month rather than building a date, which would throw for December 9999
        return expiryYear < now.Year || (expiryYear == now.Year && expiryMonth < now.Month);
    }
}
