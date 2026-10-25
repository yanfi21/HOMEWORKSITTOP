using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shop.Web.Data;

namespace Shop.Web.Health;

/// <summary>Проверка доступности БД для эндпоинта /health (мониторинг, балансировщики, Docker).</summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _db;

    public DatabaseHealthCheck(ApplicationDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("База данных доступна")
                : HealthCheckResult.Unhealthy("Нет подключения к базе данных");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Ошибка подключения к БД", ex);
        }
    }
}
