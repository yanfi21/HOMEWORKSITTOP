using System.ComponentModel.DataAnnotations;
using Shop.Web.Models;

namespace Shop.Web.ViewModels;

/// <summary>Параметры запроса каталога (приходят и из формы фильтров, и из AJAX-запросов).</summary>
public class ProductQuery
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string Sort { get; set; } = "new";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 9;
}

/// <summary>Страница результата с метаданными для пагинации.</summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}

/// <summary>Лёгкая проекция товара для списков: в БД не тянем лишние поля (Select в EF Core).</summary>
public class ProductListItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? OldPrice { get; set; }
    public int Stock { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public bool IsFeatured { get; set; }
}

public class CatalogViewModel
{
    public ProductQuery Query { get; set; } = new();
    public PagedResult<ProductListItem> Result { get; set; } = new();
    public IReadOnlyList<Category> Categories { get; set; } = Array.Empty<Category>();
}

public class HomeViewModel
{
    public IReadOnlyList<ProductListItem> Featured { get; set; } = Array.Empty<ProductListItem>();
    public IReadOnlyList<Category> Categories { get; set; } = Array.Empty<Category>();
}

public class CheckoutViewModel
{
    [Required(ErrorMessage = "Укажите имя получателя")]
    [StringLength(100)]
    [Display(Name = "Имя получателя")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите телефон")]
    [Phone(ErrorMessage = "Некорректный телефон")]
    [Display(Name = "Телефон")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите адрес доставки")]
    [StringLength(300)]
    [Display(Name = "Адрес доставки")]
    public string Address { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Комментарий к заказу")]
    public string? Comment { get; set; }

    public IReadOnlyList<CartItem> Items { get; set; } = Array.Empty<CartItem>();
    public decimal Total { get; set; }
}
