namespace backend.Models;

public class Proveedor
{
    public int Id { get; set; }
    public string Ruc { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string? Contacto { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
    public bool Activo { get; set; } = true;

    // Condiciones comerciales: alimentan la comparación de proveedores (HU-32)
    public int DiasEntrega { get; set; } = 7;          // plazo habitual de entrega, en días
    public decimal? Calificacion { get; set; }          // 1.0 a 5.0; sin valor = no calificado

    // Navegación
    public ICollection<ProductoProveedor> Productos { get; set; } = new List<ProductoProveedor>();
    public ICollection<OrdenCompra> Ordenes { get; set; } = new List<OrdenCompra>();
}

// Relación muchos a muchos: un proveedor abastece varios productos
// y un producto puede tener varios proveedores.
public class ProductoProveedor
{
    public int ProductoId { get; set; }
    public int ProveedorId { get; set; }

    public Producto Producto { get; set; } = null!;
    public Proveedor Proveedor { get; set; } = null!;
}
