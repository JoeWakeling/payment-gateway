using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Controllers;

namespace PaymentGateway.IntegrationTests;

public class PaymentGatewayApiFactory : WebApplicationFactory<PaymentsController>
{
    public StubAcquiringBankHandler AcquiringBank { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureServices(services =>
            services.ConfigureHttpClientDefaults(httpClient =>
                httpClient.ConfigurePrimaryHttpMessageHandler(() => AcquiringBank)));
}
