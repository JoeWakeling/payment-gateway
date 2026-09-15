using Microsoft.Extensions.Logging;

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
            throw new ArgumentException("Payment card has expired.");
        }

        var currency = request.Currency.ToUpperInvariant();

        var bankResponse = await acquiringBankClient.ProcessPaymentAsync(
            new AcquiringBankPaymentRequest(
                request.CardNumber,
                request.ExpiryMonth,
                request.ExpiryYear,
                currency,
                request.Amount,
                request.Cvv),
            cancellationToken);

        var payment = new Payment
        {
            Id = request.Id,
            Status = bankResponse.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            CardNumberLastFour = int.Parse(request.CardNumber[^4..]),
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            Currency = currency,
            Amount = request.Amount
        };

        paymentsRepository.Add(payment);

        logger.LogInformation("Payment stored with status {Status}", payment.Status);

        return payment;
    }

    public Payment? GetPaymentById(Guid id)
    {
        return paymentsRepository.Get(id);
    }

    private bool IsCardExpired(int expiryMonth, int expiryYear)
    {
        var now = timeProvider.GetUtcNow();
        var expiryDate = new DateOnly(expiryYear, expiryMonth, 1).AddMonths(1);

        return expiryDate <= DateOnly.FromDateTime(now.DateTime);
    }
}
