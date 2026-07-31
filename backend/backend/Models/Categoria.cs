namespace backend.Models;

public class Categoria
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    // Navegación
    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}