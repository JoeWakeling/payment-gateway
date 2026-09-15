using System.Net;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using PaymentGateway.Application.Exceptions;
using PaymentGateway.Application.Interfaces;
using PaymentGateway.Application.Models;
using PaymentGateway.Infrastructure.AcquiringBank;

namespace PaymentGateway.Infrastructure.Tests.AcquiringBank;

public class AcquiringBankClientTests
{
    private static readonly Uri BaseAddress = new("http://localhost:8080/");

    private static AcquiringBankPaymentRequest CreateRequest(int expiryMonth = 4, int expiryYear = 2025) => new(
        CardNumber: "2222405343248877",
        ExpiryMonth: expiryMonth,
        ExpiryYear: expiryYear,
        Currency: "GBP",
        Amount: 100,
        Cvv: "123");

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body) => new(statusCode)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private static (IAcquiringBankClient Client, StubHttpMessageHandler Handler, FakeLogger<AcquiringBankClient> Logger)
        CreateClient(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler,
            TimeSpan? timeout = null)
    {
        var stubHandler = new StubHttpMessageHandler(handler);
        var httpClient = new HttpClient(stubHandler) { BaseAddress = BaseAddress };
        if (timeout.HasValue)
        {
            httpClient.Timeout = timeout.Value;
        }

        var logger = new FakeLogger<AcquiringBankClient>();

        return (new AcquiringBankClient(httpClient, logger), stubHandler, logger);
    }

    private static (IAcquiringBankClient Client, StubHttpMessageHandler Handler, FakeLogger<AcquiringBankClient> Logger)
        CreateClient(HttpStatusCode statusCode, string body) =>
        CreateClient((_, _) => Task.FromResult(JsonResponse(statusCode, body)));

    [Fact]
    public async Task ProcessPaymentAsync_ValidRequest_PostsSnakeCaseBodyToPaymentsEndpoint()
    {
        // Arrange
        var (client, handler, _) =
            CreateClient(HttpStatusCode.OK, """{"authorized":true,"authorization_code":"abc"}""");

        // Act
        await client.ProcessPaymentAsync(CreateRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Equal(new Uri("http://localhost:8080/payments"), handler.LastRequest.RequestUri);

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        var root = body.RootElement;
        Assert.Equal("2222405343248877", root.GetProperty("card_number").GetString());
        Assert.Equal("04/2025", root.GetProperty("expiry_date").GetString());
        Assert.Equal("GBP", root.GetProperty("currency").GetString());
        Assert.Equal(100, root.GetProperty("amount").GetInt32());
        Assert.Equal("123", root.GetProperty("cvv").GetString());
    }

    [Theory]
    [InlineData(1, 2030, "01/2030")]
    [InlineData(9, 2026, "09/2026")]
    [InlineData(12, 2027, "12/2027")]
    public async Task ProcessPaymentAsync_ExpiryDate_FormattedAsTwoDigitMonthAndFourDigitYear(
        int expiryMonth, int expiryYear, string expected)
    {
        // Arrange
        var (client, handler, _) =
            CreateClient(HttpStatusCode.OK, """{"authorized":true,"authorization_code":"abc"}""");

        // Act
        await client.ProcessPaymentAsync(CreateRequest(expiryMonth, expiryYear), TestContext.Current.CancellationToken);

        // Assert
        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal(expected, body.RootElement.GetProperty("expiry_date").GetString());
    }

    [Fact]
    public async Task ProcessPaymentAsync_BankAuthorizes_ReturnsAuthorizedWithCode()
    {
        // Arrange
        var (client, _, logger) = CreateClient(HttpStatusCode.OK,
            """{"authorized":true,"authorization_code":"0bb07405-6d44-4b50-a14f-7ae0beff13ad"}""");

        // Act
        var result = await client.ProcessPaymentAsync(CreateRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Authorized);
        Assert.Equal("0bb07405-6d44-4b50-a14f-7ae0beff13ad", result.AuthorizationCode);
        var record = Assert.Single(logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Information);
        Assert.Equal("True", record.GetStructuredStateValue("Authorized"));
        Assert.NotNull(record.GetStructuredStateValue("ElapsedMs"));
    }

    [Fact]
    public async Task ProcessPaymentAsync_BankDeclines_ReturnsUnauthorized()
    {
        // Arrange
        var (client, _, logger) = CreateClient(HttpStatusCode.OK, """{"authorized":false,"authorization_code":""}""");

        // Act
        var result = await client.ProcessPaymentAsync(CreateRequest(), TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Authorized);
        var record = Assert.Single(logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Information);
        Assert.Equal("False", record.GetStructuredStateValue("Authorized"));
    }

    [Fact]
    public async Task ProcessPaymentAsync_ServiceUnavailable_ThrowsAcquiringBankUnavailableException()
    {
        // Arrange
        var (client, _, logger) = CreateClient(HttpStatusCode.ServiceUnavailable, "{}");

        // Act & Assert
        await Assert.ThrowsAsync<AcquiringBankUnavailableException>(() =>
            client.ProcessPaymentAsync(CreateRequest(), TestContext.Current.CancellationToken));
        var record = Assert.Single(logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        Assert.Contains("unavailable", record.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest,
        """{"error_message":"Not all required properties were sent in the request"}""")]
    [InlineData(HttpStatusCode.InternalServerError, "")]
    [InlineData(HttpStatusCode.NotFound, "")]
    public async Task ProcessPaymentAsync_UnexpectedStatusCode_ThrowsAcquiringBankException(
        HttpStatusCode statusCode, string body)
    {
        // Arrange
        var (client, _, logger) = CreateClient(statusCode, body);

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<AcquiringBankException>(() =>
                client.ProcessPaymentAsync(CreateRequest(), TestContext.Current.CancellationToken));
        Assert.Contains(((int)statusCode).ToString(), exception.Message);
        var record = Assert.Single(logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        Assert.Equal(((int)statusCode).ToString(), record.GetStructuredStateValue("StatusCode"));
    }

    [Fact]
    public async Task ProcessPaymentAsync_NullResponseBody_ThrowsAcquiringBankException()
    {
        // Arrange
        var (client, _, logger) = CreateClient(HttpStatusCode.OK, "null");

        // Act & Assert
        await Assert.ThrowsAsync<AcquiringBankException>(() =>
            client.ProcessPaymentAsync(CreateRequest(), TestContext.Current.CancellationToken));
        Assert.Single(logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task ProcessPaymentAsync_MalformedResponseBody_ThrowsAcquiringBankException()
    {
        // Arrange
        var (client, _, logger) = CreateClient(HttpStatusCode.OK, "not json");

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<AcquiringBankException>(() =>
                client.ProcessPaymentAsync(CreateRequest(), TestContext.Current.CancellationToken));
        Assert.IsType<JsonException>(exception.InnerException);
        var record = Assert.Single(logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        Assert.Same(exception.InnerException, record.Exception);
    }

    [Fact]
    public async Task ProcessPaymentAsync_HttpRequestFails_ThrowsAcquiringBankExceptionWithInnerException()
    {
        // Arrange
        var httpRequestException = new HttpRequestException("Connection refused");
        var (client, _, logger) = CreateClient((_, _) => throw httpRequestException);

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<AcquiringBankException>(() =>
                client.ProcessPaymentAsync(CreateRequest(), TestContext.Current.CancellationToken));
        Assert.Same(httpRequestException, exception.InnerException);
        var record = Assert.Single(logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        Assert.Same(httpRequestException, record.Exception);
    }

    [Fact]
    public async Task ProcessPaymentAsync_HttpClientTimesOut_ThrowsAcquiringBankException()
    {
        // Arrange
        var (client, _, logger) = CreateClient(async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, timeout: TimeSpan.FromMilliseconds(50));

        // Act & Assert
        var exception =
            await Assert.ThrowsAsync<AcquiringBankException>(() =>
                client.ProcessPaymentAsync(CreateRequest(), TestContext.Current.CancellationToken));
        Assert.IsAssignableFrom<OperationCanceledException>(exception.InnerException);
        var record = Assert.Single(logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        Assert.Same(exception.InnerException, record.Exception);
    }

    [Fact]
    public async Task ProcessPaymentAsync_CallerCancels_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (client, _, logger) = CreateClient(async (_, ct) =>
        {
            await cts.CancelAsync();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ProcessPaymentAsync(CreateRequest(), cts.Token));
        Assert.DoesNotContain(logger.Collector.GetSnapshot(), r => r.Level >= LogLevel.Warning);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, """{"authorized":true,"authorization_code":"abc"}""")]
    [InlineData(HttpStatusCode.OK, "not json")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "{}")]
    [InlineData(HttpStatusCode.BadRequest,
        """{"error_message":"Not all required properties were sent in the request"}""")]
    public async Task ProcessPaymentAsync_DoesNotLogCardNumberOrCvv(HttpStatusCode statusCode, string body)
    {
        // Arrange
        var (client, _, logger) = CreateClient(statusCode, body);
        var request = CreateRequest() with { Cvv = "7391" };

        // Act
        try
        {
            await client.ProcessPaymentAsync(request, TestContext.Current.CancellationToken);
        }
        catch (AcquiringBankException)
        {
        }

        // Assert
        var records = logger.Collector.GetSnapshot();
        Assert.Contains(records, r => r.Level == LogLevel.Debug);
        foreach (var record in records)
        {
            var loggedText = string.Join(" ",
                new[] { record.Message, record.Exception?.ToString() }
                    .Concat(record.StructuredState?.Select(kv => kv.Value) ?? []));

            Assert.DoesNotContain(request.CardNumber, loggedText);
            Assert.DoesNotContain(request.Cvv, loggedText);
        }
    }
}