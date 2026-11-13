using System.Security.Claims;
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
    private int UsuarioActualId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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
        if (!Enum.TryParse<RolUsuario>(dto.Rol, true, out var rol) || !Enum.IsDefined(rol))
            return BadRequest(new { message = "Rol inválido. Use Administrador, Almacenero, Vendedor o Gerencia" });

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

    // PUT api/usuarios/{id}/rol   body: { "rol": "Vendedor" }
    // El cambio rige de inmediato: el API valida el rol contra la BD en cada petición.
    [HttpPut("{id:int}/rol")]
    public async Task<ActionResult<UsuarioDto>> CambiarRol(int id, [FromBody] CambiarRolDto dto)
    {
        if (id == UsuarioActualId)
            return BadRequest(new { message = "No puedes cambiar tu propio rol" });
        if (!Enum.TryParse<RolUsuario>(dto.Rol, true, out var rol) || !Enum.IsDefined(rol))
            return BadRequest(new { message = "Rol inválido. Use Administrador, Almacenero, Vendedor o Gerencia" });

        var usuario = await repo.GetByIdAsync(id);
        if (usuario is null) return NotFound();

        usuario.Rol = rol;
        await repo.UpdateAsync(usuario);
        return Ok(new UsuarioDto(usuario.Id, usuario.Nombre, usuario.Email, usuario.Rol.ToString(), usuario.Activo));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (id == UsuarioActualId)
            return BadRequest(new { message = "No puedes desactivar tu propia cuenta" });

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
        var cats = await db.Categorias
            .OrderBy(c => c.Nombre)
            .Select(c => new CategoriaDto(c.Id, c.Nombre, c.Descripcion, c.Productos.Count))
            .ToListAsync();
        return Ok(cats);
    }

    // POST api/categorias
    [HttpPost]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<CategoriaDto>> Create([FromBody] GuardarCategoriaDto dto)
    {
        var error = Validar(dto);
        if (error is not null) return BadRequest(new { message = error });

        var nombre = dto.Nombre.Trim();
        if (await db.Categorias.AnyAsync(c => c.Nombre == nombre))
            return Conflict(new { message = $"Ya existe una categoría llamada '{nombre}'" });

        var categoria = new Categoria
        {
            Nombre = nombre,
            Descripcion = string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim()
        };
        db.Categorias.Add(categoria);
        await db.SaveChangesAsync();
        return Ok(new CategoriaDto(categoria.Id, categoria.Nombre, categoria.Descripcion, 0));
    }

    // PUT api/categorias/{id}
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<CategoriaDto>> Update(int id, [FromBody] GuardarCategoriaDto dto)
    {
        var categoria = await db.Categorias.FindAsync(id);
        if (categoria is null) return NotFound();

        var error = Validar(dto);
        if (error is not null) return BadRequest(new { message = error });

        var nombre = dto.Nombre.Trim();
        if (await db.Categorias.AnyAsync(c => c.Nombre == nombre && c.Id != id))
            return Conflict(new { message = $"Ya existe una categoría llamada '{nombre}'" });

        categoria.Nombre = nombre;
        categoria.Descripcion = string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim();
        await db.SaveChangesAsync();

        var cantidad = await db.Productos.CountAsync(p => p.CategoriaId == id);
        return Ok(new CategoriaDto(categoria.Id, categoria.Nombre, categoria.Descripcion, cantidad));
    }

    private static string? Validar(GuardarCategoriaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto?.Nombre))
            return "El nombre de la categoría es obligatorio";
        if (dto.Nombre.Trim().Length > 100)
            return "El nombre no puede superar los 100 caracteres";
        if ((dto.Descripcion?.Trim().Length ?? 0) > 250)
            return "La descripción no puede superar los 250 caracteres";
        return null;
    }
}
