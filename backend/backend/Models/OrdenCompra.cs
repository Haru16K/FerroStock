namespace backend.Models;

// Transiciones permitidas:
//   Pendiente -> Aprobada | Rechazada | Cancelada
//   Aprobada  -> Recibida | Cancelada
// Recibida, Rechazada y Cancelada son estados finales.
public enum EstadoOrdenCompra { Pendiente, Aprobada, Rechazada, Recibida, Cancelada }

public class OrdenCompra
{
    public int Id { get; set; }
    public int ProveedorId { get; set; }
    public EstadoOrdenCompra Estado { get; set; } = EstadoOrdenCompra.Pendiente;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public decimal Total { get; set; }
    public string? Observaciones { get; set; }

    public int CreadoPorId { get; set; }

    // Aprobación / rechazo (HU-07: quién y cuándo)
    public int? RevisadoPorId { get; set; }
    public DateTime? FechaRevision { get; set; }
    public string? MotivoRechazo { get; set; }

    // Recepción (HU-08)
    public int? RecibidoPorId { get; set; }
    public DateTime? FechaRecepcion { get; set; }
    public bool TieneDiferencia { get; set; }   // true si lo recibido != lo solicitado

    // Cancelación (HU-10)
    public int? CanceladoPorId { get; set; }
    public DateTime? FechaCancelacion { get; set; }
    public string? MotivoCancelacion { get; set; }

    // Evita que dos usuarios aprueben/reciban la misma orden a la vez
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navegación
    public Proveedor Proveedor { get; set; } = null!;
    public Usuario CreadoPor { get; set; } = null!;
    public Usuario? RevisadoPor { get; set; }
    public Usuario? RecibidoPor { get; set; }
    public Usuario? CanceladoPor { get; set; }
    public ICollection<DetalleOrdenCompra> Detalles { get; set; } = new List<DetalleOrdenCompra>();
}

public class DetalleOrdenCompra
{
    public int Id { get; set; }
    public int OrdenCompraId { get; set; }
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public int CantidadRecibida { get; set; }

    public decimal Subtotal => Cantidad * PrecioUnitario;   // no se mapea (solo getter)

    // Navegación
    public OrdenCompra OrdenCompra { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}
