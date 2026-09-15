using System.Collections.Concurrent;

using PaymentGateway.Application.Interfaces;
using PaymentGateway.Domain;

namespace PaymentGateway.Infrastructure;

public class InMemoryPaymentsRepository : IPaymentsRepository
{
    private readonly ConcurrentDictionary<Guid, Payment> _payments = new();

    public Task AddAsync(Payment payment, CancellationToken cancellationToken)
    {
        _payments[payment.Id] = payment;
        return Task.CompletedTask;
    }

    public Task<Payment?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        _payments.TryGetValue(id, out var payment);
        return Task.FromResult(payment);
    }
}