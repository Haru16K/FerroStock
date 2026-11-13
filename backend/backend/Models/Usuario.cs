namespace backend.Models;

// Se guarda como texto en la BD, por lo que agregar roles no requiere cambiar datos existentes
public enum RolUsuario { Administrador, Almacenero, Vendedor, Gerencia }

public class Usuario
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public RolUsuario Rol { get; set; } = RolUsuario.Almacenero;
    // Los tokens emitidos antes de este instante ya no valen (se fija al restablecer la contraseña)
    public DateTime? SesionesValidasDesde { get; set; }
    public bool Activo { get; set; } = true;

    // Navegación
    public ICollection<Movimiento> Movimientos { get; set; } = new List<Movimiento>();
}