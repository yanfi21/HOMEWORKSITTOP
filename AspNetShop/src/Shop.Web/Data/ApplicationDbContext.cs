using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Shop.Web.Models;

namespace Shop.Web.Data;

/// <summary>
/// Контекст EF Core. Наследуется от IdentityDbContext — таблицы пользователей/ролей
/// создаются вместе с таблицами магазина в одной БД.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Product>(e =>
        {
            // Индексы под самые частые запросы каталога: фильтр по категории, поиск по имени, сортировка по цене.
            e.HasIndex(p => p.CategoryId);
            e.HasIndex(p => p.Name);
            e.HasIndex(p => p.Price);
            e.HasIndex(p => new { p.IsActive, p.IsFeatured });
            e.HasOne(p => p.Category)
             .WithMany(c => c.Products)
             .HasForeignKey(p => p.CategoryId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Category>(e =>
        {
            e.HasIndex(c => c.Name).IsUnique();
        });

        builder.Entity<Order>(e =>
        {
            e.HasIndex(o => o.UserId);
            e.HasIndex(o => o.CreatedAt);
            e.HasOne(o => o.User)
             .WithMany(u => u.Orders)
             .HasForeignKey(o => o.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<OrderItem>(e =>
        {
            e.HasOne(i => i.Order)
             .WithMany(o => o.Items)
             .HasForeignKey(i => i.OrderId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.Product)
             .WithMany()
             .HasForeignKey(i => i.ProductId)
             .OnDelete(DeleteBehavior.Restrict);
            e.Ignore(i => i.LineTotal);
        });
    }
}
