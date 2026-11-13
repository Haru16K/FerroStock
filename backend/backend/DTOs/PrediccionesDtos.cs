namespace backend.DTOs;

// ── Predicción por producto (HU-24, HU-25, HU-27) ─────────────────────────────
public record PrediccionProductoDto(
    int ProductoId,
    string Codigo,
    string Nombre,
    string Categoria,
    int StockActual,
    int StockMinimo,
    decimal DemandaDiaria,
    decimal Demanda30d,
    decimal Minima30d,
    decimal Maxima30d,
    int ConfianzaPct,
    string NivelConfianza,
    int DiasHistorial,
    int? DiasHastaQuiebre,
    string? FechaQuiebre,          // yyyy-MM-dd
    bool EsEstacional,
    string? MesPico,
    string Explicacion
);

public record ResumenPrediccionesDto(
    int ProductosEnRiesgo,         // quiebre estimado en 7 días o menos
    int ProductosSinHistorial,
    decimal DemandaTotal30d,
    int EstacionalesDetectados,
    IReadOnlyList<PrediccionProductoDto> Productos
);

public record PuntoSemanaDto(string Etiqueta, decimal Unidades, bool Proyectado);

public record DetallePrediccionDto(
    PrediccionProductoDto Prediccion,
    IReadOnlyList<PuntoSemanaDto> Serie
);

// ── Tendencia por categoría (HU-26) ───────────────────────────────────────────
public record TendenciaCategoriaDto(
    int CategoriaId,
    string Categoria,
    IReadOnlyList<decimal> Historico,
    IReadOnlyList<decimal> Proyeccion,
    decimal? VariacionPct,         // proyección contra el mismo número de períodos recientes
    string Tendencia               // Sube | Baja | Estable | Sin datos
);

public record TendenciaResultadoDto(
    string Horizonte,
    IReadOnlyList<string> EtiquetasHistoricas,
    IReadOnlyList<string> EtiquetasProyeccion,
    IReadOnlyList<TendenciaCategoriaDto> Categorias
);

// ── Precisión del modelo (HU-28) ──────────────────────────────────────────────
public record ProductoErrorDto(int ProductoId, string Codigo, string Nombre, decimal Mae, decimal? Mape);

public record PrecisionDto(
    decimal Mae,
    decimal? Mape,
    decimal? Wape,
    int Observaciones,
    int ProductosEvaluados,
    int SemanasEvaluadas,
    string Interpretacion,
    IReadOnlyList<ProductoErrorDto> Peores
);

// ── Actualización del modelo (HU-29) ──────────────────────────────────────────
public record EjecucionModeloDto(
    int Id,
    DateTime Inicio,
    DateTime? Fin,
    string Origen,
    string Mes,
    string Estado,
    string? Mensaje,
    int ProductosProcesados,
    decimal? Mae,
    decimal? Mape,
    int EstacionalesDetectados
);

public record EstadoModeloDto(
    EjecucionModeloDto? Ultima,
    EjecucionModeloDto? UltimaExitosa,
    bool PermitirDatosDemo,
    bool HayDatosDemo
);
