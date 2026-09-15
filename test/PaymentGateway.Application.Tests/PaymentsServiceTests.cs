using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using Moq;

using PaymentGateway.Application.Interfaces;
using PaymentGateway.Domain;

namespace PaymentGateway.Application.Tests;

public class PaymentsServiceTests
{
    private readonly Mock<IPaymentsRepository> _paymentsRepository = new();
    private readonly Mock<TimeProvider> _timeProvider = new();
    private readonly FakeLogger<PaymentsService> _logger = new();
    private readonly PaymentsService _sut;

    public PaymentsServiceTests()
    {
        _sut = new PaymentsService(_paymentsRepository.Object, _timeProvider.Object, _logger);
    }

    private static Payment CreatePayment(int expiryMonth, int expiryYear) => new()
    {
        Id = Guid.NewGuid(),
        Status = PaymentStatus.Authorized,
        CardNumberLastFour = 1234,
        ExpiryMonth = expiryMonth,
        ExpiryYear = expiryYear,
        Currency = "GBP",
        Amount = 100
    };

    private void VerifyLogged(LogLevel level, string message)
    {
        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(level, record.Level);
        Assert.Equal(message, record.Message);
    }

    [Fact]
    public async Task Add_CardExpiresInFutureMonth_AddsPayment()
    {
        // Arrange
        _timeProvider.Setup(tp => tp.GetUtcNow()).Returns(new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero));
        var payment = CreatePayment(expiryMonth: 2, expiryYear: 2024);

        // Act
        await _sut.AddAsync(payment, TestContext.Current.CancellationToken);

        // Assert
        _paymentsRepository.Verify(r => r.AddAsync(payment, TestContext.Current.CancellationToken), Times.Once);
        VerifyLogged(LogLevel.Information, "Payment stored with status Authorized");
        var scope = Assert.IsType<Dictionary<string, object>>(Assert.Single(_logger.LatestRecord.Scopes));
        Assert.Equal(payment.Id, scope["PaymentId"]);
    }

    [Fact]
    public async Task Add_CardExpiresInCurrentMonth_AddsPayment()
    {
        // Arrange
        _timeProvider.Setup(tp => tp.GetUtcNow()).Returns(new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.Zero));
        var payment = CreatePayment(expiryMonth: 6, expiryYear: 2024);

        // Act
        await _sut.AddAsync(payment, TestContext.Current.CancellationToken);

        // Assert
        _paymentsRepository.Verify(r => r.AddAsync(payment, TestContext.Current.CancellationToken), Times.Once);
        VerifyLogged(LogLevel.Information, "Payment stored with status Authorized");
    }

    [Fact]
    public async Task Add_CardExpiresInFutureYear_AddsPayment()
    {
        // Arrange
        _timeProvider.Setup(tp => tp.GetUtcNow()).Returns(new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.Zero));
        var payment = CreatePayment(expiryMonth: 1, expiryYear: 2025);

        // Act
        await _sut.AddAsync(payment, TestContext.Current.CancellationToken);

        // Assert
        _paymentsRepository.Verify(r => r.AddAsync(payment, TestContext.Current.CancellationToken), Times.Once);
        VerifyLogged(LogLevel.Information, "Payment stored with status Authorized");
    }

    [Fact]
    public async Task Add_CardExpiredInPreviousMonth_ThrowsArgumentException()
    {
        // Arrange
        _timeProvider.Setup(tp => tp.GetUtcNow()).Returns(new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero));
        var payment = CreatePayment(expiryMonth: 5, expiryYear: 2024);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _sut.AddAsync(payment, TestContext.Current.CancellationToken));
        Assert.Equal("Payment card has expired.", exception.Message);
        _paymentsRepository.Verify(r => r.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyLogged(LogLevel.Information, "Payment rejected: card expired");
    }

    [Fact]
    public async Task Add_CardExpiredInPreviousYear_ThrowsArgumentException()
    {
        // Arrange
        _timeProvider.Setup(tp => tp.GetUtcNow()).Returns(new DateTimeOffset(2024, 3, 10, 0, 0, 0, TimeSpan.Zero));
        var payment = CreatePayment(expiryMonth: 12, expiryYear: 2023);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.AddAsync(payment, TestContext.Current.CancellationToken));
        _paymentsRepository.Verify(r => r.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyLogged(LogLevel.Information, "Payment rejected: card expired");
    }

    [Fact]
    public async Task Add_CardExpiredOnLastDayOfExpiryMonth_ThrowsArgumentException()
    {
        // Arrange — first day after expiry month
        _timeProvider.Setup(tp => tp.GetUtcNow()).Returns(new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero));
        var payment = CreatePayment(expiryMonth: 1, expiryYear: 2024);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.AddAsync(payment, TestContext.Current.CancellationToken));
        _paymentsRepository.Verify(r => r.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyLogged(LogLevel.Information, "Payment rejected: card expired");
    }

    [Fact]
    public async Task Add_LowercaseCurrency_NormalisesToUppercase()
    {
        // Arrange
        _timeProvider.Setup(tp => tp.GetUtcNow()).Returns(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var payment = CreatePayment(expiryMonth: 6, expiryYear: 2024);
        payment.Currency = "gbp";

        // Act
        await _sut.AddAsync(payment, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("GBP", payment.Currency);
        _paymentsRepository.Verify(r => r.AddAsync(payment, TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task Add_MixedCaseCurrency_NormalisesToUppercase()
    {
        // Arrange
        _timeProvider.Setup(tp => tp.GetUtcNow()).Returns(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var payment = CreatePayment(expiryMonth: 6, expiryYear: 2024);
        payment.Currency = "uSd";

        // Act
        await _sut.AddAsync(payment, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("USD", payment.Currency);
    }
}