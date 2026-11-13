using backend.Data;
using backend.Helpers;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

public class OrdenCompraRepository(FerroStockDbContext db) : IOrdenCompraRepository
{
    private IQueryable<OrdenCompra> ConDetalle() =>
        db.OrdenesCompra
            .Include(o => o.Proveedor)
            .Include(o => o.Detalles).ThenInclude(d => d.Producto)
            .Include(o => o.CreadoPor)
            .Include(o => o.RevisadoPor)
            .Include(o => o.RecibidoPor)
            .Include(o => o.CanceladoPor);

    public async Task<IEnumerable<OrdenCompra>> GetAllAsync(
        int? proveedorId = null, DateTime? desde = null, DateTime? hasta = null,
        EstadoOrdenCompra? estado = null)
    {
        var q = ConDetalle();

        if (proveedorId.HasValue)
            q = q.Where(o => o.ProveedorId == proveedorId.Value);
        if (desde.HasValue)
        {
            var inicio = FechasLima.InicioDiaUtc(desde.Value);
            q = q.Where(o => o.FechaCreacion >= inicio);
        }
        if (hasta.HasValue)
        {
            var limite = FechasLima.FinDiaExclusivoUtc(hasta.Value);   // incluye todo el día "hasta" (hora de Lima)
            q = q.Where(o => o.FechaCreacion < limite);
        }
        if (estado.HasValue)
            q = q.Where(o => o.Estado == estado.Value);

        return await q.AsNoTracking().OrderByDescending(o => o.FechaCreacion).ToListAsync();
    }

    public Task<OrdenCompra?> GetByIdAsync(int id) =>
        ConDetalle().FirstOrDefaultAsync(o => o.Id == id);

    // ── Crear (HU-06) ─────────────────────────────────────────────────────────
    public async Task<OrdenCompra> CrearAsync(OrdenCompra orden)
    {
        var proveedor = await db.Proveedores.FindAsync(orden.ProveedorId)
            ?? throw new KeyNotFoundException("Proveedor no encontrado");
        if (!proveedor.Activo)
            throw new InvalidOperationException("El proveedor está inactivo");

        if (orden.Detalles.Count == 0)
            throw new InvalidOperationException("La orden debe tener al menos un producto");
        if (orden.Detalles.Any(d => d.Cantidad <= 0))
            throw new InvalidOperationException("Las cantidades deben ser mayores a cero");
        if (orden.Detalles.Any(d => d.PrecioUnitario <= 0))
            throw new InvalidOperationException("Los precios unitarios deben ser mayores a cero");

        var ids = orden.Detalles.Select(d => d.ProductoId).ToList();
        if (ids.Distinct().Count() != ids.Count)
            throw new InvalidOperationException("Hay productos repetidos en la orden");

        var activos = await db.Productos
            .Where(p => ids.Contains(p.Id) && p.Activo)
            .Select(p => p.Id)
            .ToListAsync();
        var faltantes = ids.Except(activos).ToList();
        if (faltantes.Count > 0)
            throw new InvalidOperationException(
                $"Productos no encontrados o inactivos: {string.Join(", ", faltantes)}");

        orden.Estado = EstadoOrdenCompra.Pendiente;
        orden.FechaCreacion = DateTime.UtcNow;
        orden.Total = Math.Round(orden.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario), 2);

