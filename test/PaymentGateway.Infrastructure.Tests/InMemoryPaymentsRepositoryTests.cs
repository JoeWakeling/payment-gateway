using PaymentGateway.Domain;

namespace PaymentGateway.Infrastructure.Tests;

public class InMemoryPaymentsRepositoryTests
{
    private readonly InMemoryPaymentsRepository _sut = new();

    private static Payment CreatePayment() => new()
    {
        Id = Guid.NewGuid(),
        Status = PaymentStatus.Authorized,
        CardNumberLastFour = 1234,
        ExpiryMonth = 12,
        ExpiryYear = 2030,
        Currency = "GBP",
        Amount = 100
    };

    [Fact]
    public async Task GetAsync_ExistingId_ReturnsPayment()
    {
        // Arrange
        var payment = CreatePayment();
        await _sut.AddAsync(payment, TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.GetAsync(payment.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(payment, result);
    }

    [Fact]
    public async Task GetAsync_MissingId_ReturnsNull()
    {
        // Arrange
        await _sut.AddAsync(CreatePayment(), TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.GetAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AddAsync_MultiplePayments_EachRetrievableById()
    {
        // Arrange
        var first = CreatePayment();
        var second = CreatePayment();

        // Act
        await _sut.AddAsync(first, TestContext.Current.CancellationToken);
        await _sut.AddAsync(second, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(first, await _sut.GetAsync(first.Id, TestContext.Current.CancellationToken));
        Assert.Same(second, await _sut.GetAsync(second.Id, TestContext.Current.CancellationToken));
    }
}