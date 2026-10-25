using System.ComponentModel.DataAnnotations;

namespace Shop.Web.Models;

public class Product
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Название товара обязательно")]
    [StringLength(140)]
    [Display(Name = "Название")]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    [Display(Name = "Описание")]
    public string? Description { get; set; }

    [Range(typeof(decimal), "0.01", "100000000", ParseLimitsInInvariantCulture = true, ErrorMessage = "Цена должна быть больше нуля")]
    [Display(Name = "Цена, ₽")]
    public decimal Price { get; set; }

    [Range(typeof(decimal), "0", "100000000", ParseLimitsInInvariantCulture = true)]
    [Display(Name = "Старая цена, ₽")]
    public decimal? OldPrice { get; set; }

    [Range(0, 100000, ErrorMessage = "Количество не может быть отрицательным")]
    [Display(Name = "Остаток на складе")]
    public int Stock { get; set; }

    [StringLength(300)]
    [Display(Name = "Картинка")]
    public string ImageUrl { get; set; } = "/images/products/placeholder.svg";

    [Display(Name = "Показывать на главной")]
    public bool IsFeatured { get; set; }

    [Display(Name = "Активен")]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [Display(Name = "Категория")]
    public int CategoryId { get; set; }

    public Category? Category { get; set; }
}
