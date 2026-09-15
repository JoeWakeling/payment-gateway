using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using Moq;

using PaymentGateway.Application.Exceptions;
using PaymentGateway.Application.Interfaces;
using PaymentGateway.Application.Models;
using PaymentGateway.Domain;

namespace PaymentGateway.Application.Tests;

public class PaymentsServiceTests
{
    private readonly Mock<IPaymentsRepository> _paymentsRepository = new();
    private readonly Mock<IAcquiringBankClient> _acquiringBankClient = new();
    private readonly Mock<TimeProvider> _timeProvider = new();
    private readonly FakeLogger<PaymentsService> _logger = new();
    private readonly PaymentsService _sut;

    public PaymentsServiceTests()
    {
        _sut = new PaymentsService(
            _paymentsRepository.Object,
            _acquiringBankClient.Object,
            _timeProvider.Object,
            _logger);

        SetupBankResponse(authorized: true);
    }

    private static ProcessPaymentRequest CreateRequest(
        int expiryMonth = 6,
        int expiryYear = 2024,
        string currency = "GBP") => new(
        Id: Guid.NewGuid(),
        CardNumber: "2222405343248877",
        ExpiryMonth: expiryMonth,
        ExpiryYear: expiryYear,
        Currency: currency,
        Amount: 100,
        Cvv: "123");

