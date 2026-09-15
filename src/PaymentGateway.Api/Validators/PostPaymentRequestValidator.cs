using FluentValidation;

using JetBrains.Annotations;

using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Domain;

namespace PaymentGateway.Api.Validators;

[UsedImplicitly]
public class PostPaymentRequestValidator : AbstractValidator<PostPaymentRequest>
{
    public PostPaymentRequestValidator()
    {
        RuleFor(x => x.CardNumber)
            .NotEmpty()
            .Length(14, 19)
            .Must(x => x is not null && x.All(char.IsAsciiDigit))
            .WithMessage("'{PropertyName}' must contain only numeric characters.");

        RuleFor(x => x.ExpiryMonth)
            .InclusiveBetween(1, 12);

        RuleFor(x => x.ExpiryYear)
            .NotEmpty()
            .GreaterThan(0); // Application logic is responsible for rejecting expired cards

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Must(x => SupportedCurrencies.All.Contains(x))
            .WithMessage(
                $"'{{PropertyName}}' must be a supported currency ({string.Join(", ", SupportedCurrencies.All)}).");

        RuleFor(x => x.Amount)
            .GreaterThan(0);

        RuleFor(x => x.Cvv)
            .NotEmpty()
            .Length(3, 4)
            .Must(x => x is not null && x.All(char.IsAsciiDigit))
            .WithMessage("'{PropertyName}' must contain only numeric characters.");
    }
}