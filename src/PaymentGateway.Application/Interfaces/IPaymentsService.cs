using PaymentGateway.Application.Models;
using PaymentGateway.Domain;

namespace PaymentGateway.Application.Interfaces;

public interface IPaymentsService
{
    public Task<Payment> ProcessPaymentAsync(ProcessPaymentRequest request, CancellationToken cancellationToken);
    public Payment? GetPaymentById(Guid id);
}
