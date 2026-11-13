using backend.Data;
using backend.Helpers;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Ia;

public record ProductoPrediccion(
    int ProductoId, string Codigo, string Nombre, int CategoriaId, string Categoria,
    int StockActual, int StockMinimo, ResultadoPrediccion Resultado, int[] Serie);

public record ResultadoTendencia(
    string Horizonte, List<string> EtiquetasHistoricas, List<string> EtiquetasProyeccion,
    List<(int CategoriaId, string Categoria, List<decimal> Historico, List<decimal> Proyeccion)> Categorias);

public record ResultadoPrecision(
    decimal Mae, decimal? Mape, decimal? Wape, int Observaciones, int ProductosEvaluados,
    List<(int ProductoId, string Codigo, string Nombre, decimal Mae, decimal? Mape)> PorProducto);

// Carga el historial de ventas y aplica el MotorPrediccion. Todo se calcula al momento con
// los datos actuales, por eso cada venta nueva se refleja de inmediato (HU-25).
public class PrediccionService(FerroStockDbContext db, IConfiguration config, ILogger<PrediccionService> logger)
{
    public const int HistorialDias = 760;   // ~25 meses: alcanza para detectar estacionalidad

    // Unidades vendidas por día y producto. El último elemento de cada serie es AYER.
    public async Task<Dictionary<int, int[]>> CargarSeriesAsync(IReadOnlyCollection<int>? productoIds = null)
    {
        var hoy  = FechasLima.HoyLima;
        var ayer = hoy.AddDays(-1);
        var ini  = FechasLima.InicioDiaUtc(ayer.AddDays(-(HistorialDias - 1)));
        var fin  = FechasLima.InicioDiaUtc(hoy);   // exclusivo: hoy no entra

        var q = db.DetallesVenta.AsNoTracking()
            .Where(d => d.Venta.Estado == EstadoVenta.Completada
                        && d.Venta.Fecha >= ini && d.Venta.Fecha < fin);
        if (productoIds is not null)
            q = q.Where(d => productoIds.Contains(d.ProductoId));

        var filas = await q
            .Select(d => new { d.ProductoId, Fecha = d.Venta.Fecha, d.Cantidad })
            .ToListAsync();

        var series = new Dictionary<int, int[]>();
        foreach (var f in filas)
        {
            var dia = FechasLima.AHoraLima(f.Fecha).Date;
            var desfase = (ayer - dia).Days;
            if (desfase < 0 || desfase >= HistorialDias) continue;

            if (!series.TryGetValue(f.ProductoId, out var arr))
                series[f.ProductoId] = arr = new int[HistorialDias];
            arr[HistorialDias - 1 - desfase] += f.Cantidad;
        }
        return series;
    }

    public async Task<List<ProductoPrediccion>> CalcularProductosAsync(int? productoId = null)
    {
        var hoy = FechasLima.HoyLima;

        var q = db.Productos.AsNoTracking().Where(p => p.Activo);
        if (productoId.HasValue) q = q.Where(p => p.Id == productoId.Value);

        var productos = await q
            .Select(p => new
            {
                p.Id, p.Codigo, p.Nombre, p.CategoriaId,
                Categoria = p.Categoria.Nombre, p.StockActual, p.StockMinimo
            })
            .ToListAsync();

        var series = await CargarSeriesAsync(productoId.HasValue ? new[] { productoId.Value } : null);

        return productos.Select(p =>
        {
            var serie = series.TryGetValue(p.Id, out var s) ? s : new int[HistorialDias];
            return new ProductoPrediccion(
                p.Id, p.Codigo, p.Nombre, p.CategoriaId, p.Categoria, p.StockActual, p.StockMinimo,
                MotorPrediccion.Calcular(serie, p.StockActual, hoy), serie);
        }).ToList();
    }

