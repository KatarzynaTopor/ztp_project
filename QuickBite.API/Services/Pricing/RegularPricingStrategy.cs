namespace QuickBite.API.Services.Pricing;

public class RegularPricingStrategy : IPricingStrategy
{
    public string Name => "Regular";

    public decimal Calculate(decimal itemsTotal, decimal deliveryFee) => itemsTotal + deliveryFee;
}
