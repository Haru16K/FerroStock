namespace backend.DTOs;

// ── Proveedor ─────────────────────────────────────────────────────────────────
public record ProveedorDto(
    int Id,
    string Ruc,
    string RazonSocial,
    string? Contacto,
    string? Telefono,
    string? Email,
    string? Direccion,
    bool Activo,
    int CantidadProductos,
    int DiasEntrega,
    decimal? Calificacion
);

public record CrearProveedorDto(
    string Ruc,
    string RazonSocial,
    string? Contacto,
    string? Telefono,
    string? Email,
    string? Direccion,
    int DiasEntrega = 7,
    decimal? Calificacion = null
);

public record ActualizarProveedorDto(
    string Ruc,
    string RazonSocial,
    string? Contacto,
    string? Telefono,
    string? Email,
    string? Direccion,
    int DiasEntrega = 7,
    decimal? Calificacion = null
);

public record AsociarProductosDto(IReadOnlyList<int> ProductoIds);

public record ProveedorProductoDto(int ProductoId, string Codigo, string Nombre);

// ── Orden de compra ───────────────────────────────────────────────────────────
public record LineaOrdenCompraDto(int ProductoId, int Cantidad, decimal PrecioUnitario);

public record CrearOrdenCompraDto(
    int ProveedorId,
    string? Observaciones,
    IReadOnlyList<LineaOrdenCompraDto> Lineas
);

public record DetalleOrdenCompraDto(
    int Id,
    int ProductoId,
    string ProductoCodigo,
    string ProductoNombre,
    int Cantidad,
    decimal PrecioUnitario,
    decimal Subtotal,
    int CantidadRecibida
);

public record OrdenCompraDto(
    int Id,
    string Numero,
    int ProveedorId,
    string ProveedorRazonSocial,
    string Estado,
    DateTime FechaCreacion,
    decimal Total,
    string? Observaciones,
    string CreadoPor,
    string? RevisadoPor,
    DateTime? FechaRevision,
    string? MotivoRechazo,
    string? RecibidoPor,
    DateTime? FechaRecepcion,
    bool TieneDiferencia,
    string? CanceladoPor,
    DateTime? FechaCancelacion,
    string? MotivoCancelacion,
    IReadOnlyList<DetalleOrdenCompraDto> Detalles
);

public record RechazarOrdenDto(string? Motivo);

public record CancelarOrdenDto(string Motivo);

public record LineaRecepcionDto(int DetalleId, int CantidadRecibida);

public record RecibirOrdenDto(IReadOnlyList<LineaRecepcionDto> Lineas);

// MontoTotalComprado = suma de lo efectivamente recibido (cantidad recibida x precio)
// en las órdenes en estado Recibida dentro del filtro aplicado.
public record ResumenOrdenesDto(
    int Cantidad,
    decimal MontoTotalComprado,
    IEnumerable<OrdenCompraDto> Ordenes
);
