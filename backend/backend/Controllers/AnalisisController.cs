using System.Globalization;
using backend.Data;
using backend.DTOs;
using backend.Helpers;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

// Línea de venta ya "aplanada" para analizarla en memoria
internal record LineaAnalisis(
    int VentaId, DateTime Fecha, decimal DescuentoPct,
    int ProductoId, string Codigo, string Nombre,
    int CategoriaId, string Categoria,
    int Cantidad, decimal PrecioUnitario)
{
    // Importe de la línea neto de descuento
    public decimal Importe => Cantidad * PrecioUnitario * (1m - DescuentoPct / 100m);

    // Día calendario de Lima en que ocurrió la venta
    public DateTime DiaLima => FechasLima.AHoraLima(Fecha).Date;
}

[ApiController]
[Route("api/analisis")]
[Authorize(Roles = "Administrador,Gerencia")]
public class AnalisisController(FerroStockDbContext db) : ControllerBase
{
    private const int MaxDiasPeriodo = 366;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // GET api/analisis/ventas?desde=2026-11-01&hasta=2026-11-30
    // Ventas del período para el dashboard (HU-20). Por defecto, los últimos 30 días.
    [HttpGet("ventas")]
    public async Task<ActionResult<AnalisisVentasDto>> GetVentas(
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        var (d, h, error) = ResolverPeriodo(desde, hasta, 30);
        if (error is not null) return BadRequest(new { message = error });

        var ini = FechasLima.InicioDiaUtc(d);
        var fin = FechasLima.FinDiaExclusivoUtc(h);

        // Totales y serie diaria salen de las ventas (coinciden con la pantalla de Ventas)
        var ventas = await db.Ventas.AsNoTracking()
            .Where(v => v.Estado == EstadoVenta.Completada && v.Fecha >= ini && v.Fecha < fin)
            .Select(v => new { v.Fecha, v.Total, v.DescuentoMonto })
            .ToListAsync();

        var lineas = await LineasAsync(ini, fin);

        var porDia = new List<PuntoDiaDto>();
        for (var dia = d.Date; dia <= h.Date; dia = dia.AddDays(1))
        {
            var delDia = ventas.Where(v => FechasLima.AHoraLima(v.Fecha).Date == dia).ToList();
            porDia.Add(new PuntoDiaDto(dia.ToString("yyyy-MM-dd", Inv), delDia.Sum(v => v.Total), delDia.Count));
        }

        var porCategoria = lineas
            .GroupBy(l => new { l.CategoriaId, l.Categoria })
            .Select(g => new VentaCategoriaDto(g.Key.CategoriaId, g.Key.Categoria,
                Math.Round(g.Sum(l => l.Importe), 2), g.Sum(l => l.Cantidad)))
            .OrderByDescending(c => c.Monto)
            .ToList();

        var top = lineas
            .GroupBy(l => new { l.ProductoId, l.Codigo, l.Nombre })
            .Select(g => new ProductoVendidoDto(g.Key.ProductoId, g.Key.Codigo, g.Key.Nombre,
                g.Sum(l => l.Cantidad), Math.Round(g.Sum(l => l.Importe), 2)))
            .OrderByDescending(p => p.Monto)
            .Take(10)
            .ToList();

        var total = ventas.Sum(v => v.Total);
        return Ok(new AnalisisVentasDto(
            d.ToString("yyyy-MM-dd", Inv), h.ToString("yyyy-MM-dd", Inv),
            total,
            ventas.Count,
            ventas.Count == 0 ? 0 : Math.Round(total / ventas.Count, 2),
            ventas.Sum(v => v.DescuentoMonto),
            lineas.Sum(l => l.Cantidad),
            porDia, porCategoria, top));
    }

