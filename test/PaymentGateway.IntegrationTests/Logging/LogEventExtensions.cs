using Serilog.Events;

namespace PaymentGateway.IntegrationTests.Logging;

public static class LogEventExtensions
{
    public static object? GetScalarValue(this LogEvent logEvent, string propertyName) =>
        logEvent.Properties.TryGetValue(propertyName, out var value) ? (value as ScalarValue)?.Value : null;
}
