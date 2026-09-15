using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Application.Interfaces;
using PaymentGateway.Application.Models;
using PaymentGateway.Domain;

namespace PaymentGateway.IntegrationTests;

public class PostPaymentsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static PostPaymentRequest CreateValidRequest() => new()
    {
        CardNumber = "2222405343248877",
        ExpiryMonth = 4,
        ExpiryYear = DateTime.UtcNow.Year + 1,
        Currency = "gbp",
        Amount = 100,
        Cvv = "123"
    };

    private static HttpClient CreateClient(StubAcquiringBankClient bankClient) =>
        new WebApplicationFactory<PaymentsController>()
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                    services.AddSingleton<IAcquiringBankClient>(bankClient)))
            .CreateClient();

    [Theory]
    [InlineData(true, PaymentStatus.Authorized, "Authorized")]
    [InlineData(false, PaymentStatus.Declined, "Declined")]
    public async Task ProcessesPaymentAndReturnsStatusFromBank(bool authorized, PaymentStatus expectedStatus,
        string expectedStatusJson)
    {
        // Arrange
        var bankClient = new StubAcquiringBankClient(new AcquiringBankPaymentResponse(authorized, null));
        var client = CreateClient(bankClient);
        var request = CreateValidRequest();

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request, TestContext.Current.CancellationToken);
        var paymentResponse =
            await response.Content.ReadFromJsonAsync<PostPaymentResponse>(JsonOptions, TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.NotEqual(Guid.Empty, paymentResponse.Id);
        Assert.Equal(expectedStatus, paymentResponse.Status);
        Assert.Equal(expectedStatusJson, json.RootElement.GetProperty("status").GetString());
        Assert.Equal(8877, paymentResponse.CardNumberLastFour);
        Assert.Equal(request.ExpiryMonth, paymentResponse.ExpiryMonth);
        Assert.Equal(request.ExpiryYear, paymentResponse.ExpiryYear);
        Assert.Equal("GBP", paymentResponse.Currency);
        Assert.Equal(request.Amount, paymentResponse.Amount);
    }

    [Fact]
    public async Task ProcessedPaymentCanBeRetrieved()
    {
        // Arrange
        var bankClient = new StubAcquiringBankClient(new AcquiringBankPaymentResponse(true, "auth-code"));
        var client = CreateClient(bankClient);

        // Act
        var postResponse = await client.PostAsJsonAsync("/api/Payments", CreateValidRequest(),
            TestContext.Current.CancellationToken);
        var postPaymentResponse =
            await postResponse.Content.ReadFromJsonAsync<PostPaymentResponse>(JsonOptions, TestContext.Current.CancellationToken);
        var getResponse = await client.GetAsync($"/api/Payments/{postPaymentResponse!.Id}",
            TestContext.Current.CancellationToken);
        var getPaymentResponse =
            await getResponse.Content.ReadFromJsonAsync<GetPaymentResponse>(JsonOptions, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(getPaymentResponse);
        Assert.Equal(postPaymentResponse.Id, getPaymentResponse.Id);
        Assert.Equal(postPaymentResponse.Status, getPaymentResponse.Status);
    }

    [Fact]
    public async Task Returns400AndDoesNotCallBankIfRequestInvalid()
    {
        // Arrange
        var bankClient = new StubAcquiringBankClient(new AcquiringBankPaymentResponse(true, "auth-code"));
        var client = CreateClient(bankClient);
        var request = CreateValidRequest();
        request.CardNumber = "1234";

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(bankClient.Requests);
    }

    private class StubAcquiringBankClient(AcquiringBankPaymentResponse response) : IAcquiringBankClient
    {
        public List<AcquiringBankPaymentRequest> Requests { get; } = [];

        public Task<AcquiringBankPaymentResponse> ProcessPaymentAsync(
            AcquiringBankPaymentRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(response);
        }
    }
}
