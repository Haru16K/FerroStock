using backend.Data;
using backend.DTOs;
using backend.Models;
using backend.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FerroStock.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrador")]
public class UsuariosController(IUsuarioRepository repo) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UsuarioDto>>> GetAll()
    {
        var usuarios = await repo.GetAllAsync();
        return Ok(usuarios.Select(u => new UsuarioDto(
            u.Id, u.Nombre, u.Email, u.Rol.ToString(), u.Activo)));
    }

    [HttpPost]
    public async Task<ActionResult<UsuarioDto>> Create([FromBody] CrearUsuarioDto dto)
    {
        if (!Enum.TryParse<RolUsuario>(dto.Rol, out var rol))
            return BadRequest(new { message = "Rol inválido. Use Administrador o Almacenero" });

        var usuario = new Usuario
        {
            Nombre = dto.Nombre.Trim(),
            Email = dto.Email.ToLower().Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Rol = rol
        };
        var creado = await repo.CreateAsync(usuario);
        return CreatedAtAction(null, new UsuarioDto(
            creado.Id, creado.Nombre, creado.Email, creado.Rol.ToString(), creado.Activo));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await repo.DesactivarAsync(id);
        return ok ? NoContent() : NotFound();
    }
}


// ── Categorías ────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoriasController(FerroStockDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoriaDto>>> GetAll()
    {
        var cats = await db.Categorias.OrderBy(c => c.Nombre).ToListAsync();
        return Ok(cats.Select(c => new CategoriaDto(c.Id, c.Nombre, c.Descripcion)));
    }
}