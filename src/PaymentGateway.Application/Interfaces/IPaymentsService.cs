using PaymentGateway.Domain;

namespace PaymentGateway.Application.Interfaces;

public interface IPaymentsService
{
    public Task AddAsync(Payment payment, CancellationToken cancellationToken);
    public Task<Payment?> GetAsync(Guid id, CancellationToken cancellationToken);
}
