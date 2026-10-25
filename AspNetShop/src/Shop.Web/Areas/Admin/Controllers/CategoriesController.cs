using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Shop.Web.Data;
using Shop.Web.Models;
using Shop.Web.Services;

namespace Shop.Web.Areas.Admin.Controllers;

public class CategoriesController : AdminBaseController
{
    private readonly ApplicationDbContext _db;
    private readonly IProductService _productService;
    private readonly IOutputCacheStore _outputCache;
    private readonly ILogger<CategoriesController> _logger;

    public CategoriesController(ApplicationDbContext db, IProductService productService,
        IOutputCacheStore outputCache, ILogger<CategoriesController> logger)
    {
        _db = db;
        _productService = productService;
        _outputCache = outputCache;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var categories = await _db.Categories.AsNoTracking()
            .Include(c => c.Products)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
        return View(categories);
    }

    [HttpGet]
    public IActionResult Create() => View(new Category());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Category model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        _db.Categories.Add(new Category { Name = model.Name.Trim(), Description = model.Description?.Trim() });
        await _db.SaveChangesAsync(ct);
        await InvalidateCachesAsync(ct);
        _logger.LogInformation("Админ создал категорию «{Name}»", model.Name);
        TempData["Toast"] = "Категория добавлена";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        return category is null ? NotFound() : View(category);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Category model, CancellationToken ct)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
        {
            return NotFound();
        }

        category.Name = model.Name.Trim();
        category.Description = model.Description?.Trim();
        await _db.SaveChangesAsync(ct);
        await InvalidateCachesAsync(ct);
        TempData["Toast"] = "Категория сохранена";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var category = await _db.Categories.Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
        {
            return NotFound();
        }

        if (category.Products.Any())
        {
            TempData["Toast"] = "Нельзя удалить категорию с товарами — сначала перенесите товары";
            return RedirectToAction(nameof(Index));
        }

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync(ct);
        await InvalidateCachesAsync(ct);
        _logger.LogInformation("Админ удалил категорию #{Id}", id);
        TempData["Toast"] = "Категория удалена";
        return RedirectToAction(nameof(Index));
    }

    private async Task InvalidateCachesAsync(CancellationToken ct)
    {
        _productService.InvalidateCatalogCache();
        await _outputCache.EvictByTagAsync("catalog", ct);
    }
}
