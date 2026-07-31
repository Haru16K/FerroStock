using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController(FerroStockDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var hoy = DateTime.Today;
        var manana = hoy.AddDays(1);
        var ayer = hoy.AddDays(-1);
        var hace7 = hoy.AddDays(-7);
        var hace30 = hoy.AddDays(-30);

        // ── KPIs ──────────────────────────────────────────────────────────
        var totalProductos = await db.Productos.CountAsync(p => p.Activo);
        var stockCritico = await db.Productos.CountAsync(p => p.Activo && p.StockActual <= p.StockMinimo);

        // Comparar el enum directamente, no con .ToString()
        var entradasHoy = await db.Movimientos.CountAsync(m => m.Tipo == TipoMovimiento.Entrada && m.Fecha >= hoy && m.Fecha < manana);
        var salidasHoy = await db.Movimientos.CountAsync(m => m.Tipo == TipoMovimiento.Salida && m.Fecha >= hoy && m.Fecha < manana);
        var entradasAyer = await db.Movimientos.CountAsync(m => m.Tipo == TipoMovimiento.Entrada && m.Fecha >= ayer && m.Fecha < hoy);
        var salidasAyer = await db.Movimientos.CountAsync(m => m.Tipo == TipoMovimiento.Salida && m.Fecha >= ayer && m.Fecha < hoy);

        // ── Movimientos recientes (últimos 7) ─────────────────────────────
        var movRecientes = await db.Movimientos
            .Include(m => m.Producto).ThenInclude(p => p.Categoria)
            .Include(m => m.Usuario)
            .OrderByDescending(m => m.Fecha)
            .Take(7)
            .Select(m => new
            {
                producto = m.Producto.Nombre,
                categoria = m.Producto.Categoria.Nombre,
                tipo = m.Tipo.ToString(),
                cantidad = m.Cantidad,
                fecha = m.Fecha.ToString("dd/MM HH:mm"),
                usuario = m.Usuario.Nombre
            })
            .ToListAsync();

        // ── Alertas de stock crítico ───────────────────────────────────────
        var alertas = await db.Productos
            .Include(p => p.Categoria)
            .Where(p => p.Activo && p.StockActual <= p.StockMinimo)
            .OrderBy(p => p.StockActual)
            .Take(5)
            .Select(p => new
            {
                nombre = p.Nombre,
                categoria = p.Categoria.Nombre,
                stock = p.StockActual,
                minimo = p.StockMinimo
            })
            .ToListAsync();

        // ── Stock por categoría (porcentaje) ──────────────────────────────
        var totalStock = await db.Productos
            .Where(p => p.Activo)
            .SumAsync(p => (double)p.StockActual);

        var categorias = await db.Categorias
            .Select(c => new
            {
                nombre = c.Nombre,
                total = c.Productos.Where(p => p.Activo).Sum(p => p.StockActual)
            })
            .Where(c => c.total > 0)
            .OrderByDescending(c => c.total)
            .ToListAsync();

        var categoriasConPct = categorias.Select(c => new
        {
            c.nombre,
            pct = totalStock > 0 ? (int)Math.Round(c.total * 100.0 / totalStock) : 0
        });

        // ── Top 5 productos con más movimientos (últimos 30 días) ─────────
        var movsTop = await db.Movimientos
            .Where(m => m.Fecha >= hace30)
            .Select(m => new
            {
                m.ProductoId,
                ProductoNombre = m.Producto.Nombre,
                CategoriaNombre = m.Producto.Categoria.Nombre
            })
            .ToListAsync();

        var topProductos = movsTop
            .GroupBy(m => new { m.ProductoId, m.ProductoNombre, m.CategoriaNombre })
            .Select(g => new
            {
                nombre = g.Key.ProductoNombre,
                categoria = g.Key.CategoriaNombre,
                movs = g.Count()
            })
            .OrderByDescending(x => x.movs)
            .Take(5)
            .ToList();

        // ── Gráfico últimos 7 días ─────────────────────────────────────────
        // Traer todos los movimientos del período en una sola query
        var movsGrafico = await db.Movimientos
            .Where(m => m.Fecha >= hace7 && m.Fecha < manana)
            .Select(m => new { m.Fecha, m.Tipo })
            .ToListAsync();

        var grafico = Enumerable.Range(0, 7).Select(i =>
        {
            var dia = hoy.AddDays(-6 + i);
            var diaSig = dia.AddDays(1);
            var label = i == 6 ? "Hoy" : dia.ToString("ddd dd",
                         new System.Globalization.CultureInfo("es-PE"));
            var entradas = movsGrafico.Count(m => m.Tipo == TipoMovimiento.Entrada && m.Fecha >= dia && m.Fecha < diaSig);
            var salidas = movsGrafico.Count(m => m.Tipo == TipoMovimiento.Salida && m.Fecha >= dia && m.Fecha < diaSig);
            return new { label, entradas, salidas };
        }).ToList();

        return Ok(new
        {
            kpis = new
            {
                totalProductos,
                stockCritico,
                entradasHoy,
                salidasHoy,
                deltaEntradas = entradasHoy - entradasAyer,
                deltaSalidas = salidasHoy - salidasAyer
            },
            movRecientes,
            alertas,
            categorias = categoriasConPct,
            topProductos,
            grafico = new
            {
                dias = grafico.Select(g => g.label),
                entradas = grafico.Select(g => g.entradas),
                salidas = grafico.Select(g => g.salidas)
            }
        });
    }
}