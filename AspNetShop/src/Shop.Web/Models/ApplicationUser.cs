using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Shop.Web.Models;

/// <summary>
/// Пользователь магазина. Расширяет IdentityUser полями профиля.
/// Пароли никогда не хранятся в открытом виде — Identity хэширует их PBKDF2.
/// </summary>
public class ApplicationUser : IdentityUser
{
    [StringLength(100)]
    public string? FullName { get; set; }

    [StringLength(300)]
    public string? Address { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
