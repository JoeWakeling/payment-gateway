using FluentValidation;
using FluentValidation.Results;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using Moq;

using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Application.Exceptions;
using PaymentGateway.Application.Interfaces;
using PaymentGateway.Application.Models;
using PaymentGateway.Domain;

namespace PaymentGateway.Api.Tests;

public class PaymentsControllerTests
{
    private readonly Mock<IPaymentsService> _paymentsService = new();
    private readonly Mock<IValidator<PostPaymentRequest>> _validator = new();
    private readonly FakeLogger<PaymentsController> _logger = new();
    private readonly PaymentsController _sut;

    public PaymentsControllerTests()
    {
        _sut = new PaymentsController(_paymentsService.Object, _validator.Object, _logger);

        SetupValidationResult(new ValidationResult());
        _paymentsService
            .Setup(s => s.ProcessPaymentAsync(It.IsAny<ProcessPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessPaymentRequest r, CancellationToken _) => CreatePayment(r.Id));
    }

    private static PostPaymentRequest CreateRequest() => new()
    {
        CardNumber = "2222405343248877",
        ExpiryMonth = 4,
        ExpiryYear = 2030,
        Currency = "gbp",
        Amount = 1050,
        Cvv = "123"
    };
    
    private static Payment CreatePayment(Guid id) => new()
    {
        Id = id,
        Status = PaymentStatus.Declined,
        CardNumberLastFour = "8877",
        ExpiryMonth = 11,
        ExpiryYear = 2031,
        Currency = "GBP",
        Amount = 2500
    };

