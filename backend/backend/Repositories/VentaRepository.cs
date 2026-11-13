using backend.Data;
using backend.Helpers;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

public class VentaRepository(FerroStockDbContext db) : IVentaRepository
{
    private IQueryable<Venta> ConDetalle() =>
        db.Ventas
            .Include(v => v.Vendedor)
            .Include(v => v.DescuentoAplicadoPor)
            .Include(v => v.AnuladoPor)
            .Include(v => v.Detalles).ThenInclude(d => d.Producto);

    private IQueryable<Venta> Filtrar(
        int? vendedorId, int? productoId, DateTime? desde, DateTime? hasta, EstadoVenta? estado)
    {
        var q = db.Ventas.AsNoTracking().AsQueryable();

        if (vendedorId.HasValue)
            q = q.Where(v => v.VendedorId == vendedorId.Value);
        if (productoId.HasValue)
            q = q.Where(v => v.Detalles.Any(d => d.ProductoId == productoId.Value));
        if (desde.HasValue)
        {
            var inicio = FechasLima.InicioDiaUtc(desde.Value);
            q = q.Where(v => v.Fecha >= inicio);
        }
        if (hasta.HasValue)
        {
            var limite = FechasLima.FinDiaExclusivoUtc(hasta.Value);
            q = q.Where(v => v.Fecha < limite);
        }
        if (estado.HasValue)
            q = q.Where(v => v.Estado == estado.Value);
        return q;
    }

    public async Task<ListadoVentas> ListarAsync(
        int? vendedorId, int? productoId, DateTime? desde, DateTime? hasta,
        EstadoVenta? estado, int limite, CancellationToken ct = default)
    {
        var q = Filtrar(vendedorId, productoId, desde, hasta, estado);

        // Totales sobre TODAS las ventas que cumplen los filtros (no solo las mostradas)
        var total = await q.CountAsync(ct);
        var completadas = q.Where(v => v.Estado == EstadoVenta.Completada);
        var nCompletadas = await completadas.CountAsync(ct);
        var vendido = await completadas.SumAsync(v => (decimal?)v.Total, ct) ?? 0m;
        var descuentos = await completadas.SumAsync(v => (decimal?)v.DescuentoMonto, ct) ?? 0m;

        // Solo las columnas de la tabla: nada de usuarios completos ni de líneas
        var filas = await q
            .OrderByDescending(v => v.Fecha).ThenByDescending(v => v.Id)
            .Take(limite)
            .Select(v => new
            {
                v.Id, v.Fecha, v.Estado, v.VendedorId,
                VendedorNombre = v.Vendedor.Nombre,
                v.ClienteNombre, v.ClienteDocumento,
                v.Subtotal, v.DescuentoPorcentaje, v.DescuentoMonto, v.Total,
                Unidades = v.Detalles.Sum(d => d.Cantidad),
                Lineas = v.Detalles.Count(),
                v.EsDemo
            })
            .ToListAsync(ct);

        var items = filas.Select(f => new VentaResumen(
            f.Id, f.Fecha, f.Estado, f.VendedorId, f.VendedorNombre, f.ClienteNombre, f.ClienteDocumento,
            f.Subtotal, f.DescuentoPorcentaje, f.DescuentoMonto, f.Total, f.Unidades, f.Lineas, f.EsDemo)).ToList();

        return new ListadoVentas(total, nCompletadas, vendido, descuentos, items);
    }

    public Task<Venta?> GetByIdAsync(int id) =>
        ConDetalle().FirstOrDefaultAsync(v => v.Id == id);

    // Usuarios que pueden aparecer en el filtro "vendedor": los de rol Vendedor
    // y cualquiera que ya tenga ventas registradas (p. ej. un administrador).
    public async Task<IEnumerable<Usuario>> GetVendedoresAsync() =>
        await db.Usuarios
            .Where(u => u.Rol == RolUsuario.Vendedor || db.Ventas.Any(v => v.VendedorId == u.Id))
            .OrderBy(u => u.Nombre)
            .ToListAsync();

