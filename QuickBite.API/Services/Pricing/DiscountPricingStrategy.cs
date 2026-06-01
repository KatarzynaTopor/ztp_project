namespace QuickBite.API.Services.Pricing;

public class DiscountPricingStrategy : IPricingStrategy
{
    private readonly decimal _discountPercent;

    public DiscountPricingStrategy(decimal discountPercent)
    {
        if (discountPercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(discountPercent), "Rabat musi być w zakresie 0–100.");
        _discountPercent = discountPercent;
    }

    public string Name => $"Discount({_discountPercent}%)";

    public decimal Calculate(decimal itemsTotal, decimal deliveryFee)
    {
        var discounted = itemsTotal * (1 - _discountPercent / 100m);
        return discounted + deliveryFee;
    }
}
