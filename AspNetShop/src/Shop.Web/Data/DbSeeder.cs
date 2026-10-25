using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shop.Web.Models;

namespace Shop.Web.Data;

/// <summary>
/// Первичное наполнение БД: роли, администратор, демо-пользователь, категории и товары.
/// Для учебного проекта вместо миграций используется EnsureCreated (см. README — раздел про миграции).
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in new[] { "Admin", "Customer" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                logger.LogInformation("Создана роль {Role}", role);
            }
        }

        var adminEmail = config["Seed:AdminEmail"] ?? "admin@shop.local";
        var adminPassword = config["Seed:AdminPassword"] ?? "Admin123!";
        await EnsureUserAsync(userManager, adminEmail, adminPassword, "Администратор", "Admin", logger);

        var demoEmail = config["Seed:DemoEmail"] ?? "user@shop.local";
        var demoPassword = config["Seed:DemoPassword"] ?? "User123!";
        await EnsureUserAsync(userManager, demoEmail, demoPassword, "Демо Пользователь", "Customer", logger);

        if (await db.Categories.AnyAsync())
        {
            logger.LogInformation("Каталог уже заполнен — seed пропущен");
            return;
        }

        var categories = new[]
        {
            new Category { Name = "Смартфоны", Description = "Флагманы и доступные смартфоны" },
            new Category { Name = "Ноутбуки", Description = "Для работы, учёбы и игр" },
            new Category { Name = "Аудио", Description = "Наушники, колонки и гарнитуры" },
            new Category { Name = "Мониторы", Description = "Игровые и офисные мониторы" },
            new Category { Name = "Аксессуары", Description = "Мыши, клавиатуры, кабели и чехлы" }
        };
        db.Categories.AddRange(categories);
        await db.SaveChangesAsync();

        var phones = categories[0].Id;
        var laptops = categories[1].Id;
        var audio = categories[2].Id;
        var monitors = categories[3].Id;
        var accessories = categories[4].Id;

        var products = new List<Product>
        {
            P("Смартфон Aurora X5 256 ГБ", phones, 54990, 64990, 25, true, "phone1", "6.7\" AMOLED 120 Гц, камера 108 Мп, батарея 5000 мА·ч, быстрая зарядка 67 Вт."),
            P("Смартфон Aurora X5 Lite 128 ГБ", phones, 29990, null, 40, false, "phone2", "6.5\" IPS 90 Гц, камера 50 Мп, NFC, батарея 5000 мА·ч."),
            P("Смартфон Vega Note 12 Pro", phones, 79990, 89990, 12, true, "phone1", "Флагман: перископический зум, титановый корпус, 16 ГБ ОЗУ, 512 ГБ памяти."),
            P("Смартфон Vega A34", phones, 19990, 24990, 60, false, "phone2", "Доступный смартфон с ярким экраном и двойной камерой."),
            P("Ноутбук Nimbus Pro 14", laptops, 119990, 134990, 10, true, "laptop1", "14\" 2.8K, 16 ГБ ОЗУ, SSD 1 ТБ, алюминиевый корпус 1.29 кг, до 16 часов работы."),
            P("Ноутбук Nimbus Air 13", laptops, 74990, null, 18, false, "laptop2", "Лёгкий ультрабук для учёбы и работы: SSD 512 ГБ, подсветка клавиатуры."),
            P("Игровой ноутбук Titan GX16", laptops, 169990, 189990, 7, true, "laptop1", "16\" 240 Гц, дискретная видеокарта 12 ГБ, 32 ГБ ОЗУ, охлаждение с двумя вентиляторами."),
            P("Ноутбук OfficeBook 15", laptops, 44990, null, 30, false, "laptop2", "Надёжный офисный ноутбук: 15.6\" Full HD, SSD 256 ГБ, Windows не требуется для демо."),
            P("Наушники Pulse Sound Max", audio, 14990, 19990, 45, true, "audio1", "Полноразмерные, активное шумоподавление, 60 часов работы, Bluetooth 5.3."),
            P("TWS-наушники Pulse Buds Air", audio, 5990, 7990, 120, true, "audio2", "Компактные вкладыши, кейс с беспроводной зарядкой, защита IPX5."),
            P("Портативная колонка BoomBox Go", audio, 8990, null, 35, false, "audio1", "30 Вт, глубокий бас, влагозащита IP67, до 20 часов музыки."),
            P("Игровая гарнитура Strike H7", audio, 9990, 12990, 28, false, "audio2", "Объёмный звук 7.1, съёмный микрофон, амбушюры с эффектом памяти."),
            P("Монитор ViewPro 27\" QHD 165 Гц", monitors, 32990, 39990, 15, true, "monitor1", "IPS 2560×1440, 1 мс, HDR400, регулировка по высоте и повороту."),
            P("Монитор ViewPro 24\" Full HD", monitors, 13990, null, 42, false, "monitor2", "IPS 75 Гц, тонкие рамки, режим защиты зрения Low Blue Light."),
            P("Ультраширокий монитор Panorama 34\"", monitors, 54990, 62990, 8, false, "monitor1", "3440×1440, изогнутый экран 1500R, USB-C с зарядкой 65 Вт."),
            P("Монитор 4K Creator 32\"", monitors, 69990, null, 6, true, "monitor2", "4K IPS, 98% DCI-P3, заводская калибровка — для фото и видео."),
            P("Беспроводная мышь Ergo M720", accessories, 3490, 4490, 200, false, "acc1", "Тихие клики, 4000 DPI, подключение к трём устройствам."),
            P("Механическая клавиатура KeyForge K87", accessories, 7990, 9990, 55, true, "acc2", "Горячая замена переключателей, RGB-подсветка, алюминиевая рама."),
            P("USB-C док-станция HubPro 10-в-1", accessories, 6490, null, 70, false, "acc1", "HDMI 4K, 3×USB-A, Ethernet, SD/microSD, сквозная зарядка 100 Вт."),
            P("Рюкзак для ноутбука UrbanPack 16\"", accessories, 4990, 6490, 90, false, "acc2", "Водонепроницаемая ткань, отделение для ноутбука, USB-порт для зарядки."),
            P("Внешний SSD SwiftDrive 1 ТБ", accessories, 10990, 13990, 65, true, "acc1", "Скорость до 1050 МБ/с, металлический корпус, USB-C 3.2 Gen2."),
            P("Веб-камера StreamCam 4K", accessories, 8990, null, 22, false, "acc2", "4K 30fps / 1080p 60fps, автофокус, шторка приватности."),
            P("Зарядное устройство GaN 65 Вт", accessories, 2990, 3990, 150, false, "acc1", "2×USB-C + USB-A, заряжает ноутбук и телефон одновременно."),
            P("Смартфон Vega Fold Z", phones, 129990, 149990, 4, false, "phone1", "Складной экран 7.6\", многозадачность на три окна, 512 ГБ памяти.")
        };

        db.Products.AddRange(products);
        await db.SaveChangesAsync();
        logger.LogInformation("Seed завершён: {Categories} категорий, {Products} товаров", categories.Length, products.Count);
    }

    private static Product P(string name, int categoryId, decimal price, decimal? oldPrice, int stock,
        bool featured, string img, string description) => new()
    {
        Name = name,
        CategoryId = categoryId,
        Price = price,
        OldPrice = oldPrice,
        Stock = stock,
        IsFeatured = featured,
        IsActive = true,
        ImageUrl = $"/images/products/{img}.svg",
        Description = description,
        CreatedAt = DateTime.UtcNow
    };

    private static async Task EnsureUserAsync(UserManager<ApplicationUser> userManager, string email,
        string password, string fullName, string role, ILogger logger)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName
            };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                logger.LogError("Не удалось создать пользователя {Email}: {Errors}", email,
                    string.Join("; ", result.Errors.Select(e => e.Description)));
                return;
            }

            logger.LogInformation("Создан пользователь {Email}", email);
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }
}
