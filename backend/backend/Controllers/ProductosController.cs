using backend.Models;
using backend.DTOs;
using backend.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductosController(IProductoRepository repo) : ControllerBase
{
    // GET api/productos?busqueda=martillo&categoriaId=1
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductoDto>>> GetAll(
        [FromQuery] string? busqueda,
        [FromQuery] int? categoriaId)
    {
        var productos = await repo.GetAllAsync(busqueda, categoriaId);
        return Ok(productos.Select(ToDto));
    }

    // GET api/productos/criticos
    [HttpGet("criticos")]
    public async Task<ActionResult<IEnumerable<ProductoDto>>> GetCriticos()
    {
        var productos = await repo.GetCriticosAsync();
        return Ok(productos.Select(ToDto));
    }

    // GET api/productos/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductoDto>> GetById(int id)
    {
        var p = await repo.GetByIdAsync(id);
        return p is null ? NotFound() : Ok(ToDto(p));
    }

    // POST api/productos
    [HttpPost]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<ProductoDto>> Create([FromBody] CrearProductoDto dto)
    {
        if (await repo.ExisteCodigoAsync(dto.Codigo))
            return Conflict(new { message = $"El código '{dto.Codigo}' ya existe" });

        var producto = new Producto
        {
            Codigo = dto.Codigo.ToUpper().Trim(),
            Nombre = dto.Nombre.Trim(),
            CategoriaId = dto.CategoriaId,
            PrecioCompra = dto.PrecioCompra,
            StockActual = dto.StockActual,
            StockMinimo = dto.StockMinimo
        };
        var creado = await repo.CreateAsync(producto);
        var result = await repo.GetByIdAsync(creado.Id);
        return CreatedAtAction(nameof(GetById), new { id = creado.Id }, ToDto(result!));
    }

    // PUT api/productos/{id}
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<ProductoDto>> Update(int id, [FromBody] ActualizarProductoDto dto)
    {
        var producto = await repo.GetByIdAsync(id);
        if (producto is null) return NotFound();

        producto.Nombre = dto.Nombre.Trim();
        producto.CategoriaId = dto.CategoriaId;
        producto.PrecioCompra = dto.PrecioCompra;
        producto.StockMinimo = dto.StockMinimo;

        await repo.UpdateAsync(producto);
        var result = await repo.GetByIdAsync(id);
        return Ok(ToDto(result!));
    }

    // DELETE api/productos/{id}  (eliminación lógica)
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await repo.DesactivarAsync(id);
        return ok ? NoContent() : NotFound();
    }

    private static ProductoDto ToDto(Producto p) => new(
        p.Id, p.Codigo, p.Nombre, p.CategoriaId,
        p.Categoria?.Nombre ?? "",
        p.PrecioCompra, p.StockActual, p.StockMinimo, p.Activo,
        EsCritico: p.StockActual <= p.StockMinimo
    );
}