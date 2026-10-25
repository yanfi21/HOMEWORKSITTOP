using System.Diagnostics;

namespace Shop.Web.Middleware;

/// <summary>
/// Логирует каждый HTTP-запрос встроенным ILogger: метод, путь, статус, время выполнения.
/// Медленные запросы (&gt; 500 мс) помечаются Warning — их легко найти в логах при поиске узких мест.
/// Время также отдаётся клиенту в заголовке X-Response-Time-Ms (удобно смотреть в DevTools на экзамене).
/// </summary>
public class RequestLoggingMiddleware
{
    private const long SlowRequestMs = 500;
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();

        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Response-Time-Ms"] = sw.ElapsedMilliseconds.ToString();
            return Task.CompletedTask;
        });

        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            var path = context.Request.Path.Value ?? "/";

            // Статику не шумим в Information-лог, только медленные/ошибочные запросы.
            var isStatic = path.StartsWith("/css", StringComparison.OrdinalIgnoreCase)
                           || path.StartsWith("/js", StringComparison.OrdinalIgnoreCase)
                           || path.StartsWith("/images", StringComparison.OrdinalIgnoreCase);

            if (sw.ElapsedMilliseconds > SlowRequestMs || context.Response.StatusCode >= 500)
            {
                _logger.LogWarning("Медленный/ошибочный запрос {Method} {Path} -> {StatusCode} за {ElapsedMs} мс (user={User})",
                    context.Request.Method, path, context.Response.StatusCode, sw.ElapsedMilliseconds,
                    context.User.Identity?.Name ?? "anon");
            }
            else if (!isStatic)
            {
                _logger.LogInformation("{Method} {Path} -> {StatusCode} за {ElapsedMs} мс",
                    context.Request.Method, path, context.Response.StatusCode, sw.ElapsedMilliseconds);
            }
        }
    }
}
