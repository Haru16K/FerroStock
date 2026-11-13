using backend.Data;
using backend.Helpers;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Ia;

// Genera ~14 meses de ventas ficticias para poder probar y demostrar las predicciones
// (desde la aplicación no se pueden crear ventas con fecha pasada).
// Las ventas quedan marcadas EsDemo, NO tocan el stock ni los movimientos y se pueden borrar.
// Es determinista (misma semilla): siempre produce los mismos datos.
public class DatosDemoService(FerroStockDbContext db)
{
    private const int Dias = 430;

    public Task<bool> HayDatosAsync() => db.Ventas.AnyAsync(v => v.EsDemo);

    public async Task<int> GenerarAsync(int vendedorId)
    {
        if (await HayDatosAsync())
            throw new InvalidOperationException(
                "Ya existen datos de demostración. Elimínalos antes de generarlos de nuevo.");

        var productos = await db.Productos.AsNoTracking()
            .Where(p => p.Activo).OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.PrecioCompra, p.PrecioVenta })
            .ToListAsync();
        if (productos.Count == 0)
            throw new InvalidOperationException("No hay productos activos para generar ventas.");

        var rng = new Random(20260101);
        var hoy = FechasLima.HoyLima;
        var lote = new List<Venta>();
        var total = 0;

        for (var atras = Dias; atras >= 1; atras--)
        {
            var dia = hoy.AddDays(-atras);
            var lineas = new List<DetalleVenta>();

            for (var i = 0; i < productos.Count; i++)
            {
                var p = productos[i];
                var precio = p.PrecioVenta > 0 ? p.PrecioVenta : Math.Round(p.PrecioCompra * 1.35m, 2);
                if (precio <= 0) continue;

                var cantidad = Poisson(rng, Tasa(i, dia, atras));
                if (cantidad > 0)
                    lineas.Add(new DetalleVenta { ProductoId = p.Id, Cantidad = cantidad, PrecioUnitario = precio });
            }
            if (lineas.Count == 0) continue;

            // Los productos se agrupan en pares (0-1, 2-3, ...): los de un mismo par se venden en la
            // misma venta. Así hay productos que "se compran juntos" y se pueden probar las sugerencias.
            var indiceDe = productos.Select((p, idx) => (p.Id, idx)).ToDictionary(x => x.Id, x => x.idx);
            var grupos = lineas.GroupBy(l => indiceDe[l.ProductoId] / 2).OrderBy(g => g.Key);
            var minutos = 0;
            foreach (var g in grupos)
            {
                var detalle = g.ToList();
                var subtotal = detalle.Sum(l => l.Cantidad * l.PrecioUnitario);
                lote.Add(new Venta
                {
                    // Entre las 10:00 y las 18:00 hora de Lima
                    Fecha = FechasLima.InicioDiaUtc(dia).AddHours(10).AddMinutes(minutos + rng.Next(0, 30)),
                    Estado = EstadoVenta.Completada,
                    VendedorId = vendedorId,
                    ClienteNombre = "DEMO",
                    Subtotal = subtotal,
                    Total = subtotal,
                    EsDemo = true,
                    Detalles = detalle
                });
                minutos += 45;
            }

            if (lote.Count >= 60) total += await GuardarLoteAsync(lote);
        }
        if (lote.Count > 0) total += await GuardarLoteAsync(lote);
        return total;
    }

    public Task<int> EliminarAsync() =>
        db.Ventas.Where(v => v.EsDemo).ExecuteDeleteAsync();   // los detalles se borran en cascada

    private async Task<int> GuardarLoteAsync(List<Venta> lote)
    {
        var n = lote.Count;
        db.Ventas.AddRange(lote);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();   // evita acumular miles de entidades en memoria
        lote.Clear();
        return n;
    }

    // Demanda esperada (unidades/día) de cada producto en un día dado
    private static double Tasa(int indice, DateTime dia, int atras)
    {
        if (indice % 6 == 5 && atras < 200) return 0.0;               // dejó de venderse hace ~200 días
        var baseDia = 0.4 + (indice % 5) * 0.55;                       // 0,4 a 2,6 unidades/día
        var semana = dia.DayOfWeek switch
        {
            DayOfWeek.Saturday => 1.3,
            DayOfWeek.Sunday   => 0.4,
            _ => 1.0
        };
        var crecimiento = 1.0 + 0.0003 * (Dias - atras);               // leve crecimiento
        var temporada = indice % 4 == 0                                // 1 de cada 4: de verano
            ? (dia.Month is 12 or 1 or 2 ? 2.4 : 0.7)
            : 1.0;

        // La última semana trae un pico y una caída, para probar las alertas de anomalía
        var anomalia = 1.0;
        if (atras <= 7)
        {
            if (indice % 7 == 1) anomalia = 3.0;
            else if (indice % 7 == 2) anomalia = 0.1;
        }
        return baseDia * semana * crecimiento * temporada * anomalia;
    }

    private static int Poisson(Random rng, double lambda)
    {
        var limite = Math.Exp(-lambda);
        var k = 0;
        var p = 1.0;
        do { k++; p *= rng.NextDouble(); } while (p > limite);
        return k - 1;
    }
}
