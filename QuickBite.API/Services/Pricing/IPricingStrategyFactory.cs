namespace QuickBite.API.Services.Pricing;

public enum PricingStrategyType { Regular, Discount, PromoCode }

public interface IPricingStrategyFactory
{
    IPricingStrategy Create(PricingStrategyType type, decimal? discountPercent = null, string? promoCode = null);
}

public class PricingStrategyFactory : IPricingStrategyFactory
{
    public IPricingStrategy Create(PricingStrategyType type, decimal? discountPercent = null, string? promoCode = null)
    {
        return type switch
        {
            PricingStrategyType.Regular => new RegularPricingStrategy(),

            PricingStrategyType.Discount when discountPercent.HasValue
                => new DiscountPricingStrategy(discountPercent.Value),
            PricingStrategyType.Discount
                => throw new ArgumentException("Strategia Discount wymaga podania discountPercent."),

            PricingStrategyType.PromoCode when promoCode is not null
                => new PromoCodePricingStrategy(promoCode),
            PricingStrategyType.PromoCode
                => throw new ArgumentException("Strategia PromoCode wymaga podania promoCode."),

            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }
}
