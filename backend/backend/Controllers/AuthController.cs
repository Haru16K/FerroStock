using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using backend.DTOs;
using backend.Repositories;
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

    private string GenerarToken(int id, string email, string rol)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, rol)
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