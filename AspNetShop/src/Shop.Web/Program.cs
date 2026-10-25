using System.IO.Compression;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Shop.Web.Data;
using Shop.Web.Health;
using Shop.Web.Logging;
using Shop.Web.Middleware;
using Shop.Web.Models;
using Shop.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- Логирование (встроенная система ILogger) ----------
// Консоль + Debug идут из коробки, добавляем собственный файловый провайдер: logs/app-*.log
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddProvider(new FileLoggerProvider(Path.Combine(builder.Environment.ContentRootPath, "logs")));

// ---------- База данных (EF Core + SQLite, пул контекстов для скорости) ----------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? "Data Source=shop.db";
builder.Services.AddDbContextPool<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// ---------- Identity: пользователи, роли, хэширование паролей, блокировки ----------
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.LogoutPath = "/Account/Logout";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;                 // кука недоступна из JS — защита от кражи через XSS
    options.Cookie.SameSite = SameSiteMode.Lax;     // смягчение CSRF
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

// Antiforgery: токен для обычных форм и для AJAX (заголовок RequestVerificationToken)
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

// ---------- MVC + JSON ----------
builder.Services.AddControllersWithViews();

// ---------- Кэширование ----------
// 1) IMemoryCache — категории и хиты главной (ProductService)
// 2) IDistributedCache (в памяти; в проде меняется на Redis одной строкой) — бэкенд сессий корзины
// 3) OutputCache — кэш ответов API каталога на уровне middleware
// 4) ResponseCaching — HTTP-кэширование по Cache-Control
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("CatalogApi", policy => policy
        .Expire(TimeSpan.FromSeconds(30))
        .SetVaryByQuery("search", "categoryId", "minPrice", "maxPrice", "sort", "page", "pageSize")
        .Tag("catalog"));
    options.AddPolicy("Suggestions", policy => policy
        .Expire(TimeSpan.FromSeconds(15))
        .SetVaryByQuery("q")
        .Tag("catalog"));
});
builder.Services.AddResponseCaching();

// ---------- Сессия (корзина) ----------
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(7);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// ---------- Сжатие ответов: Brotli + Gzip (скорость загрузки) ----------
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.SmallestSize);

// ---------- Ограничение частоты запросов (защита от брутфорса и спама) ----------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("login", o =>
    {
        o.PermitLimit = 10;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0;
    });
    options.AddSlidingWindowLimiter("api", o =>
    {
        o.PermitLimit = 120;
        o.Window = TimeSpan.FromMinutes(1);
        o.SegmentsPerWindow = 4;
        o.QueueLimit = 0;
    });
});

// ---------- Health checks (/health) ----------
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

// ---------- Сервисы приложения (DI) ----------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IOrderService, OrderService>();

var app = builder.Build();

// ---------- Инициализация БД и начальные данные ----------
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await DbSeeder.SeedAsync(scope.ServiceProvider, app.Configuration, logger);
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Ошибка инициализации базы данных");
        throw;
    }
}

// ---------- Конвейер обработки запросов (порядок middleware важен!) ----------
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts(); // Strict-Transport-Security: браузер ходит только по HTTPS
}

app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");
app.UseHttpsRedirection();
app.UseResponseCompression();          // как можно раньше — сжимаем всё ниже по конвейеру
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

// Статические файлы с долгим кэшем браузера (CSS/JS/картинки не скачиваются повторно)
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.CacheControl = "public,max-age=2592000"; // 30 дней
    }
});

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();               // кто ты?
app.UseAuthorization();                // что тебе можно?
app.UseSession();                      // корзина
app.UseResponseCaching();
app.UseOutputCache();

app.MapHealthChecks("/health");

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Нужен для интеграционных тестов (WebApplicationFactory<Program>)
public partial class Program
{
}
