using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
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

    [Theory]
    [InlineData(true, PaymentStatus.Authorized, "Authorized")]
    [InlineData(false, PaymentStatus.Declined, "Declined")]
    public async Task ProcessesPaymentAndReturnsStatusFromBank(bool authorized, PaymentStatus expectedStatus,
        string expectedStatusJson)
    {
        // Arrange
        await using var factory = new PaymentGatewayApiFactory();
        factory.AcquiringBank.RespondWithAuthorized(authorized);
        var client = factory.CreateClient();
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
        Assert.Equal("8877", paymentResponse.CardNumberLastFour);
        Assert.Equal(request.ExpiryMonth, paymentResponse.ExpiryMonth);
        Assert.Equal(request.ExpiryYear, paymentResponse.ExpiryYear);
        Assert.Equal("GBP", paymentResponse.Currency);
        Assert.Equal(request.Amount, paymentResponse.Amount);

        var bankRequest = Assert.Single(factory.AcquiringBank.Requests);
        Assert.Equal(HttpMethod.Post, bankRequest.Method);
        Assert.Equal(new Uri("http://localhost:8080/payments"), bankRequest.Uri);
        Assert.NotNull(bankRequest.Body);
        Assert.Equal(request.CardNumber, bankRequest.Body["card_number"]!.GetValue<string>());
        Assert.Equal($"04/{request.ExpiryYear}", bankRequest.Body["expiry_date"]!.GetValue<string>());
        Assert.Equal("GBP", bankRequest.Body["currency"]!.GetValue<string>());
        Assert.Equal(request.Amount, bankRequest.Body["amount"]!.GetValue<long>());
        Assert.Equal(request.Cvv, bankRequest.Body["cvv"]!.GetValue<string>());
    }

    [Fact]
    public async Task ProcessesAmountLargerThanInt32()
    {
        // Arrange
        await using var factory = new PaymentGatewayApiFactory();
        var client = factory.CreateClient();
        var request = CreateValidRequest();
        // One past int.MaxValue: fails if Amount is ever narrowed back to an int.
        request.Amount = 2_147_483_648L;

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request, TestContext.Current.CancellationToken);
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>(
            JsonOptions, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(2_147_483_648L, paymentResponse.Amount);

        var bankRequest = Assert.Single(factory.AcquiringBank.Requests);
        Assert.Equal(2_147_483_648L, bankRequest.Body!["amount"]!.GetValue<long>());
    }

    [Fact]
    public async Task ReturnsCardNumberLastFourWithLeadingZeros()
    {
        // Arrange
        await using var factory = new PaymentGatewayApiFactory();
        var client = factory.CreateClient();
        var request = CreateValidRequest();
        request.CardNumber = "2222405343240012";

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request, TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("0012", json.RootElement.GetProperty("cardNumberLastFour").GetString());
    }

    [Fact]
    public async Task ProcessedPaymentCanBeRetrieved()
    {
        // Arrange
        await using var factory = new PaymentGatewayApiFactory();
        var client = factory.CreateClient();

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
        await using var factory = new PaymentGatewayApiFactory();
        var client = factory.CreateClient();
        var request = CreateValidRequest();
        request.CardNumber = "1234";

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.AcquiringBank.Requests);
    }

    [Fact]
    public async Task Returns400AndDoesNotCallBankIfExpiryYearTooLarge()
    {
        // Arrange
        await using var factory = new PaymentGatewayApiFactory();
        var client = factory.CreateClient();
        var request = CreateValidRequest();
        request.ExpiryYear = 10000;

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.AcquiringBank.Requests);
    }

    [Fact]
    public async Task ProcessesPaymentIfCardExpiresInDecemberOfMaxYear()
    {
        // Arrange
        await using var factory = new PaymentGatewayApiFactory();
        var client = factory.CreateClient();
        var request = CreateValidRequest();
        request.ExpiryMonth = 12;
        request.ExpiryYear = 9999;

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Returns422AndDoesNotCallBankIfCardExpired()
    {
        // Arrange
        await using var factory = new PaymentGatewayApiFactory();
        var client = factory.CreateClient();
        var request = CreateValidRequest();
        request.ExpiryYear = DateTime.UtcNow.Year - 1;

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request, TestContext.Current.CancellationToken);
        var problemDetails =
            await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(problemDetails);
        Assert.Equal("Payment rejected", problemDetails.Title);
        Assert.Equal("Rejected: card has expired", problemDetails.Detail);
        Assert.Empty(factory.AcquiringBank.Requests);
    }

    [Fact]
    public async Task Returns200WithDeclinedStatusIfBankFails()
    {
        // Arrange
        await using var factory = new PaymentGatewayApiFactory();
        factory.AcquiringBank.RespondWithStatusCode(HttpStatusCode.ServiceUnavailable);
        var client = factory.CreateClient();

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
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.Equal(PaymentStatus.Declined, postPaymentResponse.Status);
        Assert.Single(factory.AcquiringBank.Requests);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(PaymentStatus.Declined, getPaymentResponse!.Status);
    }
}
