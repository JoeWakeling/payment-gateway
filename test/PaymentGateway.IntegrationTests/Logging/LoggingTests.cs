using System.Collections.Concurrent;
using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Controllers;

using Serilog.Core;
using Serilog.Events;

namespace PaymentGateway.IntegrationTests.Logging;

public class LoggingTests
{
    private const string RequestLoggingSourceContext = "Serilog.AspNetCore.RequestLoggingMiddleware";

    private static WebApplicationFactory<PaymentsController> CreateFactory(CollectingSink sink) =>
        new WebApplicationFactory<PaymentsController>()
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                    services.AddSingleton<ILogEventSink>(sink)));

    [Fact]
    public async Task Request_WritesRequestCompletionEventToHostLogger()
    {
        // Arrange
        var sink = new CollectingSink();
        await using var factory = CreateFactory(sink);
        var client = factory.CreateClient();
        var paymentId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/api/Payments/{paymentId}", TestContext.Current.CancellationToken);

        // Assert
        var requestEvent = await sink.WaitForEventAsync(
            e => Equals(e.GetScalarValue("SourceContext"), RequestLoggingSourceContext),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(LogEventLevel.Information, requestEvent.Level);
        Assert.Equal("GET", requestEvent.GetScalarValue("RequestMethod"));
        Assert.Equal($"/api/Payments/{paymentId}", requestEvent.GetScalarValue("RequestPath"));
        Assert.Equal(404, requestEvent.GetScalarValue("StatusCode"));
        Assert.False(string.IsNullOrEmpty(requestEvent.GetScalarValue("RequestId") as string));
    }
}

public class CollectingSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    public IReadOnlyCollection<LogEvent> Events => _events;

    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);

    public async Task<LogEvent> WaitForEventAsync(Func<LogEvent, bool> predicate, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));

        while (true)
        {
            var match = _events.FirstOrDefault(predicate);
            if (match is not null)
            {
                return match;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
        }
    }
}