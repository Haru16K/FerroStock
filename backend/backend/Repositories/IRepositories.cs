using backend.Models;

namespace backend.Repositories;

public interface IProductoRepository
{
    Task<IEnumerable<Producto>> GetAllAsync(string? busqueda = null, int? categoriaId = null, bool soloActivos = true);
    Task<Producto?> GetByIdAsync(int id);
    Task<Producto?> GetByCodigoAsync(string codigo);
    Task<IEnumerable<Producto>> GetCriticosAsync();
    Task<Producto> CreateAsync(Producto producto);
    Task<Producto> UpdateAsync(Producto producto);
    Task<bool> DesactivarAsync(int id);   // eliminación lógica
    Task<bool> ExisteCodigoAsync(string codigo, int? exceptoId = null);
}

public interface IMovimientoRepository
{
    Task<IEnumerable<Movimiento>> GetByProductoAsync(int productoId);
    Task<IEnumerable<Movimiento>> GetByFechasAsync(DateTime desde, DateTime hasta);
    Task<Movimiento> RegistrarEntradaAsync(Movimiento movimiento);
    Task<Movimiento> RegistrarSalidaAsync(Movimiento movimiento);
}

public interface IUsuarioRepository
{
    Task<IEnumerable<Usuario>> GetAllAsync();
    Task<Usuario?> GetByIdAsync(int id);
    Task<Usuario?> GetByEmailAsync(string email);
    Task<Usuario> CreateAsync(Usuario usuario);
    Task<Usuario> UpdateAsync(Usuario usuario);
    Task<bool> DesactivarAsync(int id);
}