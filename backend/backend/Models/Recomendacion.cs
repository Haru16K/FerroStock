namespace backend.Models;

// Recomendación generada por el sistema (HU-30, 31, 34 y 35). El estado lo decide la persona:
// Pendiente -> Aplicada | Descartada. "Vencida" la marca el sistema cuando la situación que
// la originó ya no se cumple (por ejemplo, ya se repuso el stock).
public class Recomendacion
{
    public int Id { get; set; }
    public string Tipo { get; set; } = string.Empty;         // Compra | Promocion | Descontinuar
    public int ProductoId { get; set; }
    public string Clave { get; set; } = string.Empty;        // Tipo:ProductoId (evita duplicados)
    public string Titulo { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;       // un factor por línea (HU-35)
    public string Prioridad { get; set; } = "Media";         // Alta | Media | Baja

    public int? Cantidad { get; set; }                       // Compra: unidades sugeridas
    public int? ProveedorId { get; set; }                    // Compra: proveedor sugerido
    public decimal? MontoEstimado { get; set; }              // Compra: costo aproximado

    public string Estado { get; set; } = "Pendiente";
    public DateTime FechaGeneracion { get; set; }
    public DateTime? FechaGestion { get; set; }
    public int? GestionadaPorId { get; set; }

    public Producto Producto { get; set; } = null!;
    public Proveedor? Proveedor { get; set; }
    public Usuario? GestionadaPor { get; set; }
}
