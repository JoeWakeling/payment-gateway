using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

using PaymentGateway.Api.Controllers;

namespace PaymentGateway.IntegrationTests;

public class SwaggerTests
{
    private const string SwaggerDocumentPath = "/swagger/v1/swagger.json";

    private static WebApplicationFactory<PaymentsController> CreateDevelopmentFactory() =>
        new PaymentGatewayApiFactory()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"));

    [Fact]
    public async Task SwaggerDocument_IsGeneratedAsValidJson()
    {
        // Arrange
        await using var factory = CreateDevelopmentFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync(SwaggerDocumentPath, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        var info = document.RootElement.GetProperty("info");
        Assert.Equal("Payment Gateway API", info.GetProperty("title").GetString());
        Assert.Equal("v1", info.GetProperty("version").GetString());
    }

    [Fact]
    public async Task SwaggerDocument_IncludesSummariesFromXmlCommentsFile()
    {
        // Arrange
        await using var factory = CreateDevelopmentFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync(SwaggerDocumentPath, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        var paths = document.RootElement.GetProperty("paths");
        Assert.Equal("Processes a card payment through the payment gateway.",
            paths.GetProperty("/api/Payments").GetProperty("post").GetProperty("summary").GetString());
        Assert.Equal("Retrieves a previously processed payment.",
            paths.GetProperty("/api/Payments/{id}").GetProperty("get").GetProperty("summary").GetString());
    }

    [Fact]
    public async Task SwaggerDocument_DocumentsBothOperationsAndTheirResponseCodes()
    {
        // Arrange
        await using var factory = CreateDevelopmentFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync(SwaggerDocumentPath, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        var paths = document.RootElement.GetProperty("paths");
        Assert.Equal(new[] { "200", "400", "422" },
            ResponseCodes(paths.GetProperty("/api/Payments").GetProperty("post")));
        Assert.Equal(new[] { "200", "404" },
            ResponseCodes(paths.GetProperty("/api/Payments/{id}").GetProperty("get")));
    }

    [Theory]
    [InlineData("/api/Payments", "post", "400")]
    [InlineData("/api/Payments", "post", "422")]
    [InlineData("/api/Payments/{id}", "get", "404")]
    public async Task SwaggerDocument_DescribesErrorResponsesAsProblemJsonOnly(
        string path, string verb, string statusCode)
    {
        // Arrange
        await using var factory = CreateDevelopmentFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync(SwaggerDocumentPath, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        // Without an explicit content type Swashbuckle also advertises text/plain and text/json, neither of which
        // this API ever returns.
        using var document = JsonDocument.Parse(body);
        var content = document.RootElement
            .GetProperty("paths").GetProperty(path).GetProperty(verb)
            .GetProperty("responses").GetProperty(statusCode)
            .GetProperty("content");

        var mediaType = Assert.Single(content.EnumerateObject());
        Assert.Equal("application/problem+json", mediaType.Name);
    }

    [Theory]
    [InlineData("/api/Payments", "post", "400")]
    [InlineData("/api/Payments", "post", "422")]
    [InlineData("/api/Payments/{id}", "get", "404")]
    public async Task SwaggerDocument_GivesErrorResponsesARealExampleBody(
        string path, string verb, string statusCode)
    {
        // Arrange
        await using var factory = CreateDevelopmentFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync(SwaggerDocumentPath, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        // ProblemDetails has no example values of its own, so without the operation filter Swagger UI renders
        // "title": "string" and "status": 0 as though they were the contract.
        using var document = JsonDocument.Parse(body);
        var example = document.RootElement
            .GetProperty("paths").GetProperty(path).GetProperty(verb)
            .GetProperty("responses").GetProperty(statusCode)
            .GetProperty("content").GetProperty("application/problem+json")
            .GetProperty("example");

        Assert.Equal(int.Parse(statusCode), example.GetProperty("status").GetInt32());
        Assert.NotEqual("string", example.GetProperty("title").GetString());
        Assert.StartsWith("https://", example.GetProperty("type").GetString());
    }

    private static string[] ResponseCodes(JsonElement operation) =>
        operation.GetProperty("responses")
            .EnumerateObject()
            .Select(response => response.Name)
            .Order()
            .ToArray();
}