    private void SetupValidationResult(ValidationResult result) =>
        _validator
            .Setup(v => v.ValidateAsync(It.IsAny<PostPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private static string? GetStateValue(FakeLogRecord record, string key) =>
        Assert.Single(record.StructuredState!, kvp => kvp.Key == key).Value;

    // PostPaymentAsync tests

    [Fact]
    public async Task PostPaymentAsync_ValidatesRequest()
    {
        // Arrange
        var request = CreateRequest();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await _sut.PostPaymentAsync(request, cancellationToken);

        // Assert
        _validator.Verify(v => v.ValidateAsync(request, cancellationToken), Times.Once);
    }

    [Fact]
    public async Task PostPaymentAsync_WhenValid_CallsServiceWithMappedRequest()
    {
        // Arrange
        var request = CreateRequest();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await _sut.PostPaymentAsync(request, cancellationToken);

        // Assert
        _paymentsService.Verify(s => s.ProcessPaymentAsync(
                It.Is<ProcessPaymentRequest>(r =>
                    r.Id != Guid.Empty &&
                    r.CardNumber == request.CardNumber &&
                    r.ExpiryMonth == request.ExpiryMonth &&
                    r.ExpiryYear == request.ExpiryYear &&
                    r.Currency == request.Currency &&
                    r.Amount == request.Amount &&
                    r.Cvv == request.Cvv),
                cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task PostPaymentAsync_WhenValid_GeneratesNewIdForEachPayment()
    {
        // Arrange
        var ids = new List<Guid>();
        _paymentsService
            .Setup(s => s.ProcessPaymentAsync(It.IsAny<ProcessPaymentRequest>(), It.IsAny<CancellationToken>()))
            .Callback((ProcessPaymentRequest r, CancellationToken _) => ids.Add(r.Id))
            .ReturnsAsync((ProcessPaymentRequest r, CancellationToken _) => CreatePayment(r.Id));

        // Act
        await _sut.PostPaymentAsync(CreateRequest(), TestContext.Current.CancellationToken);
        await _sut.PostPaymentAsync(CreateRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, ids.Distinct().Count());
    }

    [Fact]
    public async Task PostPaymentAsync_WhenValid_ReturnsOkWithPaymentMappedToResponse()
    {
        // Arrange
        var payment = CreatePayment(Guid.NewGuid());
        _paymentsService
            .Setup(s => s.ProcessPaymentAsync(It.IsAny<ProcessPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(payment);

        // Act
        var result = await _sut.PostPaymentAsync(CreateRequest(), TestContext.Current.CancellationToken);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PostPaymentResponse>(okResult.Value);
        Assert.Equal(payment.Id, response.Id);
        Assert.Equal(payment.Status, response.Status);
        Assert.Equal(payment.CardNumberLastFour, response.CardNumberLastFour);
        Assert.Equal(payment.ExpiryMonth, response.ExpiryMonth);
        Assert.Equal(payment.ExpiryYear, response.ExpiryYear);
        Assert.Equal(payment.Currency, response.Currency);
        Assert.Equal(payment.Amount, response.Amount);
        Assert.Empty(_logger.Collector.GetSnapshot());
    }

    [Fact]
    public async Task PostPaymentAsync_WhenInvalid_ReturnsValidationProblemAndLogsErrors()
    {
        // Arrange
        var request = CreateRequest();
        SetupValidationResult(new ValidationResult(
        [
            new ValidationFailure("CardNumber", "'Card Number' must contain only numeric characters.",
                request.CardNumber),
            new ValidationFailure("Cvv", "'Cvv' must contain only numeric characters.", request.Cvv),
            new ValidationFailure("Cvv", "'Cvv' must be between 3 and 4 characters.", request.Cvv)
        ]));

        // Act
        var result = await _sut.PostPaymentAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        var problemDetails = Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        Assert.Equal(["'Card Number' must contain only numeric characters."], problemDetails.Errors["CardNumber"]);
        Assert.Equal(
            ["'Cvv' must contain only numeric characters.", "'Cvv' must be between 3 and 4 characters."],
            problemDetails.Errors["Cvv"]);

        _paymentsService.Verify(
            s => s.ProcessPaymentAsync(It.IsAny<ProcessPaymentRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal(
            "CardNumber: 'Card Number' must contain only numeric characters.; " +
            "Cvv: 'Cvv' must contain only numeric characters.; " +
            "Cvv: 'Cvv' must be between 3 and 4 characters.",
            GetStateValue(record, "ValidationErrors"));
        Assert.DoesNotContain(request.CardNumber, record.Message);
        Assert.DoesNotContain(request.Cvv, record.Message);
    }

    [Fact]
    public async Task PostPaymentAsync_WhenPaymentRejected_ReturnsUnprocessableEntityProblem()
    {
        // Arrange
        _paymentsService
            .Setup(s => s.ProcessPaymentAsync(It.IsAny<ProcessPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PaymentRejectedException("card has expired"));

        // Act
        var result = await _sut.PostPaymentAsync(CreateRequest(), TestContext.Current.CancellationToken);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, objectResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, problemDetails.Status);
        Assert.Equal("Payment rejected", problemDetails.Title);
        Assert.Equal("Rejected: card has expired", problemDetails.Detail);
    }

    // GetPaymentAsync tests

    [Fact]
    public async Task GetPaymentAsync_WhenFound_ReturnsOkWithPaymentMappedToResponse()
    {
        // Arrange
        var payment = CreatePayment(Guid.NewGuid());
        _paymentsService.Setup(s => s.GetPaymentByIdAsync(payment.Id, TestContext.Current.CancellationToken)).ReturnsAsync(payment);

        // Act
        var result = await _sut.GetPaymentAsync(payment.Id, TestContext.Current.CancellationToken);

        // Assert
        _paymentsService.Verify(s => s.GetPaymentByIdAsync(payment.Id, TestContext.Current.CancellationToken), Times.Once);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<GetPaymentResponse>(okResult.Value);
        Assert.Equal(payment.Id, response.Id);
        Assert.Equal(payment.Status, response.Status);
        Assert.Equal(payment.CardNumberLastFour, response.CardNumberLastFour);
        Assert.Equal(payment.ExpiryMonth, response.ExpiryMonth);
        Assert.Equal(payment.ExpiryYear, response.ExpiryYear);
        Assert.Equal(payment.Currency, response.Currency);
        Assert.Equal(payment.Amount, response.Amount);
        Assert.Empty(_logger.Collector.GetSnapshot());
    }

    [Fact]
    public async Task GetPaymentAsync_WhenNotFound_ReturnsNotFoundAndLogsPaymentId()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        _paymentsService.Setup(s => s.GetPaymentByIdAsync(paymentId, TestContext.Current.CancellationToken)).ReturnsAsync((Payment?)null);

        // Act
        var result = await _sut.GetPaymentAsync(paymentId, TestContext.Current.CancellationToken);

        // Assert
        _paymentsService.Verify(s => s.GetPaymentByIdAsync(paymentId, TestContext.Current.CancellationToken), Times.Once);
        Assert.IsType<NotFoundResult>(result.Result);
        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal($"Payment {paymentId} not found", record.Message);
        Assert.Equal(paymentId.ToString(), GetStateValue(record, "PaymentId"));
    }
}
