using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Application.Interfaces;
using PaymentGateway.Domain;

namespace PaymentGateway.IntegrationTests;

public class GetPaymentsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly Random _random = new();

    [Fact]
    public async Task RetrievesAPaymentSuccessfully()
    {
        // Arrange
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Declined,
            ExpiryYear = _random.Next(2023, 2030),
            ExpiryMonth = _random.Next(1, 12),
            Amount = _random.Next(1, 10000),
            CardNumberLastFour = _random.Next(0, 10000).ToString("D4"),
            Currency = "GBP"
        };

        await using var factory = new PaymentGatewayApiFactory();
        var paymentsRepository = factory.Services.GetRequiredService<IPaymentsRepository>();
        await paymentsRepository.AddAsync(payment, TestContext.Current.CancellationToken);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/Payments/{payment.Id}", TestContext.Current.CancellationToken);
        var paymentResponse = await response.Content.ReadFromJsonAsync<GetPaymentResponse>(JsonOptions, TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(payment.Id, paymentResponse.Id);
        Assert.Equal(payment.Status, paymentResponse.Status);
        Assert.Equal("Declined", json.RootElement.GetProperty("status").GetString());
        Assert.Equal(payment.CardNumberLastFour, paymentResponse.CardNumberLastFour);
        Assert.Equal(payment.ExpiryMonth, paymentResponse.ExpiryMonth);
        Assert.Equal(payment.ExpiryYear, paymentResponse.ExpiryYear);
        Assert.Equal(payment.Currency, paymentResponse.Currency);
        Assert.Equal(payment.Amount, paymentResponse.Amount);
    }

    [Fact]
    public async Task Returns404IfPaymentNotFound()
    {
        // Arrange
        await using var factory = new PaymentGatewayApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/Payments/{Guid.NewGuid()}", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        // The controller returns a bare NotFound(); the [ApiController] client-error filter is what turns it into
        // problem+json. That is the documented 404 contract, so it is asserted rather than assumed.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var problem = JsonDocument.Parse(body);
        Assert.Equal("Not Found", problem.RootElement.GetProperty("title").GetString());
        Assert.Equal(404, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.5",
            problem.RootElement.GetProperty("type").GetString());
    }
}
