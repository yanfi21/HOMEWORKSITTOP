using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Shop.Web.Services;
using Shop.Web.ViewModels;

namespace Shop.Web.Controllers.Api;

/// <summary>
/// JSON API каталога для AJAX: живой поиск, подсказки автодополнения, быстрый просмотр товара.
/// Ответы GET кэшируются OutputCache (30 сек) — повторные запросы не ходят в БД.
/// </summary>
[ApiController]
[Route("api/products")]
[EnableRateLimiting("api")]
public class ProductsApiController : ControllerBase
{
    private readonly IProductService _products;

    public ProductsApiController(IProductService products) => _products = products;

    [HttpGet]
    [OutputCache(PolicyName = "CatalogApi")]
    public async Task<IActionResult> Get([FromQuery] ProductQuery query, CancellationToken ct)
    {
        var result = await _products.GetProductsAsync(query, ct);
        return Ok(new
        {
            items = result.Items.Select(p => new
            {
                p.Id,
                p.Name,
                p.Price,
                p.OldPrice,
                p.Stock,
                p.ImageUrl,
                p.CategoryName
            }),
            result.TotalCount,
            result.Page,
            result.PageSize,
            result.TotalPages
        });
    }

    [HttpGet("suggestions")]
    [OutputCache(PolicyName = "Suggestions")]
    public async Task<IActionResult> Suggestions([FromQuery] string q, CancellationToken ct)
    {
        var items = await _products.GetSuggestionsAsync(q ?? string.Empty, 6, ct);
        return Ok(items.Select(p => new { p.Id, p.Name, p.Price, p.ImageUrl, p.CategoryName }));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var product = await _products.GetByIdAsync(id, ct);
        if (product is null)
        {
            return NotFound(new { message = "Товар не найден" });
        }

        return Ok(new
        {
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            product.OldPrice,
            product.Stock,
            product.ImageUrl,
            category = product.Category?.Name
        });
    }
}
