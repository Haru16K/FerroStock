using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

public class ProveedorRepository(FerroStockDbContext db) : IProveedorRepository
{
    public async Task<IEnumerable<Proveedor>> GetAllAsync(string? busqueda = null, bool soloActivos = true)
    {
        var q = db.Proveedores.Include(p => p.Productos).AsQueryable();

        if (soloActivos)
            q = q.Where(p => p.Activo);
        if (!string.IsNullOrWhiteSpace(busqueda))
            q = q.Where(p => p.RazonSocial.Contains(busqueda) || p.Ruc.Contains(busqueda));

        return await q.OrderBy(p => p.RazonSocial).ToListAsync();
    }

    public Task<Proveedor?> GetByIdAsync(int id) =>
        db.Proveedores.Include(p => p.Productos).FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Proveedor> CreateAsync(Proveedor proveedor)
    {
        db.Proveedores.Add(proveedor);
        await db.SaveChangesAsync();
        return proveedor;
    }

    public async Task<Proveedor> UpdateAsync(Proveedor proveedor)
    {
        db.Proveedores.Update(proveedor);
        await db.SaveChangesAsync();
        return proveedor;
    }

    public async Task<bool> DesactivarAsync(int id)
    {
        var p = await db.Proveedores.FindAsync(id);
        if (p is null) return false;
        p.Activo = false;
        await db.SaveChangesAsync();
        return true;
    }

    public Task<bool> ExisteRucAsync(string ruc, int? exceptoId = null) =>
        db.Proveedores.AnyAsync(p => p.Ruc == ruc && (!exceptoId.HasValue || p.Id != exceptoId.Value));

    public async Task<IEnumerable<Producto>> GetProductosAsync(int proveedorId) =>
        await db.ProductoProveedores
            .Where(pp => pp.ProveedorId == proveedorId && pp.Producto.Activo)
            .Select(pp => pp.Producto)
            .OrderBy(p => p.Nombre)
            .ToListAsync();

    public async Task<IEnumerable<Proveedor>> GetByProductoAsync(int productoId) =>
        await db.Proveedores
            .Include(p => p.Productos)
            .Where(p => p.Activo && p.Productos.Any(pp => pp.ProductoId == productoId))
            .OrderBy(p => p.RazonSocial)
            .ToListAsync();

    public async Task<bool> ReemplazarProductosAsync(int proveedorId, IReadOnlyCollection<int> productoIds)
    {
        var proveedor = await db.Proveedores
            .Include(p => p.Productos)
            .FirstOrDefaultAsync(p => p.Id == proveedorId);
        if (proveedor is null) return false;

        var ids = productoIds.Distinct().ToList();

        var validos = await db.Productos
            .Where(p => ids.Contains(p.Id) && p.Activo)
            .Select(p => p.Id)
            .ToListAsync();
        var invalidos = ids.Except(validos).ToList();
        if (invalidos.Count > 0)
            throw new InvalidOperationException(
                $"Productos no encontrados o inactivos: {string.Join(", ", invalidos)}");

        var actuales = proveedor.Productos.Select(x => x.ProductoId).ToHashSet();

        foreach (var quitar in proveedor.Productos.Where(x => !ids.Contains(x.ProductoId)).ToList())
            db.ProductoProveedores.Remove(quitar);

        foreach (var id in ids.Where(i => !actuales.Contains(i)))
            db.ProductoProveedores.Add(new ProductoProveedor { ProductoId = id, ProveedorId = proveedorId });

        await db.SaveChangesAsync();
        return true;
    }
}
