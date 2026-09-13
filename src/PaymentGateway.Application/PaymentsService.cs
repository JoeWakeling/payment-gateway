using PaymentGateway.Domain;

namespace PaymentGateway.Application;

public class PaymentsService : IPaymentsService
{
    private readonly IPaymentsRepository _paymentsRepository;

    public PaymentsService(IPaymentsRepository paymentsRepository)
    {
        _paymentsRepository = paymentsRepository;
    }

    public void Add(Payment payment)
    {
        _paymentsRepository.Add(payment);
    }

    public Payment? Get(Guid id)
    {
        return _paymentsRepository.Get(id);
    }
}
