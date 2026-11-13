using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using backend.DTOs;
using backend.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace backend.Controllers;
[ApiController]
[Route("api/[controller]")]
public class AuthController(IUsuarioRepository usuarioRepo, IConfiguration config) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<TokenResponseDto>> Login([FromBody] LoginDto dto)
    {
        var usuario = await usuarioRepo.GetByEmailAsync(dto.Email);
        if (usuario is null || !BCrypt.Net.BCrypt.Verify(dto.Password, usuario.PasswordHash))
            return Unauthorized(new { message = "Credenciales incorrectas" });

        var token = GenerarToken(usuario.Id, usuario.Email, usuario.Rol.ToString());
        return Ok(new TokenResponseDto(token, usuario.Nombre, usuario.Rol.ToString(), usuario.Id));
    }

    // GET api/auth/me  — datos vigentes del usuario (el rol puede haber cambiado desde el login)
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<PerfilDto>> Me()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var usuario = await usuarioRepo.GetByIdAsync(id);
        if (usuario is null || !usuario.Activo) return Unauthorized();
        return Ok(new PerfilDto(usuario.Id, usuario.Nombre, usuario.Rol.ToString()));
    }

    private string GenerarToken(int id, string email, string rol)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, rol),
            // Hora de emisión: permite invalidar sesiones antiguas (p. ej. al cambiar la contraseña)
            new Claim(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}