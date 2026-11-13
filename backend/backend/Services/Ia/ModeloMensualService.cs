using backend.Data;
using backend.Helpers;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Ia;

// Tarea en segundo plano (HU-29): al comenzar cada mes actualiza el modelo sin intervención
// manual. Si falla, queda registrado y se reintenta pasadas 6 horas. Se puede desactivar con
//   "Ia": { "JobMensual": { "Habilitado": false } }
public class ModeloMensualService(
    IServiceScopeFactory scopes, IConfiguration config, ILogger<ModeloMensualService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);   // deja que la app termine de arrancar
            while (!ct.IsCancellationRequested)
            {
                try { await EjecutarSiCorrespondeAsync(); }
                catch (Exception ex) { logger.LogError(ex, "Error en la tarea mensual del modelo"); }

                await Task.Delay(TimeSpan.FromHours(1), ct);
            }
        }
        catch (OperationCanceledException) { /* la aplicación se está cerrando */ }
    }

    private async Task EjecutarSiCorrespondeAsync()
    {
        if (!config.GetValue("Ia:JobMensual:Habilitado", true)) return;

        using var scope = scopes.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<FerroStockDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<PrediccionService>();

        var mes = FechasLima.HoyLima.ToString("yyyy-MM");

        // Ya se actualizó este mes
        if (await db.EjecucionesModelo.AnyAsync(e => e.Mes == mes && e.Estado == "Ok")) return;

        // Hubo un intento fallido hace poco: se espera antes de reintentar
        var ultimo = await db.EjecucionesModelo.Where(e => e.Mes == mes)
            .MaxAsync(e => (DateTime?)e.Inicio);
        if (ultimo.HasValue && DateTime.UtcNow - ultimo.Value < TimeSpan.FromHours(6)) return;

        logger.LogInformation("Actualizando el modelo de predicción del mes {Mes}", mes);
        await svc.ActualizarModeloAsync("Automática");
    }
}
