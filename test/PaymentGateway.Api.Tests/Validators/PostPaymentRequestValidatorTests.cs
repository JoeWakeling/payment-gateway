using FluentValidation.TestHelper;

using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Validators;

namespace PaymentGateway.Api.Tests.Validators;

public class PostPaymentRequestValidatorTests
{
    private readonly PostPaymentRequestValidator _validator = new();

    private static PostPaymentRequest CreateValidRequest() => new()
    {
        CardNumber = "12345678901234",
        ExpiryMonth = 6,
        ExpiryYear = 2020,
        Currency = "GBP",
        Amount = 100,
        Cvv = "123"
    };

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var result = _validator.TestValidate(CreateValidRequest());

        result.ShouldNotHaveAnyValidationErrors();
    }

    // CardNumber tests

    [Fact]
    public void CardNumber_WhenNull_FailsValidation()
    {
        var request = CreateValidRequest();
        request.CardNumber = null!;

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.CardNumber);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("    ")]
    public void CardNumber_WhenEmpty_FailsValidation(string cardNumber)
    {
        var request = CreateValidRequest();
        request.CardNumber = cardNumber;

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.CardNumber);
    }

    [Theory]
    [InlineData("1234567890123")] // 13 chars - too short
    [InlineData("12345678901234567890")] // 20 chars - too long
    public void CardNumber_WhenInvalidLength_FailsValidation(string cardNumber)
    {
        var request = CreateValidRequest();
        request.CardNumber = cardNumber;

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.CardNumber);
    }

    [Theory]
    [InlineData("12345678901234")] // 14 chars
    [InlineData("1234567890123456789")] // 19 chars
    public void CardNumber_WhenValidLength_PassesValidation(string cardNumber)
    {
        var request = CreateValidRequest();
        request.CardNumber = cardNumber;

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.CardNumber);
    }

    [Fact]
    public void CardNumber_WhenContainsNonDigits_FailsValidation()
    {
        var request = CreateValidRequest();
        request.CardNumber = "1234abcd901234";

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.CardNumber);
    }

    [Theory]
    [InlineData("1234567890\u0664\u0664\u0664\u0664")] // Arabic-Indic digits
    [InlineData("1234567890\uFF14\uFF14\uFF14\uFF14")] // Fullwidth digits
    public void CardNumber_WhenContainsNonAsciiDigits_FailsValidation(string cardNumber)
    {
        var request = CreateValidRequest();
        request.CardNumber = cardNumber;

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.CardNumber);
    }

    // ExpiryMonth tests

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public void ExpiryMonth_WhenOutOfRange_FailsValidation(int month)
    {
        var request = CreateValidRequest();
        request.ExpiryMonth = month;

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.ExpiryMonth);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    public void ExpiryMonth_WhenInRange_PassesValidation(int month)
    {
        var request = CreateValidRequest();
        request.ExpiryMonth = month;

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.ExpiryMonth);
    }

    // ExpiryYear tests

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ExpiryYear_WhenZeroOrNegative_FailsValidation(int year)
    {
        var request = CreateValidRequest();
        request.ExpiryYear = year;

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.ExpiryYear);
    }

    [Fact]
    public void ExpiryYear_WhenPositive_PassesValidation()
    {
        var request = CreateValidRequest();
        request.ExpiryYear = 2027;

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.ExpiryYear);
    }

    // Currency tests

    [Fact]
    public void Currency_WhenNull_FailsValidation()
    {
        var request = CreateValidRequest();
        request.Currency = null!;

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Currency);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Currency_WhenEmpty_FailsValidation(string currency)
    {
        var request = CreateValidRequest();
        request.Currency = currency;

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Currency);
    }

    [Theory]
    [InlineData("JPY")]
    [InlineData("CHF")]
    [InlineData("ABC")]
    public void Currency_WhenUnsupported_FailsValidation(string currency)
    {
        var request = CreateValidRequest();
        request.Currency = currency;

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Currency);
    }

    [Theory]
    [InlineData("GBP")]
    [InlineData("USD")]
    [InlineData("EUR")]
    public void Currency_WhenSupported_PassesValidation(string currency)
    {
        var request = CreateValidRequest();
        request.Currency = currency;

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Currency);
    }

    [Theory]
    [InlineData("gbp")]
    [InlineData("usd")]
    [InlineData("Eur")]
    public void Currency_WhenSupportedButDifferentCase_PassesValidation(string currency)
    {
        var request = CreateValidRequest();
        request.Currency = currency;

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Currency);
    }

    // Amount tests

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Amount_WhenZeroOrNegative_FailsValidation(int amount)
    {
        var request = CreateValidRequest();
        request.Amount = amount;

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Amount_WhenPositive_PassesValidation()
    {
        var request = CreateValidRequest();
        request.Amount = 1;

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Amount);
    }

    // Cvv tests

    [Fact]
    public void Cvv_WhenNull_FailsValidation()
    {
        var request = CreateValidRequest();
        request.Cvv = null!;

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Cvv);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Cvv_WhenEmpty_FailsValidation(string cvv)
    {
        var request = CreateValidRequest();
        request.Cvv = cvv;

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Cvv);
    }

    [Theory]
    [InlineData("12")] // 2 chars - too short
    [InlineData("12345")] // 5 chars - too long
    public void Cvv_WhenInvalidLength_FailsValidation(string cvv)
    {
        var request = CreateValidRequest();
        request.Cvv = cvv;

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Cvv);
    }

    [Theory]
    [InlineData("123")] // 3 chars
    [InlineData("1234")] // 4 chars
    public void Cvv_WhenValidLength_PassesValidation(string cvv)
    {
        var request = CreateValidRequest();
        request.Cvv = cvv;

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Cvv);
    }

    [Fact]
    public void Cvv_WhenContainsNonDigits_FailsValidation()
    {
        var request = CreateValidRequest();
        request.Cvv = "12a";

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Cvv);
    }

    [Theory]
    [InlineData("12\u0664")] // Arabic-Indic digit
    [InlineData("12\uFF14")] // Fullwidth digit
    public void Cvv_WhenContainsNonAsciiDigits_FailsValidation(string cvv)
    {
        var request = CreateValidRequest();
        request.Cvv = cvv;

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Cvv);
    }
}