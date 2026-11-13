using System.Security.Claims;
using backend.DTOs;
using backend.Models;
using backend.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrador,Vendedor,Gerencia")]
public class VentasController(IVentaRepository repo, IConfiguration config) : ControllerBase
{
    private int UsuarioActualId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private bool EsVendedor => User.IsInRole("Vendedor");

    // Límite de descuento autorizado. Se configura en appsettings.json:
    //   "Ventas": { "DescuentoMaximoPorcentaje": 10 }
    private decimal DescuentoMaximo =>
        config.GetValue<decimal>("Ventas:DescuentoMaximoPorcentaje", 10m);

    // GET api/ventas/configuracion
    [HttpGet("configuracion")]
    public ActionResult<ConfiguracionVentasDto> GetConfiguracion() =>
        Ok(new ConfiguracionVentasDto(DescuentoMaximo));

    // GET api/ventas/vendedores   (para el filtro por vendedor)
    [HttpGet("vendedores")]
    [Authorize(Roles = "Administrador,Gerencia")]
    public async Task<ActionResult<IEnumerable<VendedorDto>>> GetVendedores()
    {
        var usuarios = await repo.GetVendedoresAsync();
        return Ok(usuarios.Select(u => new VendedorDto(u.Id, u.Nombre, u.Activo)));
    }

    // GET api/ventas?vendedorId=&productoId=&desde=2026-01-01&hasta=2026-06-30&estado=Completada
    // Historial (HU-15). Un Vendedor solo ve sus propias ventas.
    [HttpGet]
    public async Task<ActionResult<ResumenVentasDto>> GetAll(
        [FromQuery] int? vendedorId,
        [FromQuery] int? productoId,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] EstadoVenta? estado,
        [FromQuery] int limite = 200,
        CancellationToken ct = default)
    {
        if (desde.HasValue && hasta.HasValue && desde.Value.Date > hasta.Value.Date)
            return BadRequest(new { message = "La fecha 'desde' no puede ser posterior a 'hasta'" });

        if (EsVendedor) vendedorId = UsuarioActualId;
        limite = Math.Clamp(limite, 1, 1000);

        try
        {
            var r = await repo.ListarAsync(vendedorId, productoId, desde, hasta, estado, limite, ct);
            return Ok(new ResumenVentasDto(
                r.Total, r.Completadas, r.Vendido, r.Descuentos,
                r.Items.Select(ToListadoDto).ToList(), limite));
        }
        catch (OperationCanceledException)
        {
            // El usuario cambió de filtro o de pantalla: nadie espera esta respuesta
            return StatusCode(499);
        }
    }

    // GET api/ventas/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<VentaDto>> GetById(int id)
    {
        var v = await repo.GetByIdAsync(id);
        if (v is null || (EsVendedor && v.VendedorId != UsuarioActualId))
            return NotFound();
        return Ok(ToDto(v));
    }

    // POST api/ventas
    [HttpPost]
    [Authorize(Roles = "Administrador,Vendedor")]
    public async Task<ActionResult<VentaDto>> Registrar([FromBody] CrearVentaDto dto)
    {
        var venta = new Venta
        {
            VendedorId = UsuarioActualId,
            ClienteNombre = Limpiar(dto.ClienteNombre),
            ClienteDocumento = Limpiar(dto.ClienteDocumento),
            DescuentoPorcentaje = dto.DescuentoPorcentaje,
            Detalles = (dto.Lineas ?? []).Select(l => new DetalleVenta
            {
                ProductoId = l.ProductoId,
                Cantidad = l.Cantidad
            }).ToList()
        };

        try
        {
            var creada = await repo.RegistrarAsync(venta, DescuentoMaximo);
            return CreatedAtAction(nameof(GetById), new { id = creada.Id }, ToDto(creada));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // POST api/ventas/{id}/anular   body: { "motivo": "..." }
    [HttpPost("{id:int}/anular")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<VentaDto>> Anular(int id, [FromBody] AnularVentaDto dto)
    {
        try
        {
            var anulada = await repo.AnularAsync(id, UsuarioActualId, dto?.Motivo ?? "");
            return Ok(ToDto(anulada));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private static string? Limpiar(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static VentaListadoDto ToListadoDto(VentaResumen v) => new(
        v.Id, $"V-{v.Id:D5}", v.Fecha, v.Estado.ToString(), v.VendedorId, v.VendedorNombre,
        v.ClienteNombre, v.ClienteDocumento, v.Subtotal, v.DescuentoPorcentaje, v.DescuentoMonto,
        v.Total, v.Unidades, v.Lineas, v.EsDemo);

    private static VentaDto ToDto(Venta v) => new(
        v.Id,
        $"V-{v.Id:D5}",
        v.Fecha,
        v.Estado.ToString(),
        v.VendedorId,
        v.Vendedor?.Nombre ?? "",
        v.ClienteNombre,
        v.ClienteDocumento,
        v.Subtotal,
        v.DescuentoPorcentaje,
        v.DescuentoMonto,
        v.Total,
        v.DescuentoAplicadoPor?.Nombre,
        v.AnuladoPor?.Nombre,
        v.FechaAnulacion,
        v.MotivoAnulacion,
        v.Detalles.OrderBy(d => d.Id).Select(d => new DetalleVentaDto(
            d.Id, d.ProductoId,
            d.Producto?.Codigo ?? "", d.Producto?.Nombre ?? "",
            d.Cantidad, d.PrecioUnitario, d.Subtotal
        )).ToList()
    );
}