    // Texto que explica de dónde sale el número (transparencia del modelo)
    public static string Explicar(ProductoPrediccion p)
    {
        var r = p.Resultado;
        if (r.DiasHistorial == 0)
            return "Este producto aún no tiene ventas registradas, por eso no se puede pronosticar su demanda.";

        var corta = MotorPrediccion.TasaDiaria(p.Serie, p.Serie.Length,
            MotorPrediccion.PrimerDiaConVentas(p.Serie, p.Serie.Length), MotorPrediccion.VentanaCorta);
        var larga = MotorPrediccion.TasaDiaria(p.Serie, p.Serie.Length,
            MotorPrediccion.PrimerDiaConVentas(p.Serie, p.Serie.Length), MotorPrediccion.VentanaLarga);

        var txt = $"Promedio ponderado de ventas: 60 % de los últimos 28 días ({corta:0.##} u/día) y " +
                  $"40 % de los últimos 84 días ({larga:0.##} u/día), proyectado a 30 días. " +
                  $"Historial disponible: {r.DiasHistorial} días.";
        if (r.EsEstacional && r.MesPico.HasValue)
            txt += $" Producto estacional (pico en {MotorPrediccion.Meses[r.MesPico.Value - 1]}); " +
                   $"ajuste por temporada x{r.FactorEstacional:0.##}.";
        if (r.NivelConfianza == "Baja")
            txt += " La confianza es baja: hay poco historial o las ventas son muy irregulares.";
        return txt;
    }

    // ── Tendencia por categoría (HU-26) ───────────────────────────────────────
    public async Task<ResultadoTendencia> TendenciaPorCategoriaAsync(bool mensual)
    {
        var hoy  = FechasLima.HoyLima;
        var ayer = hoy.AddDays(-1);

        // Períodos históricos (del más antiguo al más reciente) y a proyectar
        var periodos = new List<(DateTime Desde, DateTime HastaExcl, string Etiqueta)>();
        int nHist, nProy;
        if (mensual)
        {
            nHist = 6; nProy = 3;
            var primerMesActual = new DateTime(hoy.Year, hoy.Month, 1);
            for (var i = nHist; i >= 1; i--)
            {
                var m = primerMesActual.AddMonths(-i);
                periodos.Add((m, m.AddMonths(1), $"{MotorPrediccion.Meses[m.Month - 1][..3]} {m.Year}"));
            }
        }
        else
        {
            nHist = 12; nProy = 4;
            for (var i = nHist - 1; i >= 0; i--)
            {
                var fin = ayer.AddDays(-7 * i);            // último día de la semana (inclusive)
                var ini = fin.AddDays(-6);
                periodos.Add((ini, fin.AddDays(1), $"{ini:dd/MM}"));
            }
        }

        var desde = FechasLima.InicioDiaUtc(periodos.First().Desde);
        var hasta = FechasLima.InicioDiaUtc(periodos.Last().HastaExcl);

        var filas = await db.DetallesVenta.AsNoTracking()
            .Where(d => d.Venta.Estado == EstadoVenta.Completada && d.Venta.Fecha >= desde && d.Venta.Fecha < hasta)
            .Select(d => new
            {
                Fecha = d.Venta.Fecha, Descuento = d.Venta.DescuentoPorcentaje,
                CategoriaId = d.Producto.CategoriaId, Categoria = d.Producto.Categoria.Nombre,
                d.Cantidad, d.PrecioUnitario
            })
            .ToListAsync();

        var etiquetasProy = new List<string>();
        for (var i = 1; i <= nProy; i++)
        {
            if (mensual)
            {
                var m = new DateTime(hoy.Year, hoy.Month, 1).AddMonths(i - 1);
                etiquetasProy.Add($"{MotorPrediccion.Meses[m.Month - 1][..3]} {m.Year}");
            }
            else
                etiquetasProy.Add($"{ayer.AddDays(7 * (i - 1) + 1):dd/MM}");
        }

        var resultado = new List<(int, string, List<decimal>, List<decimal>)>();
        foreach (var g in filas.GroupBy(f => new { f.CategoriaId, f.Categoria }))
        {
            var historico = new List<decimal>();
            foreach (var p in periodos)
            {
                var ini = FechasLima.InicioDiaUtc(p.Desde);
                var fin = FechasLima.InicioDiaUtc(p.HastaExcl);
                historico.Add(Math.Round(g
                    .Where(f => f.Fecha >= ini && f.Fecha < fin)
                    .Sum(f => f.Cantidad * f.PrecioUnitario * (1m - f.Descuento / 100m)), 2));
            }

            var (pendiente, intercepto) = MotorPrediccion.Regresion(historico.Select(h => (double)h).ToList());
            var proyeccion = new List<decimal>();
            for (var i = 0; i < nProy; i++)
                proyeccion.Add(Math.Round((decimal)Math.Max(0, intercepto + pendiente * (historico.Count + i)), 2));

            resultado.Add((g.Key.CategoriaId, g.Key.Categoria, historico, proyeccion));
        }

        return new ResultadoTendencia(
            mensual ? "mensual" : "semanal",
            periodos.Select(p => p.Etiqueta).ToList(), etiquetasProy,
            resultado.OrderByDescending(r => r.Item3.Sum()).ToList());
    }

