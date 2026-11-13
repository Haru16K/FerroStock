namespace backend.Models;

// Completada -> Anulada (estado final)
public enum EstadoVenta { Completada, Anulada }

public class Venta
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public EstadoVenta Estado { get; set; } = EstadoVenta.Completada;
    public int VendedorId { get; set; }

    // Cliente (opcional, para el comprobante)
    public string? ClienteNombre { get; set; }
    public string? ClienteDocumento { get; set; }

    // Importes (el servidor los calcula; el cliente nunca envía precios)
    public decimal Subtotal { get; set; }
    public decimal DescuentoPorcentaje { get; set; }
    public decimal DescuentoMonto { get; set; }
    public decimal Total { get; set; }
    public int? DescuentoAplicadoPorId { get; set; }   // HU-13: quién aplicó el descuento

    // Venta ficticia para probar las predicciones: no movió stock y se puede eliminar
    public bool EsDemo { get; set; }

    // Anulación (HU-14)
    public int? AnuladoPorId { get; set; }
    public DateTime? FechaAnulacion { get; set; }
    public string? MotivoAnulacion { get; set; }

    // Navegación
    public Usuario Vendedor { get; set; } = null!;
    public Usuario? DescuentoAplicadoPor { get; set; }
    public Usuario? AnuladoPor { get; set; }
    public ICollection<DetalleVenta> Detalles { get; set; } = new List<DetalleVenta>();
}

public class DetalleVenta
{
    public int Id { get; set; }
    public int VentaId { get; set; }
    public int ProductoId { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }   // precio de venta vigente al momento de la venta

    public decimal Subtotal => Cantidad * PrecioUnitario;   // no se mapea (solo getter)

    // Navegación
    public Venta Venta { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}
