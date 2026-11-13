using System.Security.Claims;
using backend.DTOs;
using backend.Models;
using backend.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/ordenes-compra")]
[Authorize(Roles = "Administrador,Almacenero")]
public class OrdenesCompraController(IOrdenCompraRepository repo) : ControllerBase
{
    private int UsuarioActualId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET api/ordenes-compra?proveedorId=1&desde=2026-01-01&hasta=2026-06-30&estado=Recibida
    // Historial de compras por proveedor (HU-09) con monto total comprado.
    [HttpGet]
    public async Task<ActionResult<ResumenOrdenesDto>> GetAll(
        [FromQuery] int? proveedorId,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] EstadoOrdenCompra? estado)
    {
        if (desde.HasValue && hasta.HasValue && desde.Value.Date > hasta.Value.Date)
            return BadRequest(new { message = "La fecha 'desde' no puede ser posterior a 'hasta'" });

        var ordenes = (await repo.GetAllAsync(proveedorId, desde, hasta, estado)).ToList();

        var montoComprado = ordenes
            .Where(o => o.Estado == EstadoOrdenCompra.Recibida)
            .Sum(o => o.Detalles.Sum(d => d.CantidadRecibida * d.PrecioUnitario));

        return Ok(new ResumenOrdenesDto(
            ordenes.Count, montoComprado, ordenes.Select(ToDto).ToList()));
    }

    // GET api/ordenes-compra/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrdenCompraDto>> GetById(int id)
    {
        var o = await repo.GetByIdAsync(id);
        return o is null ? NotFound() : Ok(ToDto(o));
    }

    // POST api/ordenes-compra
    [HttpPost]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<OrdenCompraDto>> Create([FromBody] CrearOrdenCompraDto dto)
    {
        var orden = new OrdenCompra
        {
            ProveedorId = dto.ProveedorId,
            Observaciones = string.IsNullOrWhiteSpace(dto.Observaciones) ? null : dto.Observaciones.Trim(),
            CreadoPorId = UsuarioActualId,
            Detalles = (dto.Lineas ?? []).Select(l => new DetalleOrdenCompra
            {
                ProductoId = l.ProductoId,
                Cantidad = l.Cantidad,
                PrecioUnitario = l.PrecioUnitario
            }).ToList()
        };

        try
        {
            var creada = await repo.CrearAsync(orden);
            return CreatedAtAction(nameof(GetById), new { id = creada.Id }, ToDto(creada));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // POST api/ordenes-compra/{id}/aprobar
    [HttpPost("{id:int}/aprobar")]
    [Authorize(Roles = "Administrador")]
    public Task<ActionResult<OrdenCompraDto>> Aprobar(int id) =>
        Ejecutar(() => repo.AprobarAsync(id, UsuarioActualId));

    // POST api/ordenes-compra/{id}/rechazar   body: { "motivo": "..." }  (motivo opcional)
    [HttpPost("{id:int}/rechazar")]
    [Authorize(Roles = "Administrador")]
    public Task<ActionResult<OrdenCompraDto>> Rechazar(int id, [FromBody] RechazarOrdenDto dto) =>
        Ejecutar(() => repo.RechazarAsync(id, UsuarioActualId, dto?.Motivo));

    // POST api/ordenes-compra/{id}/cancelar   body: { "motivo": "..." }  (motivo obligatorio)
    [HttpPost("{id:int}/cancelar")]
    [Authorize(Roles = "Administrador")]
    public Task<ActionResult<OrdenCompraDto>> Cancelar(int id, [FromBody] CancelarOrdenDto dto) =>
        Ejecutar(() => repo.CancelarAsync(id, UsuarioActualId, dto?.Motivo ?? ""));

    // POST api/ordenes-compra/{id}/recibir
    // body: { "lineas": [ { "detalleId": 10, "cantidadRecibida": 8 }, ... ] }
    [HttpPost("{id:int}/recibir")]
    public async Task<ActionResult<OrdenCompraDto>> Recibir(int id, [FromBody] RecibirOrdenDto dto)
    {
        var lineas = dto?.Lineas ?? [];
        if (lineas.Select(l => l.DetalleId).Distinct().Count() != lineas.Count)
            return BadRequest(new { message = "Hay líneas repetidas en la recepción" });

        var cantidades = lineas.ToDictionary(l => l.DetalleId, l => l.CantidadRecibida);
        return await Ejecutar(() => repo.RecibirAsync(id, UsuarioActualId, cantidades));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private async Task<ActionResult<OrdenCompraDto>> Ejecutar(Func<Task<OrdenCompra>> accion)
    {
        try
        {
            return Ok(ToDto(await accion()));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                message = "La orden fue modificada por otro usuario. Recargue e intente de nuevo"
            });
        }
    }

    private static OrdenCompraDto ToDto(OrdenCompra o) => new(
        o.Id,
        $"OC-{o.Id:D5}",
        o.ProveedorId,
        o.Proveedor?.RazonSocial ?? "",
        o.Estado.ToString(),
        o.FechaCreacion,
        o.Total,
        o.Observaciones,
        o.CreadoPor?.Nombre ?? "",
        o.RevisadoPor?.Nombre,
        o.FechaRevision,
        o.MotivoRechazo,
        o.RecibidoPor?.Nombre,
        o.FechaRecepcion,
        o.TieneDiferencia,
        o.CanceladoPor?.Nombre,
        o.FechaCancelacion,
        o.MotivoCancelacion,
        o.Detalles.OrderBy(d => d.Id).Select(d => new DetalleOrdenCompraDto(
            d.Id, d.ProductoId,
            d.Producto?.Codigo ?? "", d.Producto?.Nombre ?? "",
            d.Cantidad, d.PrecioUnitario, d.Subtotal, d.CantidadRecibida
        )).ToList()
    );
}
