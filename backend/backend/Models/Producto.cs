namespace backend.Models;

public class Producto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int CategoriaId { get; set; }
    public decimal PrecioCompra { get; set; }
    public decimal PrecioVenta { get; set; }

    // Los fija el modelo de predicción (HU-27): patrón de ventas repetido en el año
    public bool EsEstacional { get; set; }
    public int? MesPicoEstacional { get; set; }   // 1-12

    public int StockActual { get; set; }
    public int StockMinimo { get; set; }
    public bool Activo { get; set; } = true;

    // Navegación
    public Categoria Categoria { get; set; } = null!;
    public ICollection<Movimiento> Movimientos { get; set; } = new List<Movimiento>();
}