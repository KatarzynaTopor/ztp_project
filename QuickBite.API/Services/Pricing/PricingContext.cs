namespace QuickBite.API.Services.Pricing;

public class PricingContext
{
    private IPricingStrategy _strategy;

    public PricingContext(IPricingStrategy strategy)
    {
        _strategy = strategy;
    }

    public void SetStrategy(IPricingStrategy strategy) => _strategy = strategy;

    public string StrategyName => _strategy.Name;

    public decimal CalculateTotal(decimal itemsTotal, decimal deliveryFee)
        => Math.Round(_strategy.Calculate(itemsTotal, deliveryFee), 2);
}