    // GET api/analisis/rotacion?desde=&hasta=&top=20
    // Productos ordenados de mayor a menor rotación en el período (HU-22). Por defecto, 30 días.
    [HttpGet("rotacion")]
    public async Task<ActionResult<ReporteRotacionDto>> GetRotacion(
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] int? top)
    {
        var (d, h, error) = ResolverPeriodo(desde, hasta, 30);
        if (error is not null) return BadRequest(new { message = error });
        if (top is <= 0) return BadRequest(new { message = "'top' debe ser mayor a cero" });

        var ini = FechasLima.InicioDiaUtc(d);
        var fin = FechasLima.FinDiaExclusivoUtc(h);
        var dias = (h.Date - d.Date).Days + 1;

        var vendidas = (await LineasAsync(ini, fin))
            .GroupBy(l => l.ProductoId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Cantidad));

        // Para reconstruir el stock al inicio y al final del período se "deshacen" los
        // movimientos posteriores: stock_antes = stock_actual - entradas + salidas.
        var movimientos = await db.Movimientos.AsNoTracking()
            .Where(m => m.Fecha >= ini)
            .Select(m => new { m.ProductoId, m.Tipo, m.Cantidad, m.Fecha })
            .ToListAsync();

        // Neto por producto = entradas - salidas
        var desdeInicio = movimientos
            .GroupBy(m => m.ProductoId)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Tipo == TipoMovimiento.Entrada ? m.Cantidad : -m.Cantidad));
        var desdeFin = movimientos
            .Where(m => m.Fecha >= fin)
            .GroupBy(m => m.ProductoId)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Tipo == TipoMovimiento.Entrada ? m.Cantidad : -m.Cantidad));

        var productos = await db.Productos.AsNoTracking()
            .Where(p => p.Activo)
            .Select(p => new { p.Id, p.Codigo, p.Nombre, Categoria = p.Categoria.Nombre, p.StockActual })
            .ToListAsync();

        var filas = productos.Select(p =>
        {
            vendidas.TryGetValue(p.Id, out var uds);
            desdeInicio.TryGetValue(p.Id, out var netoIni);   // entradas - salidas desde el inicio
            desdeFin.TryGetValue(p.Id, out var netoFin);      // entradas - salidas desde el fin

            var stockInicio = Math.Max(0, p.StockActual - netoIni);
            var stockFin    = Math.Max(0, p.StockActual - netoFin);
            var promedio    = (stockInicio + stockFin) / 2m;

            var rotacion = promedio > 0 ? Math.Round(uds / promedio, 2) : 0m;
            decimal? cobertura = uds > 0 && p.StockActual > 0
                ? Math.Round(p.StockActual / ((decimal)uds / dias), 1)
                : null;

            return new RotacionProductoDto(p.Id, p.Codigo, p.Nombre, p.Categoria,
                uds, p.StockActual, Math.Round(promedio, 1), rotacion, cobertura);
        })
        .OrderByDescending(r => r.Rotacion)
        .ThenByDescending(r => r.UnidadesVendidas)
        .ThenBy(r => r.Nombre)
        .ToList();

        if (top.HasValue) filas = filas.Take(top.Value).ToList();

        return Ok(new ReporteRotacionDto(
            d.ToString("yyyy-MM-dd", Inv), h.ToString("yyyy-MM-dd", Inv), dias, filas));
    }

    // GET api/analisis/comparacion-mensual?mes=2026-11&categoriaId=2
    // Ventas del mes contra el mes anterior, con variación porcentual (HU-23).
    // Si el mes está en curso, se compara contra los MISMOS días del mes anterior
    // (p. ej. 1 al 7 de noviembre contra 1 al 7 de octubre) para que la comparación sea justa.
    [HttpGet("comparacion-mensual")]
    public async Task<ActionResult<ComparacionMensualDto>> GetComparacion(
        [FromQuery] string? mes, [FromQuery] int? categoriaId)
    {
        var hoy = FechasLima.HoyLima;

        DateTime inicioMes;
        if (string.IsNullOrWhiteSpace(mes))
            inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
        else if (!DateTime.TryParseExact(mes, "yyyy-MM", Inv, DateTimeStyles.None, out inicioMes))
            return BadRequest(new { message = "El mes debe tener el formato yyyy-MM (por ejemplo 2026-11)" });

        var inicioMesActual = new DateTime(hoy.Year, hoy.Month, 1);
        if (inicioMes > inicioMesActual)
            return BadRequest(new { message = "No se puede comparar un mes futuro" });

        string? nombreCategoria = null;
        if (categoriaId.HasValue)
        {
            nombreCategoria = await db.Categorias.Where(c => c.Id == categoriaId.Value)
                .Select(c => c.Nombre).FirstOrDefaultAsync();
            if (nombreCategoria is null) return NotFound(new { message = "Categoría no encontrada" });
        }

        var enCurso = inicioMes == inicioMesActual;
        var diasMes = DateTime.DaysInMonth(inicioMes.Year, inicioMes.Month);
        var diasTranscurridos = enCurso ? hoy.Day : diasMes;

        var actualIni = inicioMes;
        var actualFin = inicioMes.AddDays(diasTranscurridos - 1);

        var anteriorIni = inicioMes.AddMonths(-1);
        var diasMesAnterior = DateTime.DaysInMonth(anteriorIni.Year, anteriorIni.Month);
        var anteriorFin = anteriorIni.AddDays(Math.Min(diasTranscurridos, diasMesAnterior) - 1);

        // Una sola consulta que cubre ambos períodos
        var lineas = await LineasAsync(
            FechasLima.InicioDiaUtc(anteriorIni), FechasLima.FinDiaExclusivoUtc(actualFin));

        var delActual   = lineas.Where(l => l.DiaLima >= actualIni   && l.DiaLima <= actualFin).ToList();
        var delAnterior = lineas.Where(l => l.DiaLima >= anteriorIni && l.DiaLima <= anteriorFin).ToList();

        // Segmentación por categoría de la comparación
        var filtradoActual   = categoriaId.HasValue ? delActual.Where(l => l.CategoriaId == categoriaId).ToList()   : delActual;
        var filtradoAnterior = categoriaId.HasValue ? delAnterior.Where(l => l.CategoriaId == categoriaId).ToList() : delAnterior;

        var categorias = delActual.Concat(delAnterior)
            .Select(l => new { l.CategoriaId, l.Categoria }).Distinct()
            .Select(c => new ComparacionCategoriaDto(
                c.CategoriaId, c.Categoria,
                Math.Round(delActual.Where(l => l.CategoriaId == c.CategoriaId).Sum(l => l.Importe), 2),
                Math.Round(delAnterior.Where(l => l.CategoriaId == c.CategoriaId).Sum(l => l.Importe), 2),
                Variacion(
                    delActual.Where(l => l.CategoriaId == c.CategoriaId).Sum(l => l.Importe),
                    delAnterior.Where(l => l.CategoriaId == c.CategoriaId).Sum(l => l.Importe))))
            .OrderByDescending(c => c.MontoActual)
            .ToList();

        var actual   = Resumir(filtradoActual, actualIni, actualFin);
        var anterior = Resumir(filtradoAnterior, anteriorIni, anteriorFin);

        return Ok(new ComparacionMensualDto(
            inicioMes.ToString("yyyy-MM", Inv), enCurso, categoriaId, nombreCategoria,
            actual, anterior,
            Variacion(actual.Monto, anterior.Monto),
            Variacion(actual.Unidades, anterior.Unidades),
            Variacion(actual.Ventas, anterior.Ventas),
            categorias));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Líneas de ventas COMPLETADAS dentro de [ini, fin) (UTC)
    // Se proyecta a columnas simples (tipo anónimo) y el objeto se arma en memoria:
    // así la consulta SQL es trivial y no depende de cómo EF traduzca un constructor.
    private async Task<List<LineaAnalisis>> LineasAsync(DateTime ini, DateTime fin)
    {
        var filas = await db.DetallesVenta.AsNoTracking()
            .Where(d => d.Venta.Estado == EstadoVenta.Completada
                        && d.Venta.Fecha >= ini && d.Venta.Fecha < fin)
            .Select(d => new
            {
                d.VentaId,
                Fecha = d.Venta.Fecha,
                Descuento = d.Venta.DescuentoPorcentaje,
                d.ProductoId,
                Codigo = d.Producto.Codigo,
                Nombre = d.Producto.Nombre,
                CategoriaId = d.Producto.CategoriaId,
                Categoria = d.Producto.Categoria.Nombre,
                d.Cantidad,
                d.PrecioUnitario
            })
            .ToListAsync();

        return filas
            .Select(f => new LineaAnalisis(
                f.VentaId, f.Fecha, f.Descuento, f.ProductoId, f.Codigo, f.Nombre,
                f.CategoriaId, f.Categoria, f.Cantidad, f.PrecioUnitario))
            .ToList();
    }

    private static PeriodoVentasDto Resumir(List<LineaAnalisis> lineas, DateTime desde, DateTime hasta) =>
        new(desde.ToString("yyyy-MM-dd", Inv), hasta.ToString("yyyy-MM-dd", Inv),
            Math.Round(lineas.Sum(l => l.Importe), 2),
            lineas.Sum(l => l.Cantidad),
            lineas.Select(l => l.VentaId).Distinct().Count());

    // Variación porcentual; null cuando el período anterior no tiene base (0)
    private static decimal? Variacion(decimal actual, decimal anterior) =>
        anterior == 0 ? (actual == 0 ? 0m : null) : Math.Round((actual - anterior) / anterior * 100m, 1);

    private static (DateTime Desde, DateTime Hasta, string? Error) ResolverPeriodo(
        DateTime? desde, DateTime? hasta, int diasPorDefecto)
    {
        var h = (hasta ?? FechasLima.HoyLima).Date;
        var d = (desde ?? h.AddDays(-(diasPorDefecto - 1))).Date;

        if (d > h)
            return (d, h, "La fecha 'desde' no puede ser posterior a 'hasta'");
        if ((h - d).Days + 1 > MaxDiasPeriodo)
            return (d, h, $"El período no puede superar {MaxDiasPeriodo} días");
        return (d, h, null);
    }
}
