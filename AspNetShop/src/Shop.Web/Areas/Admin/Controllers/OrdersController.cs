using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shop.Web.Data;
using Shop.Web.Models;
using Shop.Web.Services;

namespace Shop.Web.Areas.Admin.Controllers;

public class OrdersController : AdminBaseController
{
    private readonly IOrderService _orders;
    private readonly ApplicationDbContext _db;

    public OrdersController(IOrderService orders, ApplicationDbContext db)
    {
        _orders = orders;
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        return View(await _orders.GetAllOrdersAsync(ct));
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var order = await _db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
        return order is null ? NotFound() : View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatus status, CancellationToken ct)
    {
        var ok = await _orders.UpdateStatusAsync(id, status, ct);
        TempData["Toast"] = ok ? "Статус заказа обновлён" : "Заказ не найден";
        return RedirectToAction(nameof(Index));
    }
}
