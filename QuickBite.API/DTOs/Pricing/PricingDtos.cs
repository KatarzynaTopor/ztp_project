using System.ComponentModel.DataAnnotations;
using QuickBite.API.Services.Pricing;

namespace QuickBite.API.DTOs.Pricing;

public record PricingItemRequest(
    int MenuItemId,
    [Range(1, 99)] int Quantity
);

public record CalculatePriceDto(
    int RestaurantId,
    [MinLength(1)] List<PricingItemRequest> Items,
    PricingStrategyType Strategy = PricingStrategyType.Regular,
    [Range(0, 100)] decimal? DiscountPercent = null,
    string? PromoCode = null
);
