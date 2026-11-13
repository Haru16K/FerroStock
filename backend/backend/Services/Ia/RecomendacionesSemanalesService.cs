using backend.Services.Ia;

namespace backend.Services.Ia;

// Tarea en segundo plano (HU-31): la lista de recomendaciones se actualiza sola cada semana.
// Se puede desactivar con   "Ia": { "JobSemanal": { "Habilitado": false } }
public class RecomendacionesSemanalesService(
    IServiceScopeFactory scopes, IConfiguration config, ILogger<RecomendacionesSemanalesService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(45), ct);
            while (!ct.IsCancellationRequested)
            {
                try { await EjecutarSiCorrespondeAsync(); }
                catch (Exception ex) { logger.LogError(ex, "Error al generar las recomendaciones semanales"); }

                await Task.Delay(TimeSpan.FromHours(6), ct);
            }
        }
        catch (OperationCanceledException) { /* la aplicación se está cerrando */ }
    }

    private async Task EjecutarSiCorrespondeAsync()
    {
        if (!config.GetValue("Ia:JobSemanal:Habilitado", true)) return;

        using var scope = scopes.CreateScope();
        var cfg = scope.ServiceProvider.GetRequiredService<ConfiguracionIaService>();
        var svc = scope.ServiceProvider.GetRequiredService<RecomendacionService>();

        var ultima = await cfg.GetTextoAsync(ConfiguracionIaService.ClaveUltimaGeneracion);
        if (DateTime.TryParse(ultima, null, System.Globalization.DateTimeStyles.RoundtripKind, out var fecha)
            && DateTime.UtcNow - fecha < TimeSpan.FromDays(7))
            return;

        await svc.GenerarAsync();
    }
}