        db.OrdenesCompra.Add(orden);
        await db.SaveChangesAsync();
        return (await GetByIdAsync(orden.Id))!;
    }

    // ── Aprobar / rechazar (HU-07) ────────────────────────────────────────────
    public async Task<OrdenCompra> AprobarAsync(int id, int usuarioId)
    {
        var orden = await db.OrdenesCompra.FindAsync(id)
            ?? throw new KeyNotFoundException("Orden de compra no encontrada");
        if (orden.Estado != EstadoOrdenCompra.Pendiente)
            throw new InvalidOperationException(
                $"Solo se puede aprobar una orden Pendiente (estado actual: {orden.Estado})");

        orden.Estado = EstadoOrdenCompra.Aprobada;
        orden.RevisadoPorId = usuarioId;
        orden.FechaRevision = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (await GetByIdAsync(id))!;
    }

    public async Task<OrdenCompra> RechazarAsync(int id, int usuarioId, string? motivo)
    {
        var orden = await db.OrdenesCompra.FindAsync(id)
            ?? throw new KeyNotFoundException("Orden de compra no encontrada");
        if (orden.Estado != EstadoOrdenCompra.Pendiente)
            throw new InvalidOperationException(
                $"Solo se puede rechazar una orden Pendiente (estado actual: {orden.Estado})");

        orden.Estado = EstadoOrdenCompra.Rechazada;
        orden.RevisadoPorId = usuarioId;
        orden.FechaRevision = DateTime.UtcNow;
        orden.MotivoRechazo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
        await db.SaveChangesAsync();
        return (await GetByIdAsync(id))!;
    }

    // ── Cancelar (HU-10) ──────────────────────────────────────────────────────
    public async Task<OrdenCompra> CancelarAsync(int id, int usuarioId, string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new InvalidOperationException("Debe indicar el motivo de la cancelación");

        var orden = await db.OrdenesCompra.FindAsync(id)
            ?? throw new KeyNotFoundException("Orden de compra no encontrada");
        if (orden.Estado is not (EstadoOrdenCompra.Pendiente or EstadoOrdenCompra.Aprobada))
            throw new InvalidOperationException(
                $"Solo se puede cancelar una orden que aún no fue recibida (estado actual: {orden.Estado})");

        orden.Estado = EstadoOrdenCompra.Cancelada;
        orden.CanceladoPorId = usuarioId;
        orden.FechaCancelacion = DateTime.UtcNow;
        orden.MotivoCancelacion = motivo.Trim();
        await db.SaveChangesAsync();
        return (await GetByIdAsync(id))!;
    }

    // ── Recibir (HU-08) ───────────────────────────────────────────────────────
    // Suma al stock lo efectivamente recibido y deja un Movimiento de Entrada por línea,
    // de modo que la recepción también aparece en el historial de movimientos.
    public async Task<OrdenCompra> RecibirAsync(
        int id, int usuarioId, IReadOnlyDictionary<int, int> cantidadesPorDetalle)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var orden = await db.OrdenesCompra
                .Include(o => o.Proveedor)
                .Include(o => o.Detalles)
                .FirstOrDefaultAsync(o => o.Id == id)
                ?? throw new KeyNotFoundException("Orden de compra no encontrada");

            if (orden.Estado != EstadoOrdenCompra.Aprobada)
                throw new InvalidOperationException(
                    $"Solo se puede recibir una orden Aprobada (estado actual: {orden.Estado})");

            var idsDetalle = orden.Detalles.Select(d => d.Id).ToHashSet();
            if (!idsDetalle.SetEquals(cantidadesPorDetalle.Keys))
                throw new InvalidOperationException(
                    "Debe informar la cantidad recibida de cada línea de la orden");

            foreach (var d in orden.Detalles)
            {
                var q = cantidadesPorDetalle[d.Id];
                if (q < 0 || q > d.Cantidad)
                    throw new InvalidOperationException(
                        $"La cantidad recibida debe estar entre 0 y {d.Cantidad} (línea {d.Id})");
            }
            if (cantidadesPorDetalle.Values.All(q => q == 0))
                throw new InvalidOperationException(
                    "Debe recibir al menos una unidad. Si no llegó nada, cancele la orden");

            var ahora = DateTime.UtcNow;
            foreach (var d in orden.Detalles)
            {
                var q = cantidadesPorDetalle[d.Id];
                d.CantidadRecibida = q;
                if (q == 0) continue;

                var producto = await db.Productos.FindAsync(d.ProductoId)
                    ?? throw new KeyNotFoundException($"Producto {d.ProductoId} no encontrado");
                producto.StockActual += q;

                db.Movimientos.Add(new Movimiento
                {
                    ProductoId = d.ProductoId,
                    Tipo = TipoMovimiento.Entrada,
                    Cantidad = q,
                    Fecha = ahora,
                    Motivo = $"Recepción OC-{orden.Id:D5}",
                    Proveedor = orden.Proveedor.RazonSocial,
                    PrecioUnitario = d.PrecioUnitario,
                    UsuarioId = usuarioId
                });
            }

            orden.Estado = EstadoOrdenCompra.Recibida;
            orden.RecibidoPorId = usuarioId;
            orden.FechaRecepcion = ahora;
            orden.TieneDiferencia = orden.Detalles.Any(d => d.CantidadRecibida != d.Cantidad);

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
