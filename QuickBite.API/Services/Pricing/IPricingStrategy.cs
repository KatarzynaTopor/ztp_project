namespace QuickBite.API.Services.Pricing;

public interface IPricingStrategy
{
    string Name { get; }
    decimal Calculate(decimal itemsTotal, decimal deliveryFee);
}
