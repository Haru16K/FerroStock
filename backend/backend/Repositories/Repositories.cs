using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

// ── Producto ──────────────────────────────────────────────────────────────────
public class ProductoRepository(FerroStockDbContext db) : IProductoRepository
{
    public async Task<IEnumerable<Producto>> GetAllAsync(
        string? busqueda = null, int? categoriaId = null, bool soloActivos = true)
    {
        var q = db.Productos.Include(p => p.Categoria).AsQueryable();

        if (soloActivos)
            q = q.Where(p => p.Activo);
        if (categoriaId.HasValue)
            q = q.Where(p => p.CategoriaId == categoriaId.Value);
        if (!string.IsNullOrWhiteSpace(busqueda))
            q = q.Where(p => p.Nombre.Contains(busqueda) || p.Codigo.Contains(busqueda));

        return await q.OrderBy(p => p.Nombre).ToListAsync();
    }

    public Task<Producto?> GetByIdAsync(int id) =>
        db.Productos.Include(p => p.Categoria).FirstOrDefaultAsync(p => p.Id == id);

    public Task<Producto?> GetByCodigoAsync(string codigo) =>
        db.Productos.Include(p => p.Categoria).FirstOrDefaultAsync(p => p.Codigo == codigo);

    public async Task<IEnumerable<Producto>> GetCriticosAsync() =>
        await db.Productos
            .Include(p => p.Categoria)
            .Where(p => p.Activo && p.StockActual <= p.StockMinimo)
            .OrderBy(p => p.StockActual)
            .ToListAsync();

    public async Task<Producto> CreateAsync(Producto producto)
    {
        db.Productos.Add(producto);
        await db.SaveChangesAsync();
        return producto;
    }

    public async Task<Producto> UpdateAsync(Producto producto)
    {
        db.Productos.Update(producto);
        await db.SaveChangesAsync();
        return producto;
    }

    public async Task<bool> DesactivarAsync(int id)
    {
        var p = await db.Productos.FindAsync(id);
        if (p is null) return false;
        p.Activo = false;
        await db.SaveChangesAsync();
        return true;
    }

    public Task<bool> ExisteCodigoAsync(string codigo, int? exceptoId = null) =>
        db.Productos.AnyAsync(p => p.Codigo == codigo && (!exceptoId.HasValue || p.Id != exceptoId.Value));
}

// ── Movimiento ────────────────────────────────────────────────────────────────
public class MovimientoRepository(FerroStockDbContext db) : IMovimientoRepository
{
    public async Task<IEnumerable<Movimiento>> GetByProductoAsync(int productoId) =>
        await db.Movimientos
            .Include(m => m.Producto)
            .Include(m => m.Usuario)
            .Where(m => m.ProductoId == productoId)
            .OrderByDescending(m => m.Fecha)
            .ToListAsync();

    public async Task<IEnumerable<Movimiento>> GetByFechasAsync(DateTime desde, DateTime hasta) =>
        await db.Movimientos
            .Include(m => m.Producto).ThenInclude(p => p.Categoria)
            .Include(m => m.Usuario)
            .Where(m => m.Fecha >= desde && m.Fecha <= hasta)
            .OrderByDescending(m => m.Fecha)
            .ToListAsync();

    public async Task<Movimiento> RegistrarEntradaAsync(Movimiento movimiento)
    {
        // Transacción: guarda movimiento + actualiza stock
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var producto = await db.Productos.FindAsync(movimiento.ProductoId)
                ?? throw new InvalidOperationException("Producto no encontrado");

            movimiento.Tipo = TipoMovimiento.Entrada;
            producto.StockActual += movimiento.Cantidad;

            db.Movimientos.Add(movimiento);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return movimiento;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<Movimiento> RegistrarSalidaAsync(Movimiento movimiento)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var producto = await db.Productos.FindAsync(movimiento.ProductoId)
                ?? throw new InvalidOperationException("Producto no encontrado");

            if (producto.StockActual < movimiento.Cantidad)
                throw new InvalidOperationException(
                    $"Stock insuficiente. Disponible: {producto.StockActual}");

            movimiento.Tipo = TipoMovimiento.Salida;
            producto.StockActual -= movimiento.Cantidad;

            db.Movimientos.Add(movimiento);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return movimiento;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}

// ── Usuario ───────────────────────────────────────────────────────────────────
public class UsuarioRepository(FerroStockDbContext db) : IUsuarioRepository
{
    public async Task<IEnumerable<Usuario>> GetAllAsync() =>
        await db.Usuarios.OrderBy(u => u.Nombre).ToListAsync();

    public Task<Usuario?> GetByIdAsync(int id) =>
        db.Usuarios.FindAsync(id).AsTask();

    public Task<Usuario?> GetByEmailAsync(string email) =>
        db.Usuarios.FirstOrDefaultAsync(u => u.Email == email && u.Activo);

    public async Task<Usuario> CreateAsync(Usuario usuario)
    {
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        return usuario;
    }

    public async Task<bool> DesactivarAsync(int id)
    {
        var u = await db.Usuarios.FindAsync(id);
        if (u is null) return false;
        u.Activo = false;
        await db.SaveChangesAsync();
        return true;
    }
}