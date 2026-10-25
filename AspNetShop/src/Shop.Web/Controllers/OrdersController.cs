using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Web.Services;

namespace Shop.Web.Controllers;

[Authorize]
public class OrdersController : Controller
{
    private readonly IOrderService _orders;

    public OrdersController(IOrderService orders) => _orders = orders;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var orders = await _orders.GetUserOrdersAsync(userId, ct);
        return View(orders);
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var order = await _orders.GetUserOrderAsync(userId, id, ct);
        if (order is null)
        {
            return NotFound(); // чужой заказ выглядит как несуществующий
        }

        return View(order);
    }
}
