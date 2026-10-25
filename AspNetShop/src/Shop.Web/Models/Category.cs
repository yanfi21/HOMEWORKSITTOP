using System.ComponentModel.DataAnnotations;

namespace Shop.Web.Models;

public class Category
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Название категории обязательно")]
    [StringLength(80)]
    [Display(Name = "Название")]
    public string Name { get; set; } = string.Empty;

    [StringLength(400)]
    [Display(Name = "Описание")]
    public string? Description { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