    // ── Registrar (HU-11, HU-12, HU-13) ───────────────────────────────────────
    public async Task<Venta> RegistrarAsync(Venta venta, decimal descuentoMaximoPorcentaje)
    {
        if (venta.Detalles.Count == 0)
            throw new InvalidOperationException("La venta debe tener al menos un producto");
        if (venta.Detalles.Any(d => d.Cantidad <= 0))
            throw new InvalidOperationException("Las cantidades deben ser mayores a cero");

        var ids = venta.Detalles.Select(d => d.ProductoId).ToList();
        if (ids.Distinct().Count() != ids.Count)
            throw new InvalidOperationException("Hay productos repetidos en la venta");

        if (venta.DescuentoPorcentaje < 0 || venta.DescuentoPorcentaje > descuentoMaximoPorcentaje)
            throw new InvalidOperationException(
                $"El descuento debe estar entre 0% y {descuentoMaximoPorcentaje:0.##}% (límite autorizado)");

        if ((venta.ClienteNombre?.Length ?? 0) > 150 || (venta.ClienteDocumento?.Length ?? 0) > 20)
            throw new InvalidOperationException("Los datos del cliente exceden la longitud permitida");

        var productos = await db.Productos
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        // Validación previa para dar mensajes claros (HU-12: no vender agotados)
        foreach (var d in venta.Detalles)
        {
            if (!productos.TryGetValue(d.ProductoId, out var prod) || !prod.Activo)
                throw new InvalidOperationException($"Producto {d.ProductoId} no encontrado o inactivo");
            if (prod.PrecioVenta <= 0)
                throw new InvalidOperationException(
                    $"El producto {prod.Codigo} no tiene precio de venta configurado");
            if (prod.StockActual <= 0)
                throw new InvalidOperationException($"{prod.Nombre} está agotado (stock 0)");
            if (prod.StockActual < d.Cantidad)
                throw new InvalidOperationException(
                    $"Stock insuficiente de {prod.Nombre}. Disponible: {prod.StockActual}");

            d.PrecioUnitario = prod.PrecioVenta;   // se congela el precio de este momento
        }

        venta.Fecha = DateTime.UtcNow;
        venta.Estado = EstadoVenta.Completada;
        venta.Subtotal = venta.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario);
        venta.DescuentoMonto = Math.Round(venta.Subtotal * venta.DescuentoPorcentaje / 100m, 2);
        venta.Total = venta.Subtotal - venta.DescuentoMonto;
        venta.DescuentoAplicadoPorId = venta.DescuentoPorcentaje > 0 ? venta.VendedorId : null;

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // Descuento de stock ATÓMICO: la condición StockActual >= cantidad va dentro
            // del UPDATE, así dos ventas simultáneas no pueden dejar el stock en negativo.
            foreach (var d in venta.Detalles)
            {
                var productoId = d.ProductoId;
                var cantidad = d.Cantidad;

                var filas = await db.Productos
                    .Where(p => p.Id == productoId && p.Activo && p.StockActual >= cantidad)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockActual, p => p.StockActual - cantidad));

                if (filas == 0)
                {
                    var disponible = await db.Productos
                        .Where(p => p.Id == productoId)
                        .Select(p => p.StockActual)
                        .FirstOrDefaultAsync();
                    throw new InvalidOperationException(
                        $"Stock insuficiente de {productos[productoId].Nombre}. Disponible: {disponible}");
                }
            }

            db.Ventas.Add(venta);
            await db.SaveChangesAsync();   // genera el Id (número de venta)

            // Cada línea queda en el historial de movimientos como una salida
            foreach (var d in venta.Detalles)
            {
                db.Movimientos.Add(new Movimiento
                {
                    ProductoId = d.ProductoId,
                    Tipo = TipoMovimiento.Salida,
                    Cantidad = d.Cantidad,
                    Fecha = venta.Fecha,
                    Motivo = $"Venta V-{venta.Id:D5}",
                    PrecioUnitario = d.PrecioUnitario,
                    UsuarioId = venta.VendedorId
                });
            }
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return (await GetByIdAsync(venta.Id))!;
    }

    // ── Anular (HU-14) ────────────────────────────────────────────────────────
    public async Task<Venta> AnularAsync(int id, int usuarioId, string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Debe indicar la justificación de la anulación");
        motivo = motivo.Trim();
        if (motivo.Length > 300)
            throw new InvalidOperationException("La justificación no puede superar los 300 caracteres");

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var venta = await db.Ventas
                .AsNoTracking()
                .Include(v => v.Detalles)
                .FirstOrDefaultAsync(v => v.Id == id)
                ?? throw new KeyNotFoundException("Venta no encontrada");

            if (venta.EsDemo)
                throw new InvalidOperationException(
                    "Las ventas de demostración no se anulan porque no descontaron stock. Elimínalas desde Predicciones.");

            var ahora = DateTime.UtcNow;

            // Cambio de estado ATÓMICO: solo una anulación puede "ganar", así el stock
            // no se repone dos veces si dos administradores anulan a la vez.
            var filas = await db.Ventas
                .Where(v => v.Id == id && v.Estado == EstadoVenta.Completada)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(v => v.Estado, EstadoVenta.Anulada)
                    .SetProperty(v => v.AnuladoPorId, (int?)usuarioId)
                    .SetProperty(v => v.FechaAnulacion, (DateTime?)ahora)
                    .SetProperty(v => v.MotivoAnulacion, motivo));

            if (filas == 0)
                throw new InvalidOperationException("La venta ya fue anulada");

            foreach (var d in venta.Detalles)
            {
                var productoId = d.ProductoId;
                var cantidad = d.Cantidad;

                await db.Productos
                    .Where(p => p.Id == productoId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockActual, p => p.StockActual + cantidad));

                db.Movimientos.Add(new Movimiento
                {
                    ProductoId = d.ProductoId,
                    Tipo = TipoMovimiento.Entrada,
                    Cantidad = d.Cantidad,
                    Fecha = ahora,
                    Motivo = $"Anulación venta V-{venta.Id:D5}",
                    PrecioUnitario = d.PrecioUnitario,
                    UsuarioId = usuarioId
                });
            }
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return (await GetByIdAsync(id))!;
    }
}
