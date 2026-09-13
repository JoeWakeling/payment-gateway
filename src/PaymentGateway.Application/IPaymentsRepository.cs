using PaymentGateway.Domain;

namespace PaymentGateway.Application;

public interface IPaymentsRepository
{
    public void Add(Payment payment);
    public Payment? Get(Guid id);
}
