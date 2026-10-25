using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Shop.Web.Data;
using Shop.Web.Models;
using Shop.Web.ViewModels;

namespace Shop.Web.Services;

public interface IProductService
{
    Task<PagedResult<ProductListItem>> GetProductsAsync(ProductQuery query, CancellationToken ct = default);
    Task<Product?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ProductListItem>> GetFeaturedAsync(int count = 8, CancellationToken ct = default);
    Task<IReadOnlyList<ProductListItem>> GetSuggestionsAsync(string term, int count = 6, CancellationToken ct = default);
    void InvalidateCatalogCache();
}

/// <summary>
/// Каталог товаров.
/// Скорость: AsNoTracking (нет трекинга изменений на чтение), проекции Select в лёгкие DTO,
/// серверная пагинация, индексы в БД, IMemoryCache для категорий и "хитов" главной страницы.
/// </summary>
public class ProductService : IProductService
{
    public const string CategoriesCacheKey = "catalog:categories";
    public const string FeaturedCacheKey = "catalog:featured";

    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ProductService> _logger;
    private readonly int _categoriesMinutes;
    private readonly int _featuredMinutes;

    public ProductService(ApplicationDbContext db, IMemoryCache cache,
        ILogger<ProductService> logger, IConfiguration config)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
        _categoriesMinutes = config.GetValue("Cache:CategoriesMinutes", 10);
        _featuredMinutes = config.GetValue("Cache:FeaturedMinutes", 5);
    }

    public async Task<PagedResult<ProductListItem>> GetProductsAsync(ProductQuery query, CancellationToken ct = default)
    {
        query.Page = Math.Max(1, query.Page);
        query.PageSize = Math.Clamp(query.PageSize, 1, 48);

        var q = _db.Products.AsNoTracking().Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(p => p.Name.Contains(term) || (p.Description != null && p.Description.Contains(term)));
        }

        if (query.CategoryId is > 0)
        {
            q = q.Where(p => p.CategoryId == query.CategoryId.Value);
        }

        if (query.MinPrice is > 0)
        {
            q = q.Where(p => p.Price >= query.MinPrice.Value);
        }

        if (query.MaxPrice is > 0)
        {
            q = q.Where(p => p.Price <= query.MaxPrice.Value);
        }

        q = query.Sort switch
        {
            "price-asc" => q.OrderBy(p => p.Price),
            "price-desc" => q.OrderByDescending(p => p.Price),
            "name" => q.OrderBy(p => p.Name),
            "popular" => q.OrderByDescending(p => p.IsFeatured).ThenByDescending(p => p.CreatedAt),
            _ => q.OrderByDescending(p => p.CreatedAt)
        };

        var total = await q.CountAsync(ct);

        var items = await q
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => new ProductListItem
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                OldPrice = p.OldPrice,
                Stock = p.Stock,
                ImageUrl = p.ImageUrl,
                CategoryName = p.Category != null ? p.Category.Name : string.Empty,
                IsFeatured = p.IsFeatured
            })
            .ToListAsync(ct);

        _logger.LogDebug("Каталог: поиск={Search}, категория={CategoryId}, найдено={Total}",
            query.Search, query.CategoryId, total);

        return new PagedResult<ProductListItem>
        {
            Items = items,
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return _db.Products.AsNoTracking()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive, ct);
    }

    public async Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CategoriesCacheKey, out IReadOnlyList<Category>? cached) && cached is not null)
        {
            _logger.LogDebug("Категории отданы из IMemoryCache");
            return cached;
        }

        var categories = await _db.Categories.AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

        _cache.Set(CategoriesCacheKey, (IReadOnlyList<Category>)categories,
            TimeSpan.FromMinutes(_categoriesMinutes));
        return categories;
    }

    public async Task<IReadOnlyList<ProductListItem>> GetFeaturedAsync(int count = 8, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(FeaturedCacheKey, out IReadOnlyList<ProductListItem>? cached) && cached is not null)
        {
            return cached;
        }

        var items = await _db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.IsFeatured)
            .OrderByDescending(p => p.CreatedAt)
            .Take(count)
            .Select(p => new ProductListItem
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                OldPrice = p.OldPrice,
                Stock = p.Stock,
                ImageUrl = p.ImageUrl,
                CategoryName = p.Category != null ? p.Category.Name : string.Empty,
                IsFeatured = p.IsFeatured
            })
            .ToListAsync(ct);

        _cache.Set(FeaturedCacheKey, (IReadOnlyList<ProductListItem>)items, TimeSpan.FromMinutes(_featuredMinutes));
        return items;
    }

    public async Task<IReadOnlyList<ProductListItem>> GetSuggestionsAsync(string term, int count = 6, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(term) || term.Trim().Length < 2)
        {
            return Array.Empty<ProductListItem>();
        }

        var t = term.Trim();
        var lower = t.ToLowerInvariant();
        var capitalized = lower.Length == 0 ? lower : char.ToUpperInvariant(lower[0]) + lower[1..];
        return await _db.Products.AsNoTracking()
            .Where(p => p.IsActive && (p.Name.Contains(t) || p.Name.Contains(capitalized) || p.Name.ToLower().Contains(lower)))
            .OrderBy(p => p.Name)
            .Take(count)
            .Select(p => new ProductListItem
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                ImageUrl = p.ImageUrl,
                CategoryName = p.Category != null ? p.Category.Name : string.Empty
            })
            .ToListAsync(ct);
    }

    /// <summary>Вызывается из админки после CRUD, чтобы витрина сразу показала актуальные данные.</summary>
    public void InvalidateCatalogCache()
    {
        _cache.Remove(CategoriesCacheKey);
        _cache.Remove(FeaturedCacheKey);
        _logger.LogInformation("Кэш каталога сброшен");
    }
}
