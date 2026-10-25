using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shop.Web.Data;
using Shop.Web.Models;
using Shop.Web.Services;
using Shop.Web.ViewModels;
using Xunit;

namespace Shop.Tests;

/// <summary>
/// Тесты бизнес-логики на InMemory-базе EF Core: фильтрация/пагинация каталога,
/// кэш категорий, оформление заказа со списанием остатков и защита от чужих заказов.
/// </summary>
public class ShopServiceTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly ProductService _products;
    private readonly OrderService _orders;

    public ShopServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"shop-tests-{Guid.NewGuid():N}")
            .Options;
        _db = new ApplicationDbContext(options);
        _db.Database.EnsureCreated();

        var phones = new Category { Name = "Смартфоны" };
        var laptops = new Category { Name = "Ноутбуки" };
        _db.Categories.AddRange(phones, laptops);
        _db.SaveChanges();

        _db.Products.AddRange(
            new Product { Name = "Смартфон Aurora", CategoryId = phones.Id, Price = 50000, Stock = 5, IsActive = true, IsFeatured = true },
            new Product { Name = "Смартфон Vega", CategoryId = phones.Id, Price = 20000, Stock = 3, IsActive = true },
            new Product { Name = "Ноутбук Nimbus", CategoryId = laptops.Id, Price = 90000, Stock = 2, IsActive = true },
            new Product { Name = "Скрытый товар", CategoryId = laptops.Id, Price = 1000, Stock = 9, IsActive = false });
        _db.SaveChanges();

        var config = new ConfigurationBuilder().Build();
        _products = new ProductService(_db, new MemoryCache(new MemoryCacheOptions()),
            NullLogger<ProductService>.Instance, config);
        _orders = new OrderService(_db, NullLogger<OrderService>.Instance);
    }

    [Fact]
    public async Task GetProducts_Search_ReturnsOnlyActiveMatches()
    {
        var result = await _products.GetProductsAsync(new ProductQuery { Search = "Смартфон" });

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, i => Assert.Contains("Смартфон", i.Name));
    }

    [Fact]
    public async Task GetProducts_PriceSortAndPagination_Work()
    {
        var page1 = await _products.GetProductsAsync(new ProductQuery { Sort = "price-asc", Page = 1, PageSize = 2 });
        var page2 = await _products.GetProductsAsync(new ProductQuery { Sort = "price-asc", Page = 2, PageSize = 2 });

        Assert.Equal(3, page1.TotalCount); // скрытый товар в каталог не попадает
        Assert.Equal(20000, page1.Items[0].Price);
        Assert.Single(page2.Items);
        Assert.Equal(90000, page2.Items[0].Price);
    }

    [Fact]
    public async Task GetCategories_SecondCallComesFromCache()
    {
        var first = await _products.GetCategoriesAsync();
        Assert.Equal(2, first.Count);

        // Добавляем категорию напрямую в БД: из кэша по-прежнему отдаётся старый список,
        // пока админка не вызовет InvalidateCatalogCache().
        _db.Categories.Add(new Category { Name = "Аудио" });
        await _db.SaveChangesAsync();

        var cached = await _products.GetCategoriesAsync();
        Assert.Equal(2, cached.Count);

        _products.InvalidateCatalogCache();
        var fresh = await _products.GetCategoriesAsync();
        Assert.Equal(3, fresh.Count);
    }

    [Fact]
    public async Task CreateOrder_DecreasesStockAndCalculatesTotal()
    {
        var product = await _db.Products.FirstAsync(p => p.Name == "Смартфон Aurora");
        var cart = new List<CartItem>
        {
            new() { ProductId = product.Id, Name = product.Name, Price = 1, Quantity = 2 } // цена клиента игнорируется
        };
        var checkout = new CheckoutViewModel
        {
            CustomerName = "Тест",
            Phone = "+7 900 000-00-00",
            Address = "г. Тест, ул. Тестовая, 1"
        };

        var (ok, _, orderId) = await _orders.CreateOrderAsync("user-1", checkout, cart);

        Assert.True(ok);
        var order = await _db.Orders.Include(o => o.Items).FirstAsync(o => o.Id == orderId);
        Assert.Equal(100000, order.Total); // 2 × 50 000 из БД, а не 2 × 1 из корзины
        Assert.Equal(3, (await _db.Products.FirstAsync(p => p.Id == product.Id)).Stock);
    }

    [Fact]
    public async Task CreateOrder_NotEnoughStock_IsRejected()
    {
        var product = await _db.Products.FirstAsync(p => p.Name == "Ноутбук Nimbus");
        var cart = new List<CartItem> { new() { ProductId = product.Id, Name = product.Name, Quantity = 99 } };
        var checkout = new CheckoutViewModel { CustomerName = "Тест", Phone = "+7 900 000-00-00", Address = "Адрес" };

        var (ok, message, _) = await _orders.CreateOrderAsync("user-1", checkout, cart);

        Assert.False(ok);
        Assert.Contains("Не хватает", message);
        Assert.Equal(0, await _db.Orders.CountAsync());
    }

    [Fact]
    public async Task GetUserOrder_OtherUsersOrder_ReturnsNull()
    {
        var product = await _db.Products.FirstAsync(p => p.IsActive);
        var cart = new List<CartItem> { new() { ProductId = product.Id, Name = product.Name, Quantity = 1 } };
        var checkout = new CheckoutViewModel { CustomerName = "Владелец", Phone = "+7 900 000-00-00", Address = "Адрес" };
        var (_, _, orderId) = await _orders.CreateOrderAsync("owner", checkout, cart);

        var asStranger = await _orders.GetUserOrderAsync("stranger", orderId);

        Assert.Null(asStranger);
    }

    [Fact]
    public void CartItem_LineTotal_IsPriceTimesQuantity()
    {
        var item = new CartItem { Price = 1990m, Quantity = 3 };
        Assert.Equal(5970m, item.LineTotal);
    }

    public void Dispose() => _db.Dispose();
}
