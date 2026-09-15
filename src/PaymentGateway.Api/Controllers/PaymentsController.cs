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
    /// <summary>
    /// Processes a card payment through the payment gateway.
    /// </summary>
    /// <param name="request">The payment details to process.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <response code="200">The payment was processed and stored with a status of Authorized or Declined. A payment is
    /// Declined if the acquiring bank declines it or no valid response is received from the bank.</response>
    /// <response code="400">The request failed validation and was not sent to the acquiring bank.</response>
    /// <response code="422">The payment was rejected (e.g. the card has expired) and was not sent to the acquiring
    /// bank or stored.</response>
    [HttpPost]
    [ProducesResponseType(typeof(PostPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
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

    /// <summary>
    /// Retrieves a previously processed payment.
    /// </summary>
    /// <param name="id" example="0f8fad5b-d9cb-469f-a165-70867728950e">The payment identifier returned when the payment was processed.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <response code="200">The payment was found.</response>
    /// <response code="404">No payment exists with the given identifier.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GetPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
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
