using System.Net.Mail;
using System.Text.RegularExpressions;
using backend.DTOs;
using backend.Models;
using backend.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrador,Almacenero")]
public partial class ProveedoresController(IProveedorRepository repo) : ControllerBase
{
    [GeneratedRegex(@"^\d{11}$")]
    private static partial Regex RucRegex();

    // GET api/proveedores?busqueda=norte
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProveedorDto>>> GetAll([FromQuery] string? busqueda)
    {
        var lista = await repo.GetAllAsync(busqueda);
        return Ok(lista.Select(ToDto));
    }

    // GET api/proveedores/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProveedorDto>> GetById(int id)
    {
        var p = await repo.GetByIdAsync(id);
        return p is null ? NotFound() : Ok(ToDto(p));
    }

    // GET api/proveedores/por-producto/{productoId}
    [HttpGet("por-producto/{productoId:int}")]
    public async Task<ActionResult<IEnumerable<ProveedorDto>>> GetByProducto(int productoId)
    {
        var lista = await repo.GetByProductoAsync(productoId);
        return Ok(lista.Select(ToDto));
    }

    // POST api/proveedores
    [HttpPost]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<ProveedorDto>> Create([FromBody] CrearProveedorDto dto)
    {
        var error = Validar(dto.Ruc, dto.RazonSocial, dto.Email) ?? ValidarCondiciones(dto.DiasEntrega, dto.Calificacion);
        if (error is not null) return BadRequest(new { message = error });

        var ruc = dto.Ruc.Trim();
        if (await repo.ExisteRucAsync(ruc))
            return Conflict(new { message = $"Ya existe un proveedor con RUC {ruc}" });

        var proveedor = new Proveedor
        {
            Ruc = ruc,
            RazonSocial = dto.RazonSocial.Trim(),
            Contacto = Limpiar(dto.Contacto),
            Telefono = Limpiar(dto.Telefono),
            Email = Limpiar(dto.Email)?.ToLower(),
            Direccion = Limpiar(dto.Direccion),
            DiasEntrega = dto.DiasEntrega,
            Calificacion = dto.Calificacion
        };
        var creado = await repo.CreateAsync(proveedor);
        var result = await repo.GetByIdAsync(creado.Id);
        return CreatedAtAction(nameof(GetById), new { id = creado.Id }, ToDto(result!));
    }

    // PUT api/proveedores/{id}
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<ProveedorDto>> Update(int id, [FromBody] ActualizarProveedorDto dto)
    {
        var proveedor = await repo.GetByIdAsync(id);
        if (proveedor is null) return NotFound();

        var error = Validar(dto.Ruc, dto.RazonSocial, dto.Email) ?? ValidarCondiciones(dto.DiasEntrega, dto.Calificacion);
        if (error is not null) return BadRequest(new { message = error });

        var ruc = dto.Ruc.Trim();
        if (await repo.ExisteRucAsync(ruc, id))
            return Conflict(new { message = $"Ya existe un proveedor con RUC {ruc}" });

        proveedor.Ruc = ruc;
        proveedor.RazonSocial = dto.RazonSocial.Trim();
        proveedor.Contacto = Limpiar(dto.Contacto);
        proveedor.Telefono = Limpiar(dto.Telefono);
        proveedor.Email = Limpiar(dto.Email)?.ToLower();
        proveedor.Direccion = Limpiar(dto.Direccion);
        proveedor.DiasEntrega = dto.DiasEntrega;
        proveedor.Calificacion = dto.Calificacion;

        await repo.UpdateAsync(proveedor);
        return Ok(ToDto(proveedor));
    }

    // DELETE api/proveedores/{id}  (eliminación lógica)
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await repo.DesactivarAsync(id);
        return ok ? NoContent() : NotFound();
    }

    // GET api/proveedores/{id}/productos
    [HttpGet("{id:int}/productos")]
    public async Task<ActionResult<IEnumerable<ProveedorProductoDto>>> GetProductos(int id)
    {
        if (await repo.GetByIdAsync(id) is null) return NotFound();
        var productos = await repo.GetProductosAsync(id);
        return Ok(productos.Select(p => new ProveedorProductoDto(p.Id, p.Codigo, p.Nombre)));
    }

    // PUT api/proveedores/{id}/productos   body: { "productoIds": [1, 2] }
    // Reemplaza el conjunto completo de productos asociados al proveedor.
    [HttpPut("{id:int}/productos")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<IEnumerable<ProveedorProductoDto>>> AsociarProductos(
        int id, [FromBody] AsociarProductosDto dto)
    {
        try
        {
            var ok = await repo.ReemplazarProductosAsync(id, dto.ProductoIds);
            if (!ok) return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var productos = await repo.GetProductosAsync(id);
        return Ok(productos.Select(p => new ProveedorProductoDto(p.Id, p.Codigo, p.Nombre)));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static string? Validar(string? ruc, string? razonSocial, string? email)
    {
        if (string.IsNullOrWhiteSpace(ruc) || !RucRegex().IsMatch(ruc.Trim()))
            return "El RUC debe tener exactamente 11 dígitos numéricos";
        if (string.IsNullOrWhiteSpace(razonSocial))
            return "La razón social es obligatoria";
        if (!string.IsNullOrWhiteSpace(email) && !MailAddress.TryCreate(email.Trim(), out _))
            return "El correo electrónico no es válido";
        return null;
    }

    private static string? ValidarCondiciones(int diasEntrega, decimal? calificacion)
    {
        if (diasEntrega < 1 || diasEntrega > 180)
            return "Los días de entrega deben estar entre 1 y 180";
        if (calificacion is < 1m or > 5m)
            return "La calificación debe estar entre 1 y 5";
        return null;
    }

    private static string? Limpiar(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static ProveedorDto ToDto(Proveedor p) => new(
        p.Id, p.Ruc, p.RazonSocial, p.Contacto, p.Telefono, p.Email, p.Direccion,
        p.Activo, p.Productos.Count, p.DiasEntrega, p.Calificacion);
}
