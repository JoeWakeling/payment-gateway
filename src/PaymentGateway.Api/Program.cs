using System.Text.Json.Serialization;

using FluentValidation;

using Microsoft.OpenApi.Models;

using PaymentGateway.Api.Swagger;
using PaymentGateway.Application;
using PaymentGateway.Application.Interfaces;
using PaymentGateway.Infrastructure;
using PaymentGateway.Infrastructure.AcquiringBank;

using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext(),
    preserveStaticLogger: true);

builder.Services.AddProblemDetails();

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Payment Gateway API",
        Version = "v1",
        Description = "A simplified payment gateway that validates and forwards card payments to an acquiring bank."
    });
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "PaymentGateway.Api.xml"));
    // Lets enum properties (e.g. Status) keep their XML doc description alongside the schema reference
    options.UseAllOfToExtendReferenceSchemas();
    // Replaces the placeholder bodies Swagger UI would otherwise invent for the ProblemDetails schemas
    options.OperationFilter<ProblemDetailsExampleOperationFilter>();
});

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPaymentsRepository, InMemoryPaymentsRepository>();
builder.Services.AddScoped<IPaymentsService, PaymentsService>();
builder.Services.AddHttpClient<IAcquiringBankClient, AcquiringBankClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AcquiringBank:BaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

// Configure the HTTP request pipeline.

// First in the pipeline, so it catches exceptions from everything after it and turns them into a problem+json 500.
// Deliberately outside the request logging middleware: the exception propagates through that middleware first, so
// the request completion event is still written (at Error, with the exception and status code 500) before the
// response is produced.
app.UseExceptionHandler();

app.UseSerilogRequestLogging(options =>
    options.Logger = app.Services.GetRequiredService<Serilog.ILogger>());

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();