using Microsoft.AspNetCore.Mvc;
using Shop.Web.Services;

namespace Shop.Web.Controllers;

public class CartController : Controller
{
    private readonly ICartService _cart;

    public CartController(ICartService cart) => _cart = cart;

    /// <summary>Страница корзины. Изменения количества — через AJAX API (/api/cart), см. cart.js.</summary>
    public IActionResult Index()
    {
        return View(_cart.GetItems());
    }
}
