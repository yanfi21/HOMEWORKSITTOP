using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Shop.Web.Data;
using Shop.Web.Models;
using Shop.Web.Services;

namespace Shop.Web.Areas.Admin.Controllers;

public class ProductsController : AdminBaseController
{
    // SVG в загрузки не принимаем: это XML, внутрь можно встроить скрипт (stored XSS).
    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private const long MaxImageBytes = 2 * 1024 * 1024; // 2 МБ

    private readonly ApplicationDbContext _db;
    private readonly IProductService _productService;
    private readonly IOutputCacheStore _outputCache;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(ApplicationDbContext db, IProductService productService,
        IOutputCacheStore outputCache, IWebHostEnvironment env, ILogger<ProductsController> logger)
    {
        _db = db;
        _productService = productService;
        _outputCache = outputCache;
        _env = env;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var products = await _db.Products.AsNoTracking()
            .Include(p => p.Category)
            .OrderBy(p => p.Category!.Name)
            .ThenBy(p => p.Name)
            .ToListAsync(ct);
        return View(products);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        await PopulateCategoriesAsync(ct);
        return View(new Product { IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Product model, IFormFile? imageFile, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCategoriesAsync(ct);
            return View(model);
        }

        model.ImageUrl = await SaveImageAsync(imageFile, model.ImageUrl, ct);
        model.CreatedAt = DateTime.UtcNow;
        _db.Products.Add(model);
        await _db.SaveChangesAsync(ct);
        await InvalidateCachesAsync(ct);

        _logger.LogInformation("Админ создал товар #{Id} «{Name}»", model.Id, model.Name);
        TempData["Toast"] = "Товар добавлен";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
        {
            return NotFound();
        }

        await PopulateCategoriesAsync(ct);
        return View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Product model, IFormFile? imageFile, CancellationToken ct)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            await PopulateCategoriesAsync(ct);
            return View(model);
        }

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
        {
            return NotFound();
        }

        // Копируем только разрешённые поля — защита от overposting (подмены лишних полей формы).
        product.Name = model.Name;
        product.Description = model.Description;
        product.Price = model.Price;
        product.OldPrice = model.OldPrice;
        product.Stock = model.Stock;
        product.CategoryId = model.CategoryId;
        product.IsFeatured = model.IsFeatured;
        product.IsActive = model.IsActive;
        product.ImageUrl = await SaveImageAsync(imageFile, model.ImageUrl, ct);

        await _db.SaveChangesAsync(ct);
        await InvalidateCachesAsync(ct);
        _logger.LogInformation("Админ изменил товар #{Id} «{Name}»", product.Id, product.Name);
        TempData["Toast"] = "Товар сохранён";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (product is null)
        {
            return NotFound();
        }

        try
        {
            _db.Products.Remove(product);
            await _db.SaveChangesAsync(ct);
            TempData["Toast"] = "Товар удалён";
        }
        catch (DbUpdateException)
        {
            // Товар есть в заказах — вместо удаления просто скрываем с витрины.
            product.IsActive = false;
            await _db.SaveChangesAsync(ct);
            TempData["Toast"] = "Товар встречается в заказах, поэтому скрыт с витрины";
        }

        await InvalidateCachesAsync(ct);
        _logger.LogInformation("Админ удалил/скрыл товар #{Id}", id);
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Безопасная загрузка картинки: проверяем расширение и размер, имя файла генерируем сами (Guid),
    /// чтобы исключить path traversal и загрузку исполняемых файлов.
    /// </summary>
    private async Task<string> SaveImageAsync(IFormFile? file, string? fallbackUrl, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return string.IsNullOrWhiteSpace(fallbackUrl) ? "/images/products/placeholder.svg" : fallbackUrl;
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext) || file.Length > MaxImageBytes)
        {
            _logger.LogWarning("Отклонена загрузка файла {FileName} ({Size} байт)", file.FileName, file.Length);
            return string.IsNullOrWhiteSpace(fallbackUrl) ? "/images/products/placeholder.svg" : fallbackUrl;
        }

        var uploadsDir = Path.Combine(_env.WebRootPath, "images", "uploads");
        Directory.CreateDirectory(uploadsDir);
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(uploadsDir, fileName);

        await using var stream = System.IO.File.Create(path);
        await file.CopyToAsync(stream, ct);
        return $"/images/uploads/{fileName}";
    }

    private async Task PopulateCategoriesAsync(CancellationToken ct)
    {
        ViewBag.Categories = new SelectList(
            await _db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct), "Id", "Name");
    }

    private async Task InvalidateCachesAsync(CancellationToken ct)
    {
        _productService.InvalidateCatalogCache();
        await _outputCache.EvictByTagAsync("catalog", ct);
    }
}
