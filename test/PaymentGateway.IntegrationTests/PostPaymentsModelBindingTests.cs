using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc;

namespace PaymentGateway.IntegrationTests;

public class PostPaymentsModelBindingTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string ValidationProblemType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";

    private static StringContent JsonBody(string body) => new(body, Encoding.UTF8, "application/json");

    [Fact]
    public async Task Returns400ValidationProblemAndDoesNotCallBankIfJsonMalformed()
    {
        // Arrange
        await using var factory = new PaymentGatewayApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/Payments", JsonBody("{\"cardNumber\":"),
            TestContext.Current.CancellationToken);
        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(problemDetails);
        Assert.Equal(ValidationProblemType, problemDetails.Type);
        Assert.Equal("One or more validation errors occurred.", problemDetails.Title);
        Assert.Equal((int)HttpStatusCode.BadRequest, problemDetails.Status);
        var jsonError = Assert.Single(problemDetails.Errors["$.cardNumber"]);
        Assert.Contains("There is an open JSON object or array that should be closed.", jsonError);
        Assert.Empty(factory.AcquiringBank.Requests);
    }

    [Fact]
    public async Task Returns400ValidationProblemAndDoesNotCallBankIfFieldWrongType()
    {
        // Arrange
        await using var factory = new PaymentGatewayApiFactory();
        var client = factory.CreateClient();
        const string body = """
                            {"cardNumber":"2222405343248877","expiryMonth":4,"expiryYear":2030,"currency":"gbp","amount":"not-a-number","cvv":"123"}
                            """;

        // Act
        var response = await client.PostAsync("/api/Payments", JsonBody(body), TestContext.Current.CancellationToken);
        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(problemDetails);
        Assert.Equal(ValidationProblemType, problemDetails.Type);
        Assert.Equal("One or more validation errors occurred.", problemDetails.Title);
        Assert.Equal((int)HttpStatusCode.BadRequest, problemDetails.Status);
        var jsonError = Assert.Single(problemDetails.Errors["$.amount"]);
        Assert.Contains("The JSON value could not be converted to System.Int64.", jsonError);
        Assert.Empty(factory.AcquiringBank.Requests);
    }

    [Fact]
    public async Task Returns400ValidationProblemAndDoesNotCallBankIfBodyEmpty()
    {
        // Arrange
        await using var factory = new PaymentGatewayApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/Payments", JsonBody(string.Empty),
            TestContext.Current.CancellationToken);
        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(problemDetails);
        Assert.Equal(ValidationProblemType, problemDetails.Type);
        Assert.Equal("One or more validation errors occurred.", problemDetails.Title);
        Assert.Equal((int)HttpStatusCode.BadRequest, problemDetails.Status);
        Assert.Equal(["A non-empty request body is required."], problemDetails.Errors[string.Empty]);
        Assert.Empty(factory.AcquiringBank.Requests);
    }

    [Fact]
    public async Task ModelBindingErrorsAlsoReportTheParameterItselfAsMissing()
    {
        // Arrange
        await using var factory = new PaymentGatewayApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/Payments", JsonBody("{\"cardNumber\":"),
            TestContext.Current.CancellationToken);
        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(problemDetails);
        Assert.Equal(["$.cardNumber", "request"], problemDetails.Errors.Keys.OrderBy(key => key));
        Assert.Equal(["The request field is required."], problemDetails.Errors["request"]);
    }

    [Fact]
    public async Task Returns415ProblemAndDoesNotCallBankIfContentMissing()
    {
        // Arrange
        await using var factory = new PaymentGatewayApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/Payments", null, TestContext.Current.CancellationToken);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(problemDetails);
        Assert.Equal("https://tools.ietf.org/html/rfc9110#section-15.5.16", problemDetails.Type);
        Assert.Equal("Unsupported Media Type", problemDetails.Title);
        Assert.Equal((int)HttpStatusCode.UnsupportedMediaType, problemDetails.Status);
        Assert.Empty(factory.AcquiringBank.Requests);
    }
}
