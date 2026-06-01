using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using QuickBite.API.Controllers;
using QuickBite.API.Data;
using QuickBite.API.DTOs.Orders;
using QuickBite.API.Models;
using QuickBite.API.Services;
using QuickBite.API.Services.Pricing;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace QuickBite.Tests;


public class CreateOrderTests
{

    private static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static OrdersController NewController(AppDbContext db, string customerId)
    {
        var stateMachine = new Mock<IOrderStateMachine>();
        var userManager = MockUserManager();
        var factory = new PricingStrategyFactory();

        var controller = new OrdersController(db, stateMachine.Object, userManager.Object, factory);

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, customerId) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
        return controller;
    }

    private static Mock<UserManager<ApplicationUser>> MockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static async Task<(string customerId, Restaurant restaurant, MenuItem available, MenuItem unavailable)>
        SeedAsync(AppDbContext db, decimal deliveryFee = 5m, decimal minOrder = 0m)
    {
        var customer = new ApplicationUser { Id = "cust-1", Email = "c@x.com", UserName = "c", FullName = "Klient", Role = UserRole.Customer };
        var owner = new ApplicationUser { Id = "owner-1", Email = "o@x.com", UserName = "o", FullName = "Wlasciciel", Role = UserRole.Restaurant };
        db.Users.AddRange(customer, owner);

        var restaurant = new Restaurant
        {
            Name = "PizzaTest",
            Address = "ul. Testowa 1",
            OwnerId = owner.Id,
            DeliveryFee = deliveryFee,
            MinOrderAmount = minOrder,
            IsActive = true
        };
        db.Restaurants.Add(restaurant);
        await db.SaveChangesAsync();

        var pizza = new MenuItem { Name = "Margherita", Price = 30m, IsAvailable = true, RestaurantId = restaurant.Id };
        var soldOut = new MenuItem { Name = "Hawajska", Price = 35m, IsAvailable = false, RestaurantId = restaurant.Id };
        db.MenuItems.AddRange(pizza, soldOut);
        await db.SaveChangesAsync();

        return (customer.Id, restaurant, pizza, soldOut);
    }


    [Fact]
    public async Task CreateOrder_ValidRequest_Returns201WithOrderResponse()
    {
        var db = NewDb();
        var (customerId, restaurant, pizza, _) = await SeedAsync(db);
        var controller = NewController(db, customerId);

        var dto = new CreateOrderDto
        {
            RestaurantId = restaurant.Id,
            Items = new() { new() { MenuItemId = pizza.Id, Quantity = 2 } }
        };

        var result = await controller.CreateOrder(dto);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<OrderResponseDto>(created.Value);
        Assert.Equal("Pending", response.Status);
        Assert.Equal(restaurant.Id, response.RestaurantId);
        Assert.Single(response.Items);
        Assert.Equal(2, response.Items[0].Quantity);
    }

    [Fact]
    public async Task CreateOrder_ValidRequest_PersistsOrderAndItems()
    {
        var db = NewDb();
        var (customerId, restaurant, pizza, _) = await SeedAsync(db);
        var controller = NewController(db, customerId);

        var dto = new CreateOrderDto
        {
            RestaurantId = restaurant.Id,
            Items = new() { new() { MenuItemId = pizza.Id, Quantity = 3 } }
        };

        await controller.CreateOrder(dto);

        var saved = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync();
        Assert.NotNull(saved);
        Assert.Equal(customerId, saved!.CustomerId);
        Assert.Equal(restaurant.Id, saved.RestaurantEntityId);
        Assert.Single(saved.Items);
        Assert.Equal(3, saved.Items.First().Quantity);
    }

    [Fact]
    public async Task CreateOrder_SnapshotsMenuItemPriceAndName()
    {
        var db = NewDb();
        var (customerId, restaurant, pizza, _) = await SeedAsync(db);
        var controller = NewController(db, customerId);

        var dto = new CreateOrderDto
        {
            RestaurantId = restaurant.Id,
            Items = new() { new() { MenuItemId = pizza.Id, Quantity = 1 } }
        };

        await controller.CreateOrder(dto);
        var saved = await db.Orders.Include(o => o.Items).FirstAsync();
        var line = saved.Items.First();

        Assert.Equal(30m, line.UnitPriceSnapshot);
        Assert.Equal("Margherita", line.NameSnapshot);
    }


    [Fact]
    public async Task CreateOrder_EmptyCart_ReturnsBadRequest()
    {
        var db = NewDb();
        var (customerId, restaurant, _, _) = await SeedAsync(db);
        var controller = NewController(db, customerId);

        var dto = new CreateOrderDto
        {
            RestaurantId = restaurant.Id,
            Items = new()
        };

        var result = await controller.CreateOrder(dto);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateOrder_NonexistentRestaurant_ReturnsBadRequest()
    {
        var db = NewDb();
        var (customerId, _, pizza, _) = await SeedAsync(db);
        var controller = NewController(db, customerId);

        var dto = new CreateOrderDto
        {
            RestaurantId = 9999,
            Items = new() { new() { MenuItemId = pizza.Id, Quantity = 1 } }
        };

        var result = await controller.CreateOrder(dto);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateOrder_InactiveRestaurant_ReturnsBadRequest()
    {
        var db = NewDb();
        var (customerId, restaurant, pizza, _) = await SeedAsync(db);
        restaurant.IsActive = false;
        await db.SaveChangesAsync();
        var controller = NewController(db, customerId);

        var dto = new CreateOrderDto
        {
            RestaurantId = restaurant.Id,
            Items = new() { new() { MenuItemId = pizza.Id, Quantity = 1 } }
        };

        var result = await controller.CreateOrder(dto);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateOrder_UnavailableMenuItem_ReturnsBadRequest()
    {
        var db = NewDb();
        var (customerId, restaurant, _, soldOut) = await SeedAsync(db);
        var controller = NewController(db, customerId);

        var dto = new CreateOrderDto
        {
            RestaurantId = restaurant.Id,
            Items = new() { new() { MenuItemId = soldOut.Id, Quantity = 1 } }
        };

        var result = await controller.CreateOrder(dto);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateOrder_MenuItemFromOtherRestaurant_ReturnsBadRequest()
    {
        var db = NewDb();
        var (customerId, restaurant, _, _) = await SeedAsync(db);

        // Druga restauracja z własną pozycją
        var owner2 = new ApplicationUser { Id = "owner-2", Email = "o2@x.com", UserName = "o2", FullName = "W2", Role = UserRole.Restaurant };
        db.Users.Add(owner2);
        var rest2 = new Restaurant { Name = "Inna", Address = "ul. Inna 1", OwnerId = owner2.Id, DeliveryFee = 3m, IsActive = true };
        db.Restaurants.Add(rest2);
        await db.SaveChangesAsync();
        var foreignItem = new MenuItem { Name = "Cudza", Price = 20m, IsAvailable = true, RestaurantId = rest2.Id };
        db.MenuItems.Add(foreignItem);
        await db.SaveChangesAsync();

        var controller = NewController(db, customerId);
        var dto = new CreateOrderDto
        {
            RestaurantId = restaurant.Id,
            Items = new() { new() { MenuItemId = foreignItem.Id, Quantity = 1 } }
        };

        var result = await controller.CreateOrder(dto);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateOrder_BelowMinOrderAmount_ReturnsBadRequest()
    {
        var db = NewDb();
        var (customerId, restaurant, pizza, _) = await SeedAsync(db, deliveryFee: 5m, minOrder: 100m);
        var controller = NewController(db, customerId);

        // Pizza za 30 * 1 = 30 < 100 (min order)
        var dto = new CreateOrderDto
        {
            RestaurantId = restaurant.Id,
            Items = new() { new() { MenuItemId = pizza.Id, Quantity = 1 } }
        };

        var result = await controller.CreateOrder(dto);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateOrder_MeetsMinOrderAmount_Succeeds()
    {
        var db = NewDb();
        var (customerId, restaurant, pizza, _) = await SeedAsync(db, deliveryFee: 5m, minOrder: 30m);
        var controller = NewController(db, customerId);

        // 30 * 1 = 30 == 30 (min order) → OK
        var dto = new CreateOrderDto
        {
            RestaurantId = restaurant.Id,
            Items = new() { new() { MenuItemId = pizza.Id, Quantity = 1 } }
        };

        var result = await controller.CreateOrder(dto);
        Assert.IsType<CreatedAtActionResult>(result);
    }

    [Fact]
    public async Task CreateOrder_RegularStrategy_TotalEqualsItemsPlusDelivery()
    {
        var db = NewDb();
        var (customerId, restaurant, pizza, _) = await SeedAsync(db, deliveryFee: 5m);
        var controller = NewController(db, customerId);

        var dto = new CreateOrderDto
        {
            RestaurantId = restaurant.Id,
            Items = new() { new() { MenuItemId = pizza.Id, Quantity = 2 } },  // 30*2 = 60
            Strategy = PricingStrategyType.Regular
        };

        var result = await controller.CreateOrder(dto);
        var response = (OrderResponseDto)((CreatedAtActionResult)result).Value!;

        Assert.Equal(60m, response.ItemsTotal);
        Assert.Equal(5m, response.DeliveryFee);
        Assert.Equal(0m, response.Discount);
        Assert.Equal(65m, response.TotalAmount);   // 60 + 5
    }

    [Fact]
    public async Task CreateOrder_DiscountStrategy_AppliesDiscountToItems()
    {
        var db = NewDb();
        var (customerId, restaurant, pizza, _) = await SeedAsync(db, deliveryFee: 5m);
        var controller = NewController(db, customerId);

        // 30 * 2 = 60 items; 10% rabat → 54 + 5 = 59
        var dto = new CreateOrderDto
        {
            RestaurantId = restaurant.Id,
            Items = new() { new() { MenuItemId = pizza.Id, Quantity = 2 } },
            Strategy = PricingStrategyType.Discount,
            DiscountPercent = 10m
        };

        var result = await controller.CreateOrder(dto);
        var response = (OrderResponseDto)((CreatedAtActionResult)result).Value!;

        Assert.Equal(60m, response.ItemsTotal);
        Assert.Equal(5m, response.DeliveryFee);
        Assert.Equal(6m, response.Discount);       // 60 - 54
        Assert.Equal(59m, response.TotalAmount);
    }

    [Fact]
    public async Task CreateOrder_PromoCodeWELCOME10_Applies10PercentDiscount()
    {
        var db = NewDb();
        var (customerId, restaurant, pizza, _) = await SeedAsync(db, deliveryFee: 5m);
        var controller = NewController(db, customerId);

        var dto = new CreateOrderDto
        {
            RestaurantId = restaurant.Id,
            Items = new() { new() { MenuItemId = pizza.Id, Quantity = 2 } }, // 60
            Strategy = PricingStrategyType.PromoCode,
            PromoCode = "WELCOME10"
        };

        var result = await controller.CreateOrder(dto);
        var response = (OrderResponseDto)((CreatedAtActionResult)result).Value!;

        Assert.Equal(59m, response.TotalAmount);  // 60*0.9 + 5
        Assert.Contains("WELCOME10", response.PricingStrategy);
    }

    [Fact]
    public async Task CreateOrder_UnknownPromoCode_ReturnsBadRequest()
    {
        var db = NewDb();
        var (customerId, restaurant, pizza, _) = await SeedAsync(db);
        var controller = NewController(db, customerId);

        var dto = new CreateOrderDto
        {
            RestaurantId = restaurant.Id,
            Items = new() { new() { MenuItemId = pizza.Id, Quantity = 1 } },
            Strategy = PricingStrategyType.PromoCode,
            PromoCode = "FAKE99"
        };

        var result = await controller.CreateOrder(dto);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateOrder_DiscountStrategyWithoutPercent_ReturnsBadRequest()
    {
        var db = NewDb();
        var (customerId, restaurant, pizza, _) = await SeedAsync(db);
        var controller = NewController(db, customerId);

        var dto = new CreateOrderDto
        {
            RestaurantId = restaurant.Id,
            Items = new() { new() { MenuItemId = pizza.Id, Quantity = 1 } },
            Strategy = PricingStrategyType.Discount,
            DiscountPercent = null
        };

        var result = await controller.CreateOrder(dto);
        Assert.IsType<BadRequestObjectResult>(result);
    }


    [Fact]
    public async Task CreateOrder_NewOrder_HasPendingStatus()
    {
        var db = NewDb();
        var (customerId, restaurant, pizza, _) = await SeedAsync(db);
        var controller = NewController(db, customerId);

        var dto = new CreateOrderDto
        {
            RestaurantId = restaurant.Id,
            Items = new() { new() { MenuItemId = pizza.Id, Quantity = 1 } }
        };

        await controller.CreateOrder(dto);
        var saved = await db.Orders.FirstAsync();
        Assert.Equal(OrderStatus.Pending, saved.Status);
    }

    [Fact]
    public async Task CreateOrder_MultipleItems_SumsCorrectly()
    {
        var db = NewDb();
        var (customerId, restaurant, pizza, _) = await SeedAsync(db, deliveryFee: 5m);

        var pasta = new MenuItem { Name = "Carbonara", Price = 25m, IsAvailable = true, RestaurantId = restaurant.Id };
        db.MenuItems.Add(pasta);
        await db.SaveChangesAsync();

        var controller = NewController(db, customerId);
        var dto = new CreateOrderDto
        {
            RestaurantId = restaurant.Id,
            Items = new()
            {
                new() { MenuItemId = pizza.Id, Quantity = 2 },   // 30*2 = 60
                new() { MenuItemId = pasta.Id, Quantity = 1 }    // 25*1 = 25
            }
        };

        var result = await controller.CreateOrder(dto);
        var response = (OrderResponseDto)((CreatedAtActionResult)result).Value!;

        Assert.Equal(85m, response.ItemsTotal);       // 60 + 25
        Assert.Equal(90m, response.TotalAmount);      // 85 + 5
        Assert.Equal(2, response.Items.Count);
    }
}
