using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Shop.Web.Services;
using Shop.Web.ViewModels;

namespace Shop.Web.Controllers;

public class HomeController : Controller
{
    private readonly IProductService _products;
    private readonly ILogger<HomeController> _logger;

    public HomeController(IProductService products, ILogger<HomeController> logger)
    {
        _products = products;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = new HomeViewModel
        {
            Featured = await _products.GetFeaturedAsync(8, ct),
            Categories = await _products.GetCategoriesAsync(ct)
        };
        return View(model);
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? statusCode)
    {
        if (statusCode is 404)
        {
            _logger.LogWarning("Страница не найдена (404): {Path}", Request.Path);
        }

        ViewBag.StatusCode = statusCode;
        ViewBag.RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View();
    }
}
