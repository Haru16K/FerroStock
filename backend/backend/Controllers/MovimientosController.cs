using backend.DTOs;
using backend.Models;
using backend.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FerroStock.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MovimientosController(IMovimientoRepository repo) : ControllerBase
{
    private int UsuarioActualId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET api/movimientos/producto/{productoId}
    [HttpGet("producto/{productoId:int}")]
    public async Task<ActionResult<IEnumerable<MovimientoDto>>> GetByProducto(int productoId)
    {
        var movs = await repo.GetByProductoAsync(productoId);
        return Ok(movs.Select(ToDto));
    }

    // GET api/movimientos?desde=2026-01-01&hasta=2026-06-30
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MovimientoDto>>> GetByFechas(
        [FromQuery] DateTime desde,
        [FromQuery] DateTime hasta)
    {
        var movs = await repo.GetByFechasAsync(desde, hasta.AddDays(1));
        return Ok(movs.Select(ToDto));
    }

    // POST api/movimientos/entrada
    [HttpPost("entrada")]
    public async Task<ActionResult<MovimientoDto>> Entrada([FromBody] RegistrarEntradaDto dto)
    {
        try
        {
            var mov = new Movimiento
            {
                ProductoId = dto.ProductoId,
                Cantidad = dto.Cantidad,
                Proveedor = dto.Proveedor,
                PrecioUnitario = dto.PrecioUnitario,
                UsuarioId = UsuarioActualId
            };
            var creado = await repo.RegistrarEntradaAsync(mov);
            return Ok(ToDto(creado));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // POST api/movimientos/salida
    [HttpPost("salida")]
    public async Task<ActionResult<MovimientoDto>> Salida([FromBody] RegistrarSalidaDto dto)
    {
        try
        {
            var mov = new Movimiento
            {
                ProductoId = dto.ProductoId,
                Cantidad = dto.Cantidad,
                Motivo = dto.Motivo,
                UsuarioId = UsuarioActualId
            };
            var creado = await repo.RegistrarSalidaAsync(mov);
            return Ok(ToDto(creado));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private static MovimientoDto ToDto(Movimiento m) => new(
        m.Id,
        m.ProductoId,
        m.Producto?.Nombre ?? "",
        m.Producto?.Codigo ?? "",
        m.Tipo.ToString(),
        m.Cantidad,
        m.Fecha,
        m.Motivo,
        m.Proveedor,
        m.PrecioUnitario,
        m.Usuario?.Nombre ?? ""
    );
}


// ── Reportes ──────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportesController(
    IProductoRepository productoRepo,
    IMovimientoRepository movimientoRepo) : ControllerBase
{
    // GET api/reportes/criticos
    [HttpGet("criticos")]
    public async Task<ActionResult<IEnumerable<ProductoCriticoDto>>> GetCriticos()
    {
        var productos = await productoRepo.GetCriticosAsync();
        var result = productos.Select(p => new ProductoCriticoDto(
            p.Id, p.Codigo, p.Nombre,
            p.Categoria?.Nombre ?? "",
            p.StockActual, p.StockMinimo,
            p.StockMinimo - p.StockActual
        ));
        return Ok(result);
    }

    // GET api/reportes/movimientos?desde=2026-01-01&hasta=2026-06-30
    [HttpGet("movimientos")]
    public async Task<ActionResult<ResumenMovimientosDto>> GetMovimientos(
        [FromQuery] DateTime desde,
        [FromQuery] DateTime hasta)
    {
        var movs = await movimientoRepo.GetByFechasAsync(desde, hasta.AddDays(1));
        var lista = movs.Select(m => new MovimientoDto(
            m.Id, m.ProductoId,
            m.Producto?.Nombre ?? "",
            m.Producto?.Codigo ?? "",
            m.Tipo.ToString(),
            m.Cantidad, m.Fecha,
            m.Motivo, m.Proveedor,
            m.PrecioUnitario,
            m.Usuario?.Nombre ?? ""
        )).ToList();

        return Ok(new ResumenMovimientosDto(
            desde, hasta,
            lista.Count(m => m.Tipo == "Entrada"),
            lista.Count(m => m.Tipo == "Salida"),
            lista
        ));
    }
}