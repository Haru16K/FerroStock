namespace backend.DTOs;

// ── Recomendaciones guardadas (HU-35) ─────────────────────────────────────────
public record RecomendacionDto(
    int Id,
    string Tipo,
    int ProductoId,
    string ProductoCodigo,
    string ProductoNombre,
    string Titulo,
    IReadOnlyList<string> Motivos,
    string Prioridad,
    int? Cantidad,
    string? ProveedorNombre,
    decimal? MontoEstimado,
    string Estado,
    DateTime FechaGeneracion,
    DateTime? FechaGestion,
    string? GestionadaPor
);

public record ListaRecomendacionesDto(
    int Pendientes,
    int PendientesCompra,
    int PendientesPromocion,
    int PendientesDescontinuar,
    DateTime? UltimaGeneracion,
    IReadOnlyList<RecomendacionDto> Items
);

public record ResultadoGeneracionDto(int Nuevas, int Actualizadas, int Vencidas, string Mensaje);

// ── Recomendación de compra y proveedor para un producto (HU-30, HU-32) ───────
public record ProveedorRecomendadoDto(
    int ProveedorId,
    string Proveedor,
    decimal Precio,
    bool PrecioEsReferencia,       // true = no hay compras previas: se usa el precio de compra del producto
    int DiasEntrega,
    bool EntregaEsReal,            // true = promedio de entregas reales; false = plazo registrado
    decimal? Calificacion,
    int PuntajePct,
    bool EsRecomendado,
    IReadOnlyList<string> Justificacion
);

public record RecomendacionProductoDto(
    int ProductoId,
    bool RecomendarCompra,
    int Cantidad,
    string Prioridad,
    IReadOnlyList<string> Motivos,
    decimal? MontoEstimado,
    IReadOnlyList<ProveedorRecomendadoDto> Proveedores,
    string? Aviso
);

// ── Complementarios durante una venta (HU-33) ─────────────────────────────────
public record ComplementarioDto(
    int ProductoId,
    string Codigo,
    string Nombre,
    decimal PrecioVenta,
    int StockActual,
    int VecesJuntos,
    int ConfianzaPct,
    string Motivo
);

// ── Configuración editable ────────────────────────────────────────────────────
public record LimiteConfigDto(int Defecto, int Min, int Max);

public record ConfiguracionIaDto(
    IReadOnlyDictionary<string, int> Valores,
    IReadOnlyDictionary<string, LimiteConfigDto> Limites,
    IReadOnlyList<int> NivelesServicio
);

public record GuardarConfiguracionIaDto(Dictionary<string, int> Valores);
