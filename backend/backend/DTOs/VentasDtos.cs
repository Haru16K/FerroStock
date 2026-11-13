namespace backend.DTOs;

// ── Registrar venta ───────────────────────────────────────────────────────────
// El cliente solo envía producto y cantidad: el precio lo toma el servidor del catálogo.
public record LineaVentaDto(int ProductoId, int Cantidad);

public record CrearVentaDto(
    string? ClienteNombre,
    string? ClienteDocumento,
    decimal DescuentoPorcentaje,
    IReadOnlyList<LineaVentaDto> Lineas
);

public record AnularVentaDto(string Motivo);

// ── Respuestas ────────────────────────────────────────────────────────────────
public record DetalleVentaDto(
    int Id,
    int ProductoId,
    string ProductoCodigo,
    string ProductoNombre,
    int Cantidad,
    decimal PrecioUnitario,
    decimal Subtotal
);

public record VentaDto(
    int Id,
    string Numero,
    DateTime Fecha,
    string Estado,
    int VendedorId,
    string VendedorNombre,
    string? ClienteNombre,
    string? ClienteDocumento,
    decimal Subtotal,
    decimal DescuentoPorcentaje,
    decimal DescuentoMonto,
    decimal Total,
    string? DescuentoAplicadoPor,
    string? AnuladoPor,
    DateTime? FechaAnulacion,
    string? MotivoAnulacion,
    IReadOnlyList<DetalleVentaDto> Detalles
);

// Los totales cuentan solo ventas Completadas (las anuladas no suman).
public record ResumenVentasDto(
    int Cantidad,                          // ventas que cumplen los filtros (puede ser mayor que las mostradas)
    int CantidadCompletadas,
    decimal TotalVendido,
    decimal TotalDescuentos,
    IEnumerable<VentaListadoDto> Ventas,   // las más recientes, hasta 'Limite'
    int Limite
);

// Fila del historial: solo lo necesario para la tabla. El detalle (líneas, trazabilidad)
// se pide aparte con GET api/ventas/{id}.
public record VentaListadoDto(
    int Id,
    string Numero,
    DateTime Fecha,
    string Estado,
    int VendedorId,
    string VendedorNombre,
    string? ClienteNombre,
    string? ClienteDocumento,
    decimal Subtotal,
    decimal DescuentoPorcentaje,
    decimal DescuentoMonto,
    decimal Total,
    int Unidades,
    int Lineas,
    bool EsDemo
);

public record ConfiguracionVentasDto(decimal DescuentoMaximoPorcentaje);

public record VendedorDto(int Id, string Nombre, bool Activo);
