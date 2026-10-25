using Microsoft.AspNetCore.Mvc;
using Shop.Web.Services;
using Shop.Web.ViewModels;

namespace Shop.Web.Controllers;

public class ProductsController : Controller
{
    private readonly IProductService _products;

    public ProductsController(IProductService products) => _products = products;

    /// <summary>
    /// Страница каталога. Первый рендер — серверный (быстрый First Paint, SEO),
    /// дальше фильтры/поиск/пагинация работают через AJAX (см. действие Grid и catalog.js).
    /// </summary>
    public async Task<IActionResult> Index([FromQuery] ProductQuery query, CancellationToken ct)
    {
        var model = new CatalogViewModel
        {
            Query = query,
            Result = await _products.GetProductsAsync(query, ct),
            Categories = await _products.GetCategoriesAsync(ct)
        };
        return View(model);
    }

    /// <summary>AJAX-эндпоинт: возвращает только HTML-сетку товаров + пагинацию (PartialView).</summary>
    [HttpGet]
    public async Task<IActionResult> Grid([FromQuery] ProductQuery query, CancellationToken ct)
    {
        var result = await _products.GetProductsAsync(query, ct);
        return PartialView("_ProductGrid", new CatalogViewModel { Query = query, Result = result });
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var product = await _products.GetByIdAsync(id, ct);
        if (product is null)
        {
            return NotFound();
        }

        return View(product);
    }
}
