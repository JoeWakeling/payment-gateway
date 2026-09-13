using PaymentGateway.Domain;

namespace PaymentGateway.Application;

public class PaymentsService : IPaymentsService
{
    private readonly IPaymentsRepository _paymentsRepository;
    private readonly TimeProvider _timeProvider;

    public PaymentsService(IPaymentsRepository paymentsRepository, TimeProvider timeProvider)
    {
        _paymentsRepository = paymentsRepository;
        _timeProvider = timeProvider;
    }

    public void Add(Payment payment)
    {
        if (IsCardExpired(payment))
        {
            throw new ArgumentException("Payment card has expired.");
        }
        
        payment.Currency = payment.Currency.ToUpperInvariant();

        _paymentsRepository.Add(payment);
    }

    public Payment? Get(Guid id)
    {
        return _paymentsRepository.Get(id);
    }

    private bool IsCardExpired(Payment payment)
    {
        var now = _timeProvider.GetUtcNow();
        var expiryDate = new DateOnly(payment.ExpiryYear, payment.ExpiryMonth, 1).AddMonths(1);

        return expiryDate <= DateOnly.FromDateTime(now.DateTime);
    }
}
