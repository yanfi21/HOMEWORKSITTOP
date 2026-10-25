using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shop.Web.Data;
using Shop.Web.Models;

namespace Shop.Web.Services;

public interface ICartService
{
    IReadOnlyList<CartItem> GetItems();
    Task<(bool Ok, string Message)> AddAsync(int productId, int quantity = 1, CancellationToken ct = default);
    void UpdateQuantity(int productId, int quantity);
    void Remove(int productId);
    void Clear();
    int TotalQuantity { get; }
    decimal Total { get; }
}

/// <summary>
/// Корзина на сессии. Цена и название подтягиваются из БД при добавлении —
/// клиент не может подделать цену через AJAX-запрос.
/// </summary>
public class CartService : ICartService
{
    private const string CartKey = "cart:v1";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<CartService> _logger;

    public CartService(IHttpContextAccessor httpContextAccessor, ApplicationDbContext db, ILogger<CartService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
        _logger = logger;
    }

    private ISession Session =>
        _httpContextAccessor.HttpContext?.Session
        ?? throw new InvalidOperationException("Сессия недоступна вне HTTP-запроса");

    public IReadOnlyList<CartItem> GetItems()
    {
        var json = Session.GetString(CartKey);
        if (string.IsNullOrEmpty(json))
        {
            return Array.Empty<CartItem>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<CartItem>>(json) ?? new List<CartItem>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Повреждённые данные корзины в сессии — корзина очищена");
            Session.Remove(CartKey);
            return Array.Empty<CartItem>();
        }
    }

    public async Task<(bool Ok, string Message)> AddAsync(int productId, int quantity = 1, CancellationToken ct = default)
    {
        quantity = Math.Clamp(quantity, 1, 99);

        var product = await _db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive, ct);

        if (product is null)
        {
            return (false, "Товар не найден");
        }

        if (product.Stock <= 0)
        {
            return (false, "Товара нет в наличии");
        }

        var items = GetItems().ToList();
        var existing = items.FirstOrDefault(i => i.ProductId == productId);
        if (existing is not null)
        {
            existing.Quantity = Math.Min(existing.Quantity + quantity, product.Stock);
        }
        else
        {
            items.Add(new CartItem
            {
                ProductId = product.Id,
                Name = product.Name,
                Price = product.Price,
                ImageUrl = product.ImageUrl,
                Quantity = Math.Min(quantity, product.Stock)
            });
        }

        Save(items);
        _logger.LogDebug("В корзину добавлен товар {ProductId} x{Quantity}", productId, quantity);
        return (true, $"«{product.Name}» добавлен в корзину");
    }

    public void UpdateQuantity(int productId, int quantity)
    {
        var items = GetItems().ToList();
        var item = items.FirstOrDefault(i => i.ProductId == productId);
        if (item is null)
        {
            return;
        }

        if (quantity <= 0)
        {
            items.Remove(item);
        }
        else
        {
            item.Quantity = Math.Min(quantity, 99);
        }

        Save(items);
    }

    public void Remove(int productId)
    {
        var items = GetItems().Where(i => i.ProductId != productId).ToList();
        Save(items);
    }

    public void Clear() => Session.Remove(CartKey);

    public int TotalQuantity => GetItems().Sum(i => i.Quantity);
    public decimal Total => GetItems().Sum(i => i.LineTotal);

    private void Save(List<CartItem> items)
    {
        if (items.Count == 0)
        {
            Session.Remove(CartKey);
            return;
        }

        Session.SetString(CartKey, JsonSerializer.Serialize(items));
    }
}
