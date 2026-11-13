using System.Security.Claims;
using backend.Data;
using backend.DTOs;
using backend.Helpers;
using backend.Models;
using backend.Services.Ia;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/predicciones")]
[Authorize(Roles = "Administrador,Gerencia,Almacenero")]
public class PrediccionesController(
    PrediccionService servicio,
    DatosDemoService demo,
    FerroStockDbContext db,
    IConfiguration config,
    IWebHostEnvironment env) : ControllerBase
{
    private const int DiasRiesgo = 7;

    private int UsuarioActualId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Los datos de demostración y el fallo simulado solo se permiten donde se habilite
    // (por defecto, solo en Desarrollo): "Ia": { "PermitirDatosDemo": true }
    private bool PermitirDemo => config.GetValue("Ia:PermitirDatosDemo", env.IsDevelopment());

    // GET api/predicciones/estado
    [HttpGet("estado")]
    public async Task<ActionResult<EstadoModeloDto>> GetEstado()
    {
        var ultima = await db.EjecucionesModelo.AsNoTracking()
            .OrderByDescending(e => e.Inicio).FirstOrDefaultAsync();
        var exitosa = await db.EjecucionesModelo.AsNoTracking()
            .Where(e => e.Estado == "Ok").OrderByDescending(e => e.Inicio).FirstOrDefaultAsync();

        return Ok(new EstadoModeloDto(
            ultima is null ? null : ToEjecucionDto(ultima),
            exitosa is null ? null : ToEjecucionDto(exitosa),
            PermitirDemo,
            await demo.HayDatosAsync()));
    }

    // GET api/predicciones/productos   (HU-24, HU-25, HU-27)
    // Ordenados por urgencia: primero los que se quedarán sin stock antes.
    [HttpGet("productos")]
    public async Task<ActionResult<ResumenPrediccionesDto>> GetProductos()
    {
        var lista = await servicio.CalcularProductosAsync();

        var dtos = lista
            .OrderBy(p => p.Resultado.DiasHastaQuiebre ?? int.MaxValue)
            .ThenByDescending(p => p.Resultado.Demanda30d)
            .Select(ToDto)
            .ToList();

        return Ok(new ResumenPrediccionesDto(
            dtos.Count(p => p.DiasHastaQuiebre is <= DiasRiesgo),
            dtos.Count(p => p.DiasHistorial == 0),
            dtos.Sum(p => p.Demanda30d),
            dtos.Count(p => p.EsEstacional),
            dtos));
    }

    // GET api/predicciones/productos/{id}   (ficha con serie semanal y proyección)
    [HttpGet("productos/{id:int}")]
    public async Task<ActionResult<DetallePrediccionDto>> GetProducto(int id)
    {
        var p = (await servicio.CalcularProductosAsync(id)).FirstOrDefault();
        if (p is null) return NotFound();

        var serie = new List<PuntoSemanaDto>();
        var n = p.Serie.Length;
        var ayer = FechasLima.HoyLima.AddDays(-1);

        // 12 semanas históricas (la más reciente al final)
        for (var w = 11; w >= 0; w--)
        {
            var fin = n - 7 * w;   // exclusivo
            var suma = 0;
            for (var i = fin - 7; i < fin; i++) suma += p.Serie[i];
            serie.Add(new PuntoSemanaDto($"{ayer.AddDays(-7 * w - 6):dd/MM}", suma, false));
        }
        // 4 semanas proyectadas
        var porSemana = Math.Round(p.Resultado.DemandaDiaria * 7, 2);
        for (var w = 1; w <= 4; w++)
            serie.Add(new PuntoSemanaDto($"{ayer.AddDays(7 * (w - 1) + 1):dd/MM}", porSemana, true));

        return Ok(new DetallePrediccionDto(ToDto(p), serie));
    }

    // GET api/predicciones/categorias?horizonte=semanal|mensual   (HU-26)
    [HttpGet("categorias")]
    [Authorize(Roles = "Administrador,Gerencia")]
    public async Task<ActionResult<TendenciaResultadoDto>> GetCategorias([FromQuery] string horizonte = "semanal")
    {
        if (horizonte is not ("semanal" or "mensual"))
            return BadRequest(new { message = "El horizonte debe ser 'semanal' o 'mensual'" });

        var r = await servicio.TendenciaPorCategoriaAsync(horizonte == "mensual");

        var categorias = r.Categorias.Select(c =>
        {
            var reciente = c.Historico.TakeLast(c.Proyeccion.Count).Sum();
            var proyectado = c.Proyeccion.Sum();
            decimal? variacion = reciente > 0 ? Math.Round((proyectado - reciente) / reciente * 100m, 1) : null;
            var tendencia = variacion is null ? "Sin datos"
                : variacion > 5 ? "Sube" : variacion < -5 ? "Baja" : "Estable";
            return new TendenciaCategoriaDto(c.CategoriaId, c.Categoria, c.Historico, c.Proyeccion, variacion, tendencia);
        }).ToList();

        return Ok(new TendenciaResultadoDto(r.Horizonte, r.EtiquetasHistoricas, r.EtiquetasProyeccion, categorias));
    }

    // GET api/predicciones/precision   (HU-28)
    [HttpGet("precision")]
    [Authorize(Roles = "Administrador,Gerencia")]
    public async Task<ActionResult<PrecisionDto>> GetPrecision()
    {
        var r = await servicio.CalcularPrecisionAsync();

        var interpretacion = r.Observaciones == 0
            ? "Aún no hay suficiente historial para medir la precisión: hacen falta al menos 5 semanas de ventas por producto."
            : $"Comparando lo pronosticado con lo realmente vendido en las últimas {PrediccionService.SemanasBacktest} semanas, " +
              $"el modelo se desvía en promedio {r.Mae:0.##} unidades por producto y semana" +
              (r.Wape.HasValue ? $" (error ponderado por volumen: {r.Wape:0.#} %)." : ".");

        var peores = r.PorProducto
            .OrderByDescending(p => p.Mae).Take(5)
            .Select(p => new ProductoErrorDto(p.ProductoId, p.Codigo, p.Nombre, p.Mae, p.Mape))
            .ToList();

        return Ok(new PrecisionDto(r.Mae, r.Mape, r.Wape, r.Observaciones, r.ProductosEvaluados,
            PrediccionService.SemanasBacktest, interpretacion, peores));
    }

    // GET api/predicciones/ejecuciones
    [HttpGet("ejecuciones")]
    [Authorize(Roles = "Administrador,Gerencia")]
    public async Task<ActionResult<IEnumerable<EjecucionModeloDto>>> GetEjecuciones()
    {
        var lista = await db.EjecucionesModelo.AsNoTracking()
            .OrderByDescending(e => e.Inicio).Take(12).ToListAsync();
        return Ok(lista.Select(ToEjecucionDto));
    }

    // POST api/predicciones/recalcular?simularFallo=false   (HU-29, ejecución manual)
    [HttpPost("recalcular")]
    [Authorize(Roles = "Administrador")]
    public async Task<ActionResult<EjecucionModeloDto>> Recalcular([FromQuery] bool simularFallo = false)
    {
        if (simularFallo && !PermitirDemo)
            return BadRequest(new { message = "La simulación de fallos no está habilitada en este entorno." });

        var ejecucion = await servicio.ActualizarModeloAsync("Manual", simularFallo);
        return Ok(ToEjecucionDto(ejecucion));
    }

    // POST api/predicciones/demo
    [HttpPost("demo")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> GenerarDemo()
    {
        if (!PermitirDemo)
            return BadRequest(new { message = "Los datos de demostración no están habilitados en este entorno." });
        try
        {
            var ventas = await demo.GenerarAsync(UsuarioActualId);
            // Con historial nuevo se actualiza el modelo (estacionalidad y precisión)
            await servicio.ActualizarModeloAsync("Manual");
            return Ok(new { message = $"Se generaron {ventas} ventas de demostración (14 meses).", ventas });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // DELETE api/predicciones/demo
    [HttpDelete("demo")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> EliminarDemo()
    {
        if (!PermitirDemo)
            return BadRequest(new { message = "Los datos de demostración no están habilitados en este entorno." });

        var borradas = await demo.EliminarAsync();
        await servicio.ActualizarModeloAsync("Manual");
        return Ok(new { message = $"Se eliminaron {borradas} ventas de demostración.", ventas = borradas });
    }

    // ── Mapeo ─────────────────────────────────────────────────────────────────
    private static PrediccionProductoDto ToDto(ProductoPrediccion p)
    {
        var r = p.Resultado;
        string? fecha = r.DiasHastaQuiebre.HasValue
            ? FechasLima.HoyLima.AddDays(r.DiasHastaQuiebre.Value).ToString("yyyy-MM-dd")
            : null;
        string? mes = r.MesPico.HasValue ? MotorPrediccion.Meses[r.MesPico.Value - 1] : null;

        return new PrediccionProductoDto(
            p.ProductoId, p.Codigo, p.Nombre, p.Categoria, p.StockActual, p.StockMinimo,
            r.DemandaDiaria, r.Demanda30d, r.Minima30d, r.Maxima30d,
            r.ConfianzaPct, r.NivelConfianza, r.DiasHistorial,
            r.DiasHastaQuiebre, fecha, r.EsEstacional, mes, PrediccionService.Explicar(p));
    }

    private static EjecucionModeloDto ToEjecucionDto(EjecucionModelo e) => new(
        e.Id, e.Inicio, e.Fin, e.Origen, e.Mes, e.Estado, e.Mensaje,
        e.ProductosProcesados, e.Mae, e.Mape, e.EstacionalesDetectados);
}
