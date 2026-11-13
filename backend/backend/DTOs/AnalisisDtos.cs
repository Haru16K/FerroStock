namespace backend.DTOs;

// Los importes son NETOS de descuento. Como el descuento de una venta es un porcentaje
// uniforme sobre toda la venta, se reparte exacto entre sus líneas (por producto/categoría).

// ── Ventas por período (dashboard, HU-20) ─────────────────────────────────────
public record PuntoDiaDto(string Fecha, decimal Monto, int Ventas);

public record VentaCategoriaDto(int CategoriaId, string Categoria, decimal Monto, int Unidades);

public record ProductoVendidoDto(int ProductoId, string Codigo, string Nombre, int Unidades, decimal Monto);

public record AnalisisVentasDto(
    string Desde,
    string Hasta,
    decimal TotalVendido,
    int CantidadVentas,
    decimal TicketPromedio,
    decimal TotalDescuentos,
    int UnidadesVendidas,
    IReadOnlyList<PuntoDiaDto> PorDia,
    IReadOnlyList<VentaCategoriaDto> PorCategoria,
    IReadOnlyList<ProductoVendidoDto> TopProductos
);

// ── Rotación (HU-22) ──────────────────────────────────────────────────────────
// Rotación = unidades vendidas en el período / stock promedio del período,
// con stock promedio = (stock al inicio + stock al final) / 2, reconstruido con los movimientos.
public record RotacionProductoDto(
    int ProductoId,
    string Codigo,
    string Nombre,
    string Categoria,
    int UnidadesVendidas,
    int StockActual,
    decimal StockPromedio,
    decimal Rotacion,
    decimal? DiasCobertura        // días que alcanza el stock actual al ritmo de venta del período
);

public record ReporteRotacionDto(
    string Desde,
    string Hasta,
    int DiasPeriodo,
    IReadOnlyList<RotacionProductoDto> Productos
);

// ── Comparación mensual (HU-23) ───────────────────────────────────────────────
public record PeriodoVentasDto(string Desde, string Hasta, decimal Monto, int Unidades, int Ventas);

public record ComparacionCategoriaDto(
    int CategoriaId,
    string Categoria,
    decimal MontoActual,
    decimal MontoAnterior,
    decimal? VariacionMonto       // null = el período anterior no tiene ventas (sin base de comparación)
);

public record ComparacionMensualDto(
    string Mes,
    bool MesEnCurso,
    int? CategoriaId,
    string? CategoriaNombre,
    PeriodoVentasDto Actual,
    PeriodoVentasDto Anterior,
    decimal? VariacionMonto,
    decimal? VariacionUnidades,
    decimal? VariacionVentas,
    IReadOnlyList<ComparacionCategoriaDto> PorCategoria
);
