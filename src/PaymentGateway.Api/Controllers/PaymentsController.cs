using FluentValidation;
using FluentValidation.AspNetCore;

using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Application.Exceptions;
using PaymentGateway.Application.Interfaces;
using PaymentGateway.Application.Models;
using PaymentGateway.Domain;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController(
    IPaymentsService paymentsService,
    IValidator<PostPaymentRequest> validator,
    ILogger<PaymentsController> logger)
    : Controller
{
    [HttpPost]
    public async Task<ActionResult<PostPaymentResponse>> PostPaymentAsync(
        PostPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            logger.LogInformation("Payment request failed validation: {ValidationErrors}",
                string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));

            validationResult.AddToModelState(ModelState);
            return ValidationProblem(ModelState);
        }

        var processPaymentRequest = new ProcessPaymentRequest(
            Guid.NewGuid(),
            request.CardNumber,
            request.ExpiryMonth,
            request.ExpiryYear,
            request.Currency,
            request.Amount,
            request.Cvv);

        Payment payment;
        try
        {
            payment = await paymentsService.ProcessPaymentAsync(processPaymentRequest, cancellationToken);
        }
        catch (PaymentRejectedException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Payment rejected");
        }

        var response = new PostPaymentResponse
        {
            Id = payment.Id,
            Status = payment.Status,
            CardNumberLastFour = payment.CardNumberLastFour,
            ExpiryMonth = payment.ExpiryMonth,
            ExpiryYear = payment.ExpiryYear,
            Currency = payment.Currency,
            Amount = payment.Amount
        };

        return new OkObjectResult(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GetPaymentResponse?>> GetPaymentAsync(Guid id, CancellationToken cancellationToken)
    {
        var payment = await paymentsService.GetPaymentByIdAsync(id, cancellationToken);

        if (payment == null)
        {
            logger.LogInformation("Payment {PaymentId} not found", id);
            return NotFound();
        }

        var response = new GetPaymentResponse
        {
            Id = payment.Id,
            Status = payment.Status,
            CardNumberLastFour = payment.CardNumberLastFour,
            ExpiryMonth = payment.ExpiryMonth,
            ExpiryYear = payment.ExpiryYear,
            Currency = payment.Currency,
            Amount = payment.Amount
        };

        return new OkObjectResult(response);
    }
}
