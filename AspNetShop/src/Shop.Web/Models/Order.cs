using System.ComponentModel.DataAnnotations;

namespace Shop.Web.Models;

public enum OrderStatus
{
    [Display(Name = "Новый")]
    New = 0,

    [Display(Name = "В обработке")]
    Processing = 1,

    [Display(Name = "Отправлен")]
    Shipped = 2,

    [Display(Name = "Выполнен")]
    Completed = 3,

    [Display(Name = "Отменён")]
    Cancelled = 4
}

public class Order
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    [Required, StringLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string Phone { get; set; } = string.Empty;

    [Required, StringLength(300)]
    public string Address { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Comment { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.New;

    public decimal Total { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}

public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>Название и цена фиксируются на момент заказа (история не меняется при правках каталога).</summary>
    [Required, StringLength(140)]
    public string ProductName { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;
}
