namespace QuickBite.API.Services.Pricing;

public class PromoCodePricingStrategy : IPricingStrategy
{
    private static readonly Dictionary<string, decimal> KnownCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "WELCOME10", 10m },
        { "SUMMER20",  20m },
        { "VIP30",     30m }
    };

    private readonly decimal _discountPercent;

    public string Name { get; }

    public PromoCodePricingStrategy(string promoCode)
    {
        if (!KnownCodes.TryGetValue(promoCode, out var discount))
            throw new ArgumentException($"Nieznany kod promocyjny: '{promoCode}'.");
        _discountPercent = discount;
        Name = $"PromoCode({promoCode.ToUpperInvariant()}, {discount}%)";
    }

    public static bool IsValid(string promoCode) => KnownCodes.ContainsKey(promoCode);

    public decimal Calculate(decimal itemsTotal, decimal deliveryFee)
    {
        var discounted = itemsTotal * (1 - _discountPercent / 100m);
        return discounted + deliveryFee;
    }
}
