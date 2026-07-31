namespace backend.Models;

public enum RolUsuario { Administrador, Almacenero }

public class Usuario
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public RolUsuario Rol { get; set; } = RolUsuario.Almacenero;
    public bool Activo { get; set; } = true;

    // Navegación
    public ICollection<Movimiento> Movimientos { get; set; } = new List<Movimiento>();
}