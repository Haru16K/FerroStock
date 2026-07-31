namespace backend.DTOs;

// ── Auth ──────────────────────────────────────────────────────────────────────
public record LoginDto(string Email, string Password);

public record TokenResponseDto(string Token, string Nombre, string Rol, int UsuarioId);

// ── Categoria ─────────────────────────────────────────────────────────────────
public record CategoriaDto(int Id, string Nombre, string? Descripcion);

// ── Producto ──────────────────────────────────────────────────────────────────
public record ProductoDto(
    int Id,
    string Codigo,
    string Nombre,
    int CategoriaId,
    string CategoriaNombre,
    decimal PrecioCompra,
    int StockActual,
    int StockMinimo,
    bool Activo,
    bool EsCritico          // StockActual <= StockMinimo
);

public record CrearProductoDto(
    string Codigo,
    string Nombre,
    int CategoriaId,
    decimal PrecioCompra,
    int StockActual,
    int StockMinimo
);

public record ActualizarProductoDto(
    string Nombre,
    int CategoriaId,
    decimal PrecioCompra,
    int StockMinimo
);

// ── Movimiento ────────────────────────────────────────────────────────────────
public record MovimientoDto(
    int Id,
    int ProductoId,
    string ProductoNombre,
    string ProductoCodigo,
    string Tipo,
    int Cantidad,
    DateTime Fecha,
    string? Motivo,
    string? Proveedor,
    decimal? PrecioUnitario,
    string UsuarioNombre
);

public record RegistrarEntradaDto(
    int ProductoId,
    int Cantidad,
    string Proveedor,
    decimal PrecioUnitario
);

public record RegistrarSalidaDto(
    int ProductoId,
    int Cantidad,
    string Motivo
);

// ── Usuario ───────────────────────────────────────────────────────────────────
public record UsuarioDto(int Id, string Nombre, string Email, string Rol, bool Activo);

public record CrearUsuarioDto(string Nombre, string Email, string Password, string Rol);

// ── Reportes ──────────────────────────────────────────────────────────────────
public record ProductoCriticoDto(
    int Id,
    string Codigo,
    string Nombre,
    string Categoria,
    int StockActual,
    int StockMinimo,
    int Diferencia
);

public record ResumenMovimientosDto(
    DateTime Desde,
    DateTime Hasta,
    int TotalEntradas,
    int TotalSalidas,
    IEnumerable<MovimientoDto> Movimientos
);