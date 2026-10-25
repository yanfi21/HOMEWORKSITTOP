using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shop.Web.Data;
using Shop.Web.Models;

namespace Shop.Web.Areas.Admin.Controllers;

public class DashboardController : AdminBaseController
{
    private readonly ApplicationDbContext _db;

    public DashboardController(ApplicationDbContext db) => _db = db;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.ProductsCount = await _db.Products.CountAsync(ct);
        ViewBag.CategoriesCount = await _db.Categories.CountAsync(ct);
        ViewBag.OrdersCount = await _db.Orders.CountAsync(ct);
        ViewBag.NewOrdersCount = await _db.Orders.CountAsync(o => o.Status == OrderStatus.New, ct);
        ViewBag.Revenue = await _db.Orders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.Total, ct) ?? 0m;
        ViewBag.RecentOrders = await _db.Orders.AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .Take(5)
            .ToListAsync(ct);
        return View();
    }
}
