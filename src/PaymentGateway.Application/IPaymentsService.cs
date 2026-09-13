using PaymentGateway.Domain;

namespace PaymentGateway.Application;

public interface IPaymentsService
{
    public void Add(Payment payment);
    public Payment? Get(Guid id);
}
