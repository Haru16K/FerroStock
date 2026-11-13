using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using backend.Data;
using backend.DTOs;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

// Recuperación de contraseña (HU-19). Endpoints públicos: el usuario no puede iniciar sesión.
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class RecuperacionController(
    FerroStockDbContext db,
    IEmailSender email,
    IConfiguration config,
    ILogger<RecuperacionController> logger) : ControllerBase
{
    private const int MinPassword = 8;
    private const int MaxPassword = 72;   // BCrypt ignora lo que pase de 72 bytes

    private int MinutosExpiracion => config.GetValue("PasswordReset:MinutosExpiracion", 30);
    private string UrlFrontend => (config["App:FrontendUrl"] ?? "http://localhost:4200").TrimEnd('/');

    // POST api/auth/olvide-password   body: { "email": "..." }
    [HttpPost("olvide-password")]
    public async Task<IActionResult> OlvidePassword([FromBody] OlvidePasswordDto dto)
    {
        var correo = (dto?.Email ?? "").Trim().ToLower();
        if (!MailAddress.TryCreate(correo, out _))
            return BadRequest(new { message = "Ingresa un correo electrónico válido" });

        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == correo && u.Activo);

        if (usuario is not null)
        {
            var ahora = DateTime.UtcNow;

            // Máximo un correo por minuto por usuario (evita usar el sistema para enviar spam)
            var reciente = await db.PasswordResetTokens
                .AnyAsync(t => t.UsuarioId == usuario.Id && t.CreadoEn > ahora.AddMinutes(-1));

            if (!reciente)
            {
                // Solo el último enlace solicitado sirve: se invalidan los anteriores
                await db.PasswordResetTokens
                    .Where(t => t.UsuarioId == usuario.Id && t.UsadoEn == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsadoEn, (DateTime?)ahora));

                var token = GenerarToken();
                db.PasswordResetTokens.Add(new PasswordResetToken
                {
                    UsuarioId = usuario.Id,
                    TokenHash = Hash(token),
                    CreadoEn = ahora,
                    ExpiraEn = ahora.AddMinutes(MinutosExpiracion)
                });
                await db.SaveChangesAsync();

                var enlace = $"{UrlFrontend}/restablecer?token={Uri.EscapeDataString(token)}";
                try
                {
                    await email.EnviarAsync(
                        usuario.Email,
                        "Restablece tu contraseña — FerroStock",
                        CuerpoHtml(usuario.Nombre, enlace),
                        CuerpoTexto(usuario.Nombre, enlace));
                }
                catch (Exception ex)
                {
                    // Un fallo de SMTP no debe revelar si la cuenta existe
                    logger.LogError(ex, "No se pudo enviar el correo de recuperación a {Email}", usuario.Email);
                }
            }
        }

        // Misma respuesta exista o no la cuenta: no se revela qué correos están registrados
        return Ok(new
        {
            message = "Si el correo está registrado, te enviamos un enlace para restablecer tu contraseña. " +
                      "Revisa tu bandeja de entrada."
        });
    }

    // POST api/auth/restablecer-password   body: { "token": "...", "nuevaPassword": "..." }
    [HttpPost("restablecer-password")]
    public async Task<IActionResult> RestablecerPassword([FromBody] RestablecerPasswordDto dto)
    {
        const string enlaceInvalido = "El enlace no es válido o ya expiró. Solicita uno nuevo.";

        if (string.IsNullOrWhiteSpace(dto?.Token))
            return BadRequest(new { message = enlaceInvalido });
        if (string.IsNullOrEmpty(dto.NuevaPassword) || dto.NuevaPassword.Length < MinPassword)
            return BadRequest(new { message = $"La contraseña debe tener al menos {MinPassword} caracteres" });
        if (dto.NuevaPassword.Length > MaxPassword)
            return BadRequest(new { message = $"La contraseña no puede superar los {MaxPassword} caracteres" });

        var hash = Hash(dto.Token.Trim());
        var ahora = DateTime.UtcNow;

        // Uso único ATÓMICO: el enlace se "consume" en un solo UPDATE condicionado, así dos
        // peticiones simultáneas con el mismo enlace no pueden cambiar la contraseña dos veces.
        var consumidos = await db.PasswordResetTokens
            .Where(t => t.TokenHash == hash && t.UsadoEn == null && t.ExpiraEn > ahora)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsadoEn, (DateTime?)ahora));

        if (consumidos == 0)
            return BadRequest(new { message = enlaceInvalido });

        var usuarioId = await db.PasswordResetTokens
            .Where(t => t.TokenHash == hash)
            .Select(t => t.UsuarioId)
            .FirstAsync();

        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId && u.Activo);
        if (usuario is null)
            return BadRequest(new { message = enlaceInvalido });

        usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NuevaPassword);
        // Cierra todas las sesiones abiertas con la clave anterior. Se trunca al segundo porque
        // el token guarda su hora de emisión en segundos: un inicio de sesión posterior sí vale.
        usuario.SesionesValidasDesde = new DateTime(
            ahora.Ticks - ahora.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
        await db.SaveChangesAsync();

        // Cualquier otro enlace pendiente de este usuario queda sin efecto
        await db.PasswordResetTokens
            .Where(t => t.UsuarioId == usuarioId && t.UsadoEn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsadoEn, (DateTime?)ahora));

        return Ok(new { message = "Tu contraseña se actualizó. Ya puedes iniciar sesión." });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    // 32 bytes aleatorios criptográficamente seguros, en base64 apto para URL
    private static string GenerarToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private string CuerpoHtml(string nombre, string enlace) => $"""
        <div style="font-family:Arial,sans-serif;font-size:14px;color:#1A1916">
          <p>Hola {WebUtility.HtmlEncode(nombre)},</p>
          <p>Recibimos una solicitud para restablecer tu contraseña de FerroStock.</p>
          <p><a href="{WebUtility.HtmlEncode(enlace)}"
                style="background:#D95F2B;color:#fff;padding:10px 18px;border-radius:6px;text-decoration:none;font-weight:bold">
                Restablecer contraseña</a></p>
          <p>El enlace se puede usar una sola vez y vence en {MinutosExpiracion} minutos.</p>
          <p style="color:#6B6963">Si no fuiste tú, ignora este mensaje: tu contraseña no cambiará.</p>
        </div>
        """;

    private string CuerpoTexto(string nombre, string enlace) =>
        $"Hola {nombre},{Environment.NewLine}Para restablecer tu contraseña de FerroStock abre este enlace " +
        $"(se usa una sola vez y vence en {MinutosExpiracion} minutos):{Environment.NewLine}{enlace}";
}
