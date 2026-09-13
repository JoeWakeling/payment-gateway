namespace PaymentGateway.Domain;

public static class SupportedCurrencies
{
    private const string Gbp = "GBP";
    private const string Usd = "USD";
    private const string Eur = "EUR";


    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Gbp, Usd, Eur
    };
}