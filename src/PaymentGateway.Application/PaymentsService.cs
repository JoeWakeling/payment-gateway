using Microsoft.Extensions.Logging;

using PaymentGateway.Application.Interfaces;
using PaymentGateway.Domain;

namespace PaymentGateway.Application;

public class PaymentsService(
    IPaymentsRepository paymentsRepository,
    TimeProvider timeProvider,
    ILogger<PaymentsService> logger)
    : IPaymentsService
{
    public void Add(Payment payment)
    {
        using var _ = logger.BeginScope(new Dictionary<string, object> { ["PaymentId"] = payment.Id });

        if (IsCardExpired(payment))
        {
            logger.LogInformation("Payment rejected: card expired");
            throw new ArgumentException("Payment card has expired.");
        }

        payment.Currency = payment.Currency.ToUpperInvariant();

        paymentsRepository.Add(payment);

        logger.LogInformation("Payment stored with status {Status}", payment.Status);
    }

    public Payment? Get(Guid id)
    {
        return paymentsRepository.Get(id);
    }

    private bool IsCardExpired(Payment payment)
    {
        var now = timeProvider.GetUtcNow();
        var expiryDate = new DateOnly(payment.ExpiryYear, payment.ExpiryMonth, 1).AddMonths(1);

        return expiryDate <= DateOnly.FromDateTime(now.DateTime);
    }
}