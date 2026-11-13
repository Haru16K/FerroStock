using backend.Models;

namespace backend.Repositories;

public interface IProveedorRepository
{
    Task<IEnumerable<Proveedor>> GetAllAsync(string? busqueda = null, bool soloActivos = true);
    Task<Proveedor?> GetByIdAsync(int id);
    Task<Proveedor> CreateAsync(Proveedor proveedor);
    Task<Proveedor> UpdateAsync(Proveedor proveedor);
    Task<bool> DesactivarAsync(int id);   // eliminación lógica
    Task<bool> ExisteRucAsync(string ruc, int? exceptoId = null);

    Task<IEnumerable<Producto>> GetProductosAsync(int proveedorId);
    Task<IEnumerable<Proveedor>> GetByProductoAsync(int productoId);
    // Reemplaza el conjunto de productos asociados. false si el proveedor no existe.
    Task<bool> ReemplazarProductosAsync(int proveedorId, IReadOnlyCollection<int> productoIds);
}

public interface IOrdenCompraRepository
{
    Task<IEnumerable<OrdenCompra>> GetAllAsync(
        int? proveedorId = null, DateTime? desde = null, DateTime? hasta = null,
        EstadoOrdenCompra? estado = null);
    Task<OrdenCompra?> GetByIdAsync(int id);

    // Todos estos métodos lanzan KeyNotFoundException si no existe lo que buscan
    // e InvalidOperationException si se rompe una regla de negocio.
    Task<OrdenCompra> CrearAsync(OrdenCompra orden);
    Task<OrdenCompra> AprobarAsync(int id, int usuarioId);
    Task<OrdenCompra> RechazarAsync(int id, int usuarioId, string? motivo);
    Task<OrdenCompra> CancelarAsync(int id, int usuarioId, string motivo);
    // cantidadesPorDetalle: DetalleId -> cantidad efectivamente recibida
    Task<OrdenCompra> RecibirAsync(int id, int usuarioId, IReadOnlyDictionary<int, int> cantidadesPorDetalle);
}
