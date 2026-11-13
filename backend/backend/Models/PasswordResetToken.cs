namespace backend.Models;

// Enlace de recuperación de contraseña (HU-19).
// Nunca se guarda el token en claro: solo su hash SHA-256, así que ni un volcado de la
// base de datos permite restablecer contraseñas ajenas.
public class PasswordResetToken
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreadoEn { get; set; }
    public DateTime ExpiraEn { get; set; }
    public DateTime? UsadoEn { get; set; }   // null = aún sin usar

    public Usuario Usuario { get; set; } = null!;
}
