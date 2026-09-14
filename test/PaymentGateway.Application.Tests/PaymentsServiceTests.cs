using Moq;

using PaymentGateway.Application.Interfaces;
using PaymentGateway.Domain;

namespace PaymentGateway.Application.Tests;

public class PaymentsServiceTests
{
    private readonly Mock<IPaymentsRepository> _paymentsRepository = new();
    private readonly Mock<TimeProvider> _timeProvider = new();
    private readonly PaymentsService _sut;

    public PaymentsServiceTests()
    {
        _sut = new PaymentsService(_paymentsRepository.Object, _timeProvider.Object);
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

    [Fact]
    public void Add_CardExpiresInFutureMonth_AddsPayment()
    {
        // Arrange
        _timeProvider.Setup(tp => tp.GetUtcNow()).Returns(new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero));
        var payment = CreatePayment(expiryMonth: 2, expiryYear: 2024);

        // Act
        _sut.Add(payment);

        // Assert
        _paymentsRepository.Verify(r => r.Add(payment), Times.Once);
    }

    [Fact]
    public void Add_CardExpiresInCurrentMonth_AddsPayment()
    {
        // Arrange
        _timeProvider.Setup(tp => tp.GetUtcNow()).Returns(new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.Zero));
        var payment = CreatePayment(expiryMonth: 6, expiryYear: 2024);

        // Act
        _sut.Add(payment);

        // Assert
        _paymentsRepository.Verify(r => r.Add(payment), Times.Once);
    }

    [Fact]
    public void Add_CardExpiresInFutureYear_AddsPayment()
    {
        // Arrange
        _timeProvider.Setup(tp => tp.GetUtcNow()).Returns(new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.Zero));
        var payment = CreatePayment(expiryMonth: 1, expiryYear: 2025);

        // Act
        _sut.Add(payment);

        // Assert
        _paymentsRepository.Verify(r => r.Add(payment), Times.Once);
    }

    [Fact]
    public void Add_CardExpiredInPreviousMonth_ThrowsArgumentException()
    {
        // Arrange
        _timeProvider.Setup(tp => tp.GetUtcNow()).Returns(new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero));
        var payment = CreatePayment(expiryMonth: 5, expiryYear: 2024);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _sut.Add(payment));
        Assert.Equal("Payment card has expired.", exception.Message);
        _paymentsRepository.Verify(r => r.Add(It.IsAny<Payment>()), Times.Never);
    }

    [Fact]
    public void Add_CardExpiredInPreviousYear_ThrowsArgumentException()
    {
        // Arrange
        _timeProvider.Setup(tp => tp.GetUtcNow()).Returns(new DateTimeOffset(2024, 3, 10, 0, 0, 0, TimeSpan.Zero));
        var payment = CreatePayment(expiryMonth: 12, expiryYear: 2023);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _sut.Add(payment));
        _paymentsRepository.Verify(r => r.Add(It.IsAny<Payment>()), Times.Never);
    }

    [Fact]
    public void Add_CardExpiredOnLastDayOfExpiryMonth_ThrowsArgumentException()
    {
        // Arrange — first day after expiry month
        _timeProvider.Setup(tp => tp.GetUtcNow()).Returns(new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero));
        var payment = CreatePayment(expiryMonth: 1, expiryYear: 2024);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _sut.Add(payment));
        _paymentsRepository.Verify(r => r.Add(It.IsAny<Payment>()), Times.Never);
    }

    [Fact]
    public void Add_LowercaseCurrency_NormalisesToUppercase()
    {
        // Arrange
        _timeProvider.Setup(tp => tp.GetUtcNow()).Returns(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var payment = CreatePayment(expiryMonth: 6, expiryYear: 2024);
        payment.Currency = "gbp";

        // Act
        _sut.Add(payment);

        // Assert
        Assert.Equal("GBP", payment.Currency);
        _paymentsRepository.Verify(r => r.Add(payment), Times.Once);
    }

    [Fact]
    public void Add_MixedCaseCurrency_NormalisesToUppercase()
    {
        // Arrange
        _timeProvider.Setup(tp => tp.GetUtcNow()).Returns(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var payment = CreatePayment(expiryMonth: 6, expiryYear: 2024);
        payment.Currency = "uSd";

        // Act
        _sut.Add(payment);

        // Assert
        Assert.Equal("USD", payment.Currency);
    }
}