    // ── Precisión del modelo (HU-28) ──────────────────────────────────────────
    public const int SemanasBacktest = 4;

    public async Task<ResultadoPrecision> CalcularPrecisionAsync()
    {
        var productos = await db.Productos.AsNoTracking().Where(p => p.Activo)
            .Select(p => new { p.Id, p.Codigo, p.Nombre }).ToListAsync();
        var series = await CargarSeriesAsync();

        var todas = new List<ObservacionBacktest>();
        var porProducto = new List<(int, string, string, decimal, decimal?)>();

        foreach (var p in productos)
        {
            if (!series.TryGetValue(p.Id, out var serie)) continue;
            var obs = MotorPrediccion.Backtest(serie, SemanasBacktest);
            if (obs.Count == 0) continue;

            todas.AddRange(obs);
            var (mae, mape, _) = MotorPrediccion.Metricas(obs);
            porProducto.Add((p.Id, p.Codigo, p.Nombre, mae, mape));
        }

        var (maeG, mapeG, wapeG) = MotorPrediccion.Metricas(todas);
        return new ResultadoPrecision(maeG, mapeG, wapeG, todas.Count, porProducto.Count, porProducto);
    }

    // ── Actualización del modelo (HU-29) ──────────────────────────────────────
    // Recalcula la estacionalidad de cada producto y mide la precisión. Todo se calcula
    // primero en memoria y solo al final se escribe, así un fallo no deja datos a medias.
    public async Task<EjecucionModelo> ActualizarModeloAsync(string origen, bool simularFallo = false)
    {
        var ejecucion = new EjecucionModelo
        {
            Inicio = DateTime.UtcNow,
            Origen = origen,
            Mes = FechasLima.HoyLima.ToString("yyyy-MM"),
            Estado = "Ok"
        };

        try
        {
            if (simularFallo)
                throw new InvalidOperationException("Fallo simulado para probar la notificación (CP-29).");

            var productos = await db.Productos.AsNoTracking().Where(p => p.Activo)
                .Select(p => new { p.Id, p.EsEstacional, p.MesPicoEstacional }).ToListAsync();
            var series = await CargarSeriesAsync();
            var hoy = FechasLima.HoyLima;

            var cambios = new List<(int Id, bool Es, int? Mes)>();
            var estacionales = 0;
            foreach (var p in productos)
            {
                var serie = series.TryGetValue(p.Id, out var s) ? s : new int[HistorialDias];
                var primer = MotorPrediccion.PrimerDiaConVentas(serie, serie.Length);
                var (es, mes, _) = MotorPrediccion.DetectarEstacionalidad(serie, primer, hoy);
                if (es) estacionales++;
                if (es != p.EsEstacional || mes != p.MesPicoEstacional) cambios.Add((p.Id, es, mes));
            }

            var precision = await CalcularPrecisionAsync();

            foreach (var c in cambios)
            {
                var id = c.Id; var es = c.Es; var mes = c.Mes;
                await db.Productos.Where(p => p.Id == id)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.EsEstacional, es)
                        .SetProperty(p => p.MesPicoEstacional, mes));
            }

            ejecucion.ProductosProcesados = productos.Count;
            ejecucion.EstacionalesDetectados = estacionales;
            ejecucion.Mae = precision.Mae;
            ejecucion.Mape = precision.Mape;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló la actualización del modelo de predicción");
            ejecucion.Estado = "Error";
            var msg = ex.Message;
            ejecucion.Mensaje = msg.Length > 480 ? msg[..480] : msg;
        }

        ejecucion.Fin = DateTime.UtcNow;
        db.EjecucionesModelo.Add(ejecucion);
        await db.SaveChangesAsync();
        return ejecucion;
    }
}
