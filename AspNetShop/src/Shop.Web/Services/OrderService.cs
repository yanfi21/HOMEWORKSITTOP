using Microsoft.EntityFrameworkCore;
using Shop.Web.Data;
using Shop.Web.Models;
using Shop.Web.ViewModels;

namespace Shop.Web.Services;

public interface IOrderService
{
    Task<(bool Ok, string Message, int OrderId)> CreateOrderAsync(string userId, CheckoutViewModel model,
        IReadOnlyList<CartItem> cartItems, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetUserOrdersAsync(string userId, CancellationToken ct = default);
    Task<Order?> GetUserOrderAsync(string userId, int orderId, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetAllOrdersAsync(CancellationToken ct = default);
    Task<bool> UpdateStatusAsync(int orderId, OrderStatus status, CancellationToken ct = default);
}

/// <summary>
/// Оформление заказа одной транзакцией: проверяем остатки, создаём заказ,
/// списываем товар со склада. Цена берётся из БД, а не из корзины клиента.
/// </summary>
public class OrderService : IOrderService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<OrderService> _logger;

    public OrderService(ApplicationDbContext db, ILogger<OrderService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<(bool Ok, string Message, int OrderId)> CreateOrderAsync(string userId,
        CheckoutViewModel model, IReadOnlyList<CartItem> cartItems, CancellationToken ct = default)
    {
        if (cartItems.Count == 0)
        {
            return (false, "Корзина пуста", 0);
        }

        var ids = cartItems.Select(i => i.ProductId).ToList();
        var products = await _db.Products
            .Where(p => ids.Contains(p.Id) && p.IsActive)
            .ToListAsync(ct);

        foreach (var item in cartItems)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product is null)
            {
                return (false, $"Товар «{item.Name}» больше не продаётся", 0);
            }

            if (product.Stock < item.Quantity)
            {
                return (false, $"Не хватает товара «{product.Name}»: осталось {product.Stock} шт.", 0);
            }
        }

        var order = new Order
        {
            UserId = userId,
            CustomerName = model.CustomerName.Trim(),
            Phone = model.Phone.Trim(),
            Address = model.Address.Trim(),
            Comment = model.Comment?.Trim(),
            Status = OrderStatus.New,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var item in cartItems)
        {
            var product = products.First(p => p.Id == item.ProductId);
            product.Stock -= item.Quantity;
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = item.Quantity
            });
        }

        order.Total = order.Items.Sum(i => i.UnitPrice * i.Quantity);

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Создан заказ #{OrderId} пользователя {UserId} на сумму {Total}", order.Id, userId, order.Total);
        return (true, "Заказ оформлен", order.Id);
    }

    public async Task<IReadOnlyList<Order>> GetUserOrdersAsync(string userId, CancellationToken ct = default)
    {
        return await _db.Orders.AsNoTracking()
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Include(o => o.Items)
            .AsSplitQuery()
            .ToListAsync(ct);
    }

    public Task<Order?> GetUserOrderAsync(string userId, int orderId, CancellationToken ct = default)
    {
        // Фильтр по UserId защищает от просмотра чужих заказов подбором id (IDOR).
        return _db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct);
    }

    public async Task<IReadOnlyList<Order>> GetAllOrdersAsync(CancellationToken ct = default)
    {
        return await _db.Orders.AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .Include(o => o.Items)
            .Include(o => o.User)
            .AsSplitQuery()
            .ToListAsync(ct);
    }

    public async Task<bool> UpdateStatusAsync(int orderId, OrderStatus status, CancellationToken ct = default)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null)
        {
            return false;
        }

        order.Status = status;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Статус заказа #{OrderId} изменён на {Status}", orderId, status);
        return true;
    }
}
