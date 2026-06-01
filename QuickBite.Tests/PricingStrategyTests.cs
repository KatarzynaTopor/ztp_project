using QuickBite.API.Services.Pricing;
using Xunit;

namespace QuickBite.Tests;

public class PricingStrategyTests
{
    // ── RegularPricingStrategy ──────────────────────────────────────────────

    [Fact]
    public void Regular_Calculate_ReturnsSumPlusDelivery()
    {
        var sut = new RegularPricingStrategy();
        Assert.Equal(45m, sut.Calculate(40m, 5m));
    }

    [Fact]
    public void Regular_Name_IsRegular()
    {
        Assert.Equal("Regular", new RegularPricingStrategy().Name);
    }

    // ── DiscountPricingStrategy ─────────────────────────────────────────────

    [Theory]
    [InlineData(10,  40, 5, 41)]   // 40 * 0.9 + 5 = 41
    [InlineData(20,  50, 0, 40)]   // 50 * 0.8 + 0 = 40
    [InlineData(0,   30, 5, 35)]   // brak rabatu
    [InlineData(100, 30, 5,  5)]   // 100% rabat — płaci tylko dostawę
    public void Discount_Calculate_AppliesDiscountOnItemsOnly(
        decimal percent, decimal items, decimal delivery, decimal expected)
    {
        var sut = new DiscountPricingStrategy(percent);
        Assert.Equal(expected, sut.Calculate(items, delivery));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Discount_InvalidPercent_ThrowsArgumentOutOfRange(decimal percent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiscountPricingStrategy(percent));
    }

    // ── PromoCodePricingStrategy ────────────────────────────────────────────

    [Theory]
    [InlineData("WELCOME10", 40, 5, 41)]   // 40 * 0.9 + 5
    [InlineData("SUMMER20",  50, 5, 45)]   // 50 * 0.8 + 5
    [InlineData("VIP30",     50, 5, 40)]   // 50 * 0.7 + 5
    public void PromoCode_Calculate_AppliesCorrectDiscount(
        string code, decimal items, decimal delivery, decimal expected)
    {
        var sut = new PromoCodePricingStrategy(code);
        Assert.Equal(expected, sut.Calculate(items, delivery));
    }

    [Fact]
    public void PromoCode_UnknownCode_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new PromoCodePricingStrategy("FAKE99"));
    }

    [Theory]
    [InlineData("WELCOME10", true)]
    [InlineData("welcome10", true)]   // case-insensitive
    [InlineData("FAKE99",    false)]
    public void PromoCode_IsValid_ReturnsExpected(string code, bool expected)
    {
        Assert.Equal(expected, PromoCodePricingStrategy.IsValid(code));
    }

    // ── PricingContext ──────────────────────────────────────────────────────

    [Fact]
    public void Context_CalculateTotal_UsesInjectedStrategy()
    {
        var context = new PricingContext(new RegularPricingStrategy());
        Assert.Equal(45m, context.CalculateTotal(40m, 5m));
    }

    [Fact]
    public void Context_SetStrategy_SwitchesStrategy()
    {
        var context = new PricingContext(new RegularPricingStrategy());
        context.SetStrategy(new DiscountPricingStrategy(50));
        Assert.Equal(25m, context.CalculateTotal(40m, 5m));  // 40*0.5 + 5
    }

    // ── PricingStrategyFactory ──────────────────────────────────────────────

    [Fact]
    public void Factory_Regular_ReturnsRegularStrategy()
    {
        var factory = new PricingStrategyFactory();
        var strategy = factory.Create(PricingStrategyType.Regular);
        Assert.IsType<RegularPricingStrategy>(strategy);
    }

    [Fact]
    public void Factory_Discount_ReturnsDiscountStrategy()
    {
        var factory = new PricingStrategyFactory();
        var strategy = factory.Create(PricingStrategyType.Discount, discountPercent: 15);
        Assert.IsType<DiscountPricingStrategy>(strategy);
    }

    [Fact]
    public void Factory_Discount_MissingPercent_ThrowsArgumentException()
    {
        var factory = new PricingStrategyFactory();
        Assert.Throws<ArgumentException>(() =>
            factory.Create(PricingStrategyType.Discount));
    }

    [Fact]
    public void Factory_PromoCode_ReturnsPromoCodeStrategy()
    {
        var factory = new PricingStrategyFactory();
        var strategy = factory.Create(PricingStrategyType.PromoCode, promoCode: "VIP30");
        Assert.IsType<PromoCodePricingStrategy>(strategy);
    }

    [Fact]
    public void Factory_PromoCode_MissingCode_ThrowsArgumentException()
    {
        var factory = new PricingStrategyFactory();
        Assert.Throws<ArgumentException>(() =>
            factory.Create(PricingStrategyType.PromoCode));
    }
}
