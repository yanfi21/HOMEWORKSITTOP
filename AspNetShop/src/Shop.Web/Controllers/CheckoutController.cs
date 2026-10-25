using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Shop.Web.Models;
using Shop.Web.Services;
using Shop.Web.ViewModels;

namespace Shop.Web.Controllers;

[Authorize] // оформить заказ может только авторизованный пользователь
public class CheckoutController : Controller
{
    private readonly ICartService _cart;
    private readonly IOrderService _orders;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ILogger<CheckoutController> _logger;

    public CheckoutController(ICartService cart, IOrderService orders,
        UserManager<ApplicationUser> users, ILogger<CheckoutController> logger)
    {
        _cart = cart;
        _orders = orders;
        _users = users;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var items = _cart.GetItems();
        if (items.Count == 0)
        {
            TempData["Toast"] = "Корзина пуста — сначала добавьте товар";
            return RedirectToAction("Index", "Products");
        }

        var user = await _users.GetUserAsync(User);
        var model = new CheckoutViewModel
        {
            CustomerName = user?.FullName ?? string.Empty,
            Phone = user?.PhoneNumber ?? string.Empty,
            Address = user?.Address ?? string.Empty,
            Items = items,
            Total = _cart.Total
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(CheckoutViewModel model, CancellationToken ct)
    {
        var items = _cart.GetItems();
        model.Items = items;
        model.Total = _cart.Total;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var (ok, message, orderId) = await _orders.CreateOrderAsync(userId, model, items, ct);

        if (!ok)
        {
            _logger.LogWarning("Заказ пользователя {UserId} отклонён: {Message}", userId, message);
            ModelState.AddModelError(string.Empty, message);
            return View(model);
        }

        _cart.Clear();
        return RedirectToAction(nameof(Success), new { id = orderId });
    }

    [HttpGet]
    public IActionResult Success(int id)
    {
        ViewBag.OrderId = id;
        return View();
    }
}
