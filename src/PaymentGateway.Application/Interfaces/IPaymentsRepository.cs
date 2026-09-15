using PaymentGateway.Domain;

namespace PaymentGateway.Application.Interfaces;

public interface IPaymentsRepository
{
    public Task AddAsync(Payment payment, CancellationToken cancellationToken);
    public Task<Payment?> GetAsync(Guid id, CancellationToken cancellationToken);
}