    private void SetupNow(int year, int month, int day) =>
        _timeProvider.Setup(tp => tp.GetUtcNow()).Returns(new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero));

    private void SetupBankResponse(bool authorized) =>
        _acquiringBankClient
            .Setup(c => c.ProcessPaymentAsync(It.IsAny<AcquiringBankPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AcquiringBankPaymentResponse(authorized, authorized ? "auth-code" : null));

    private void VerifyLogged(LogLevel level, string message)
    {
        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(level, record.Level);
        Assert.Equal(message, record.Message);
    }

    private void VerifyNotProcessed()
    {
        _acquiringBankClient.Verify(
            c => c.ProcessPaymentAsync(It.IsAny<AcquiringBankPaymentRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _paymentsRepository.Verify(r => r.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessPaymentAsync_BankAuthorizes_StoresAndReturnsAuthorizedPayment()
    {
        // Arrange
        SetupNow(2024, 1, 15);
        var request = CreateRequest(expiryMonth: 2, expiryYear: 2024);

        // Act
        var payment = await _sut.ProcessPaymentAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(request.Id, payment.Id);
        Assert.Equal(PaymentStatus.Authorized, payment.Status);
        Assert.Equal(8877, payment.CardNumberLastFour);
        Assert.Equal(request.ExpiryMonth, payment.ExpiryMonth);
        Assert.Equal(request.ExpiryYear, payment.ExpiryYear);
        Assert.Equal(request.Currency, payment.Currency);
        Assert.Equal(request.Amount, payment.Amount);
        _paymentsRepository.Verify(r => r.AddAsync(payment, TestContext.Current.CancellationToken), Times.Once);
        VerifyLogged(LogLevel.Information, "Payment stored with status Authorized");
        var scope = Assert.IsType<Dictionary<string, object>>(Assert.Single(_logger.LatestRecord.Scopes));
        Assert.Equal(request.Id, scope["PaymentId"]);
    }

    [Fact]
    public async Task ProcessPaymentAsync_BankDeclines_StoresAndReturnsDeclinedPayment()
    {
        // Arrange
        SetupNow(2024, 1, 15);
        SetupBankResponse(authorized: false);
        var request = CreateRequest(expiryMonth: 2, expiryYear: 2024);

        // Act
        var payment = await _sut.ProcessPaymentAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PaymentStatus.Declined, payment.Status);
        _paymentsRepository.Verify(r => r.AddAsync(payment, TestContext.Current.CancellationToken), Times.Once);
        VerifyLogged(LogLevel.Information, "Payment stored with status Declined");
    }

    [Fact]
    public async Task ProcessPaymentAsync_SendsRequestDetailsToBank()
    {
        // Arrange
        SetupNow(2024, 1, 15);
        var request = CreateRequest(expiryMonth: 2, expiryYear: 2024);

        // Act
        await _sut.ProcessPaymentAsync(request, TestContext.Current.CancellationToken);

        // Assert
        _acquiringBankClient.Verify(c => c.ProcessPaymentAsync(
                new AcquiringBankPaymentRequest(
                    request.CardNumber,
                    request.ExpiryMonth,
                    request.ExpiryYear,
                    request.Currency,
                    request.Amount,
                    request.Cvv),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProcessPaymentAsync_BankFails_StoresAndReturnsDeclinedPayment(bool bankUnavailable)
    {
        // Arrange
        SetupNow(2024, 1, 15);
        var exception = bankUnavailable
            ? new AcquiringBankUnavailableException("Acquiring bank is unavailable.")
            : new AcquiringBankException("Failed to communicate with the acquiring bank.", new HttpRequestException());
        _acquiringBankClient
            .Setup(c => c.ProcessPaymentAsync(It.IsAny<AcquiringBankPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);
        var request = CreateRequest(expiryMonth: 2, expiryYear: 2024);

        // Act
        var payment = await _sut.ProcessPaymentAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(request.Id, payment.Id);
        Assert.Equal(PaymentStatus.Declined, payment.Status);
        Assert.Equal(8877, payment.CardNumberLastFour);
        _paymentsRepository.Verify(r => r.AddAsync(payment, TestContext.Current.CancellationToken), Times.Once);
        var records = _logger.Collector.GetSnapshot();
        Assert.Collection(records,
            r =>
            {
                Assert.Equal(LogLevel.Warning, r.Level);
                Assert.Equal("Acquiring bank did not return an authorization decision; declining payment", r.Message);
            },
            r =>
            {
                Assert.Equal(LogLevel.Information, r.Level);
                Assert.Equal("Payment stored with status Declined", r.Message);
            });
    }

    [Fact]
    public async Task ProcessPaymentAsync_CancelledDuringBankCall_PropagatesAndDoesNotStorePayment()
    {
        // Arrange
        SetupNow(2024, 1, 15);
        _acquiringBankClient
            .Setup(c => c.ProcessPaymentAsync(It.IsAny<AcquiringBankPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        var request = CreateRequest(expiryMonth: 2, expiryYear: 2024);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.ProcessPaymentAsync(request, TestContext.Current.CancellationToken));
        _paymentsRepository.Verify(r => r.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessPaymentAsync_CardExpiresInCurrentMonth_ProcessesPayment()
    {
        // Arrange
        SetupNow(2024, 6, 15);
        var request = CreateRequest(expiryMonth: 6, expiryYear: 2024);

        // Act
        var payment = await _sut.ProcessPaymentAsync(request, TestContext.Current.CancellationToken);

        // Assert
        _paymentsRepository.Verify(r => r.AddAsync(payment, TestContext.Current.CancellationToken), Times.Once);
        VerifyLogged(LogLevel.Information, "Payment stored with status Authorized");
    }

    [Fact]
    public async Task ProcessPaymentAsync_CardExpiresInFutureYear_ProcessesPayment()
    {
        // Arrange
        SetupNow(2024, 6, 15);
        var request = CreateRequest(expiryMonth: 1, expiryYear: 2025);

        // Act
        var payment = await _sut.ProcessPaymentAsync(request, TestContext.Current.CancellationToken);

        // Assert
        _paymentsRepository.Verify(r => r.AddAsync(payment, TestContext.Current.CancellationToken), Times.Once);
        VerifyLogged(LogLevel.Information, "Payment stored with status Authorized");
    }

    [Fact]
    public async Task ProcessPaymentAsync_CardExpiredInPreviousMonth_ThrowsPaymentRejectedException()
    {
        // Arrange
        SetupNow(2024, 6, 1);
        var request = CreateRequest(expiryMonth: 5, expiryYear: 2024);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<PaymentRejectedException>(
            () => _sut.ProcessPaymentAsync(request, TestContext.Current.CancellationToken));
        Assert.Equal("Rejected: card has expired", exception.Message);
        VerifyNotProcessed();
        VerifyLogged(LogLevel.Information, "Payment rejected: card expired");
    }

    [Fact]
    public async Task ProcessPaymentAsync_CardExpiredInPreviousYear_ThrowsPaymentRejectedException()
    {
        // Arrange
        SetupNow(2024, 3, 10);
        var request = CreateRequest(expiryMonth: 12, expiryYear: 2023);

        // Act & Assert
        await Assert.ThrowsAsync<PaymentRejectedException>(
            () => _sut.ProcessPaymentAsync(request, TestContext.Current.CancellationToken));
        VerifyNotProcessed();
        VerifyLogged(LogLevel.Information, "Payment rejected: card expired");
    }

    [Fact]
    public async Task ProcessPaymentAsync_CardExpiredOnLastDayOfExpiryMonth_ThrowsPaymentRejectedException()
    {
        // Arrange — first day after expiry month
        SetupNow(2024, 2, 1);
        var request = CreateRequest(expiryMonth: 1, expiryYear: 2024);

        // Act & Assert
        await Assert.ThrowsAsync<PaymentRejectedException>(
            () => _sut.ProcessPaymentAsync(request, TestContext.Current.CancellationToken));
        VerifyNotProcessed();
        VerifyLogged(LogLevel.Information, "Payment rejected: card expired");
    }

    [Theory]
    [InlineData("gbp", "GBP")]
    [InlineData("uSd", "USD")]
    public async Task ProcessPaymentAsync_NonUppercaseCurrency_NormalisesToUppercase(string currency, string expected)
    {
        // Arrange
        SetupNow(2024, 1, 1);
        var request = CreateRequest(currency: currency);

        // Act
        var payment = await _sut.ProcessPaymentAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, payment.Currency);
        _acquiringBankClient.Verify(c => c.ProcessPaymentAsync(
                It.Is<AcquiringBankPaymentRequest>(r => r.Currency == expected),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
