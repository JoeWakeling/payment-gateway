using PaymentGateway.Application.Models;

namespace PaymentGateway.Application.Interfaces;

public interface IAcquiringBankClient
{
    public Task<AcquiringBankPaymentResponse> ProcessPaymentAsync(
        AcquiringBankPaymentRequest request,
        CancellationToken cancellationToken);
}