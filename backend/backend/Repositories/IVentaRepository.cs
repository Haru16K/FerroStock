using backend.Models;

namespace backend.Repositories;

public interface IVentaRepository
{
    // Historial liviano: columnas justas, sin seguimiento de cambios y con tope de filas.
    // Respeta la cancelación: si el usuario cambia de filtro o de pantalla, la consulta se aborta.
    Task<ListadoVentas> ListarAsync(
        int? vendedorId, int? productoId, DateTime? desde, DateTime? hasta,
        EstadoVenta? estado, int limite, CancellationToken ct = default);
    Task<Venta?> GetByIdAsync(int id);
    Task<IEnumerable<Usuario>> GetVendedoresAsync();

    // Lanzan KeyNotFoundException si no existe lo buscado e
    // InvalidOperationException si se rompe una regla de negocio.
    Task<Venta> RegistrarAsync(Venta venta, decimal descuentoMaximoPorcentaje);
    Task<Venta> AnularAsync(int id, int usuarioId, string motivo);
}

public record VentaResumen(
    int Id, DateTime Fecha, EstadoVenta Estado, int VendedorId, string VendedorNombre,
    string? ClienteNombre, string? ClienteDocumento, decimal Subtotal, decimal DescuentoPorcentaje,
    decimal DescuentoMonto, decimal Total, int Unidades, int Lineas, bool EsDemo);

public record ListadoVentas(
    int Total, int Completadas, decimal Vendido, decimal Descuentos, List<VentaResumen> Items);
