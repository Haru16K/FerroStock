using System.Security.Claims;
using backend.Data;
using backend.DTOs;
using backend.Models;
using backend.Services.Ia;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/recomendaciones")]
[Authorize]
public class RecomendacionesController(
    RecomendacionService servicio,
    ConfiguracionIaService config,
    FerroStockDbContext db) : ControllerBase
{
    private int UsuarioActualId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET api/recomendaciones?tipo=Compra&estado=Pendiente   (estado: Pendiente | Aplicada | Descartada | Vencida | Todas)
    [HttpGet]
    [Authorize(Roles = "Administrador,Gerencia")]
    public async Task<ActionResult<ListaRecomendacionesDto>> GetAll(
        [FromQuery] string? tipo, [FromQuery] string estado = "Pendiente")
    {
        var estados = new[] { "Pendiente", "Aplicada", "Descartada", "Vencida", "Todas" };
        if (!estados.Contains(estado))
            return BadRequest(new { message = $"Estado inválido. Use: {string.Join(", ", estados)}" });
        var tipos = new[] { TipoRec.Compra, TipoRec.Promocion, TipoRec.Descontinuar };
        if (!string.IsNullOrEmpty(tipo) && !tipos.Contains(tipo))
            return BadRequest(new { message = $"Tipo inválido. Use: {string.Join(", ", tipos)}" });

        var q = db.Recomendaciones.AsNoTracking()
            .Include(r => r.Producto).Include(r => r.Proveedor).Include(r => r.GestionadaPor)
            .AsQueryable();
        if (!string.IsNullOrEmpty(tipo)) q = q.Where(r => r.Tipo == tipo);
        if (estado != "Todas") q = q.Where(r => r.Estado == estado);

        var items = await q.ToListAsync();
        items = items
            .OrderBy(r => r.Prioridad == "Alta" ? 0 : r.Prioridad == "Media" ? 1 : 2)
            .ThenByDescending(r => r.FechaGeneracion)
            .ToList();

        var pendientes = await db.Recomendaciones.AsNoTracking()
            .Where(r => r.Estado == EstadoRec.Pendiente)
            .GroupBy(r => r.Tipo)
            .Select(g => new { Tipo = g.Key, N = g.Count() })
            .ToListAsync();
        int Pend(string t) => pendientes.FirstOrDefault(p => p.Tipo == t)?.N ?? 0;

        DateTime? ultima = null;
        var texto = await config.GetTextoAsync(ConfiguracionIaService.ClaveUltimaGeneracion);
        if (DateTime.TryParse(texto, null, System.Globalization.DateTimeStyles.RoundtripKind, out var f)) ultima = f;

        return Ok(new ListaRecomendacionesDto(
            pendientes.Sum(p => p.N), Pend(TipoRec.Compra), Pend(TipoRec.Promocion), Pend(TipoRec.Descontinuar),
            ultima, items.Select(ToDto).ToList()));
    }

    // POST api/recomendaciones/generar
    [HttpPost("generar")]
    [Authorize(Roles = "Administrador,Gerencia")]
    public async Task<ActionResult<ResultadoGeneracionDto>> Generar() =>
        Ok(await servicio.GenerarAsync());

    // POST api/recomendaciones/{id}/aplicar   |   POST api/recomendaciones/{id}/descartar
    [HttpPost("{id:int}/aplicar")]
    [Authorize(Roles = "Administrador,Gerencia")]
    public Task<ActionResult<RecomendacionDto>> Aplicar(int id) => Gestionar(id, EstadoRec.Aplicada);

    [HttpPost("{id:int}/descartar")]
    [Authorize(Roles = "Administrador,Gerencia")]
    public Task<ActionResult<RecomendacionDto>> Descartar(int id) => Gestionar(id, EstadoRec.Descartada);

    private async Task<ActionResult<RecomendacionDto>> Gestionar(int id, string estado)
    {
        try
        {
            await servicio.GestionarAsync(id, estado, UsuarioActualId);
            var rec = await db.Recomendaciones.AsNoTracking()
                .Include(r => r.Producto).Include(r => r.Proveedor).Include(r => r.GestionadaPor)
                .FirstAsync(r => r.Id == id);
            return Ok(ToDto(rec));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // GET api/recomendaciones/producto/{id}   (compra sugerida y proveedores, en vivo)
    [HttpGet("producto/{id:int}")]
    [Authorize(Roles = "Administrador,Gerencia")]
    public async Task<ActionResult<RecomendacionProductoDto>> GetProducto(int id)
    {
        var r = await servicio.PorProductoAsync(id);
        return r is null ? NotFound() : Ok(r);
    }

    // GET api/recomendaciones/complementarios?productoIds=1&productoIds=2   (HU-33, durante la venta)
    [HttpGet("complementarios")]
    [Authorize(Roles = "Administrador,Vendedor")]
    public async Task<ActionResult<IEnumerable<ComplementarioDto>>> GetComplementarios([FromQuery] int[]? productoIds) =>
        Ok(await servicio.ComplementariosAsync(productoIds ?? Array.Empty<int>()));

    // GET / PUT api/recomendaciones/configuracion
    [HttpGet("configuracion")]
    [Authorize(Roles = "Administrador,Gerencia")]
    public async Task<ActionResult<ConfiguracionIaDto>> GetConfiguracion() =>
        Ok(await ArmarConfiguracionAsync());

    [HttpPut("configuracion")]
    [Authorize(Roles = "Administrador,Gerencia")]
    public async Task<ActionResult<ConfiguracionIaDto>> PutConfiguracion([FromBody] GuardarConfiguracionIaDto dto)
    {
        try { await config.GuardarAsync(dto?.Valores ?? new Dictionary<string, int>()); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        return Ok(await ArmarConfiguracionAsync());
    }

    private async Task<ConfiguracionIaDto> ArmarConfiguracionAsync() => new(
        await config.GetTodoAsync(),
        ConfiguracionIaService.Parametros.ToDictionary(p => p.Key, p => new LimiteConfigDto(p.Value.Defecto, p.Value.Min, p.Value.Max)),
        MotorRecomendacion.NivelesServicio);

    private static RecomendacionDto ToDto(Recomendacion r) => new(
        r.Id, r.Tipo, r.ProductoId, r.Producto?.Codigo ?? "", r.Producto?.Nombre ?? "",
        r.Titulo, r.Motivo.Split('\n', StringSplitOptions.RemoveEmptyEntries), r.Prioridad,
        r.Cantidad, r.Proveedor?.RazonSocial, r.MontoEstimado,
        r.Estado, r.FechaGeneracion, r.FechaGestion, r.GestionadaPor?.Nombre);
}
