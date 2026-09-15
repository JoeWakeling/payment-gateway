using System.Net;
using System.Net.Http.Json;

using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Models.Requests;
using PaymentGateway.IntegrationTests.Logging;

using Serilog.Core;
using Serilog.Events;

namespace PaymentGateway.IntegrationTests;

public class UnhandledExceptionTests
{
    private const string RequestLoggingSourceContext = "Serilog.AspNetCore.RequestLoggingMiddleware";
    private const string ExceptionMessage = "stub acquiring bank handler failed";

    private static PostPaymentRequest CreateValidRequest() => new()
    {
        CardNumber = "2222405343248877",
        ExpiryMonth = 4,
        ExpiryYear = DateTime.UtcNow.Year + 1,
        Currency = "gbp",
        Amount = 100,
        Cvv = "123"
    };

    [Fact]
    public async Task Returns500ProblemDetailsWithoutExceptionDetailsIfExceptionUnhandled()
    {
        // Arrange
        await using var factory = new PaymentGatewayApiFactory();
        factory.AcquiringBank.ThrowOnSend(new InvalidOperationException(ExceptionMessage));
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", CreateValidRequest(),
            TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            """
            {"type":"https://tools.ietf.org/html/rfc9110#section-15.6.1","title":"An error occurred while processing your request.","status":500}
            """,
            body);
        Assert.DoesNotContain(ExceptionMessage, body);
        Assert.DoesNotContain(nameof(InvalidOperationException), body);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LogsUnhandledExceptionWithRequestCompletionEvent()
    {
        // Arrange
        var sink = new CollectingSink();
        await using var factory = new PaymentGatewayApiFactory();
        await using var factoryWithSink = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<ILogEventSink>(sink)));
        factory.AcquiringBank.ThrowOnSend(new InvalidOperationException(ExceptionMessage));
        var client = factoryWithSink.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", CreateValidRequest(),
            TestContext.Current.CancellationToken);

        // Assert
        var requestEvent = await sink.WaitForEventAsync(
            e => Equals(e.GetScalarValue("SourceContext"), RequestLoggingSourceContext),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(LogEventLevel.Error, requestEvent.Level);
        Assert.Equal("POST", requestEvent.GetScalarValue("RequestMethod"));
        Assert.Equal("/api/Payments", requestEvent.GetScalarValue("RequestPath"));
        Assert.Equal(500, requestEvent.GetScalarValue("StatusCode"));
        var loggedException = Assert.IsType<InvalidOperationException>(requestEvent.Exception);
        Assert.Equal(ExceptionMessage, loggedException.Message);
    }
}
