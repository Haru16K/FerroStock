namespace backend.Models;

public enum TipoMovimiento { Entrada, Salida }

public class Movimiento
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public TipoMovimiento Tipo { get; set; }
    public int Cantidad { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string? Motivo { get; set; }
    public string? Proveedor { get; set; }       // solo para Entradas
    public decimal? PrecioUnitario { get; set; } // solo para Entradas
    public int UsuarioId { get; set; }

    // Navegación
    public Producto Producto { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
}