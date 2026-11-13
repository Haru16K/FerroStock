using backend.Data;
using backend.DTOs;
using backend.Helpers;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Ia;

public static class TipoRec
{
    public const string Compra = "Compra";
    public const string Promocion = "Promocion";
    public const string Descontinuar = "Descontinuar";
}

public static class EstadoRec
{
    public const string Pendiente = "Pendiente";
    public const string Aplicada = "Aplicada";
    public const string Descartada = "Descartada";
    public const string Vencida = "Vencida";
}

public record DatosProveedores(
    Dictionary<int, List<int>> ProveedoresDeProducto,
    Dictionary<int, (string Nombre, int DiasEntrega, decimal? Calificacion)> Info,
    Dictionary<(int Prov, int Prod), decimal> UltimoPrecio,
    Dictionary<int, int> EntregaReal,
    Dictionary<int, decimal> PrecioCompra);

public record PropuestaCompra(
    ProductoPrediccion Producto, SalidaCompra Salida, List<ProveedorPuntuado> Proveedores,
    int DiasEntrega, decimal PrecioUnitario, List<string> Motivos);

public class RecomendacionService(
    FerroStockDbContext db, PrediccionService prediccion, ConfiguracionIaService config,
    ILogger<RecomendacionService> logger)
{
    public const int PlazoPorDefecto = 7;   // días, si el producto no tiene proveedor asociado

    // ── Datos de proveedores (HU-32) ──────────────────────────────────────────
    public async Task<DatosProveedores> CargarDatosProveedoresAsync()
    {
        var asociaciones = await db.ProductoProveedores.AsNoTracking()
            .Where(pp => pp.Proveedor.Activo)
            .Select(pp => new
            {
                pp.ProductoId, pp.ProveedorId, pp.Proveedor.RazonSocial,
                pp.Proveedor.DiasEntrega, pp.Proveedor.Calificacion
            })
            .ToListAsync();

        var deProducto = asociaciones
            .GroupBy(a => a.ProductoId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.ProveedorId).Distinct().ToList());

        var info = new Dictionary<int, (string Nombre, int DiasEntrega, decimal? Calificacion)>();
        foreach (var a in asociaciones)
            info[a.ProveedorId] = (a.RazonSocial, a.DiasEntrega, a.Calificacion);

        // Último precio pagado a cada proveedor por cada producto (órdenes aprobadas o recibidas)
        var precios = await db.DetallesOrdenCompra.AsNoTracking()
            .Where(d => d.OrdenCompra.Estado == EstadoOrdenCompra.Aprobada
                        || d.OrdenCompra.Estado == EstadoOrdenCompra.Recibida)
            .Select(d => new { d.OrdenCompra.ProveedorId, d.ProductoId, d.PrecioUnitario, Fecha = d.OrdenCompra.FechaCreacion })
            .ToListAsync();
        var ultimoPrecio = precios
            .GroupBy(x => (x.ProveedorId, x.ProductoId))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Fecha).First().PrecioUnitario);

        // Plazo de entrega REAL: días entre la aprobación y la recepción de las órdenes recibidas
        var recibidas = await db.OrdenesCompra.AsNoTracking()
            .Where(o => o.Estado == EstadoOrdenCompra.Recibida && o.FechaRecepcion != null && o.FechaRevision != null)
            .Select(o => new { o.ProveedorId, o.FechaRecepcion, o.FechaRevision })
            .ToListAsync();
        var entregaReal = recibidas
            .GroupBy(o => o.ProveedorId)
            .ToDictionary(g => g.Key,
                g => (int)Math.Max(1, Math.Round(g.Average(o => (o.FechaRecepcion!.Value - o.FechaRevision!.Value).TotalDays))));

        var precioCompra = await db.Productos.AsNoTracking()
            .ToDictionaryAsync(p => p.Id, p => p.PrecioCompra);

        return new DatosProveedores(deProducto, info, ultimoPrecio, entregaReal, precioCompra);
    }

    public List<ProveedorPuntuado> EvaluarProveedores(int productoId, DatosProveedores d)
    {
        if (!d.ProveedoresDeProducto.TryGetValue(productoId, out var proveedores))
            return new List<ProveedorPuntuado>();

        var candidatos = new List<CandidatoProveedor>();
        foreach (var pid in proveedores)
        {
            var inf = d.Info[pid];
            var tienePrecio = d.UltimoPrecio.TryGetValue((pid, productoId), out var precio);
            if (!tienePrecio) precio = d.PrecioCompra.GetValueOrDefault(productoId);

            var tieneReal = d.EntregaReal.TryGetValue(pid, out var diasReales);
            candidatos.Add(new CandidatoProveedor(
                pid, inf.Nombre, precio, !tienePrecio,
                tieneReal ? diasReales : inf.DiasEntrega, tieneReal, inf.Calificacion));
        }
        return MotorRecomendacion.Puntuar(candidatos);
    }

    // ── Compra óptima (HU-30) ─────────────────────────────────────────────────
    private async Task<Dictionary<int, int>> EnCaminoAsync()
    {
        var filas = await db.DetallesOrdenCompra.AsNoTracking()
            .Where(d => d.OrdenCompra.Estado == EstadoOrdenCompra.Aprobada)
            .GroupBy(d => d.ProductoId)
            .Select(g => new { ProductoId = g.Key, Cantidad = g.Sum(x => x.Cantidad) })
            .ToListAsync();
        return filas.ToDictionary(f => f.ProductoId, f => f.Cantidad);
    }

    private PropuestaCompra ConstruirCompra(
        ProductoPrediccion p, DatosProveedores datos, Dictionary<int, int> enCamino, int cobertura, double z)
    {
        var evaluados = EvaluarProveedores(p.ProductoId, datos);
        var mejor = evaluados.FirstOrDefault()?.C;
        var plazo = mejor?.DiasEntrega ?? PlazoPorDefecto;
        var precio = mejor?.Precio ?? datos.PrecioCompra.GetValueOrDefault(p.ProductoId);
        enCamino.TryGetValue(p.ProductoId, out var camino);

        var r = p.Resultado;
        var salida = MotorRecomendacion.CalcularCompra(new EntradaCompra(
            (double)r.DemandaDiaria, (double)r.DesviacionSemanal, p.StockActual, camino,
            p.StockMinimo, plazo, cobertura, z, r.DiasHistorial > 0));

        var motivos = new List<string>();
        if (r.DiasHistorial > 0)
        {
            motivos.Add($"Demanda estimada: {r.DemandaDiaria:0.##} u/día ({r.Demanda30d:0} u en 30 días, confianza {r.NivelConfianza.ToLower()}).");
            if (r.EsEstacional && r.MesPico.HasValue)
                motivos.Add($"Producto estacional (pico en {MotorPrediccion.Meses[r.MesPico.Value - 1]}): el pronóstico ya incluye el ajuste por temporada.");
        }
        else
            motivos.Add($"Sin historial de ventas: se toma como referencia el stock mínimo ({p.StockMinimo} u).");

        string origenPlazo;
        if (mejor is null) origenPlazo = "valor por defecto: no hay proveedor asociado";
        else if (mejor.EntregaEsReal) origenPlazo = $"promedio real de {mejor.Nombre}";
        else origenPlazo = $"plazo registrado de {mejor.Nombre}";
        motivos.Add($"Plazo de entrega: {plazo} días ({origenPlazo}).");

        var textoCamino = camino > 0 ? $" + {camino} en camino" : "";
        motivos.Add($"Stock actual: {p.StockActual}{textoCamino}. Punto de reposición: {salida.PuntoReposicion:0} u (incluye {salida.StockSeguridad:0} u de seguridad).");

        if (salida.Recomendar)
        {
            motivos.Add($"Para cubrir {cobertura} días después de la entrega hay que llegar a {salida.NivelObjetivo:0} u: se sugiere comprar {salida.Cantidad} u.");
            if (salida.DiasDeStock is double dias && dias < plazo)
                motivos.Add($"Atención: el stock alcanza para {dias:0.#} días y el proveedor tarda {plazo}; habrá quiebre antes de que llegue el pedido.");
        }
        else
            motivos.Add("El stock actual cubre la demanda durante el plazo de entrega: todavía no hace falta comprar.");

        return new PropuestaCompra(p, salida, evaluados, plazo, precio, motivos);
    }

    // Recomendación en vivo para un producto (se muestra junto a su detalle)
    public async Task<RecomendacionProductoDto?> PorProductoAsync(int productoId)
    {
        var p = (await prediccion.CalcularProductosAsync(productoId)).FirstOrDefault();
        if (p is null) return null;

        var cfg = await config.GetTodoAsync();
        var datos = await CargarDatosProveedoresAsync();
        var enCamino = await EnCaminoAsync();

        var propuesta = ConstruirCompra(p, datos, enCamino,
            cfg["Compra.DiasCobertura"], MotorRecomendacion.ZDeNivel(cfg["Compra.NivelServicio"]));

        var justificacion = MotorRecomendacion.Justificar(propuesta.Proveedores);
        var proveedores = propuesta.Proveedores.Select((pp, i) => new ProveedorRecomendadoDto(
            pp.C.ProveedorId, pp.C.Nombre, pp.C.Precio, pp.C.PrecioEsReferencia,
            pp.C.DiasEntrega, pp.C.EntregaEsReal, pp.C.Calificacion,
            (int)Math.Round(pp.Puntaje * 100), i == 0,
            i == 0 ? justificacion : new List<string>())).ToList();

        string? aviso = proveedores.Count == 0
            ? "Este producto no tiene proveedores asociados. Asócialos desde Proveedores para poder compararlos."
            : null;

        decimal? monto = propuesta.Salida.Recomendar ? propuesta.Salida.Cantidad * propuesta.PrecioUnitario : null;
        return new RecomendacionProductoDto(
            productoId, propuesta.Salida.Recomendar, propuesta.Salida.Cantidad, propuesta.Salida.Prioridad,
            propuesta.Motivos, monto, proveedores, aviso);
    }

    // ── Generación de recomendaciones guardadas (HU-30, 31, 34) ───────────────
    private record Candidata(
        string Tipo, int ProductoId, string Titulo, List<string> Motivos, string Prioridad,
        int? Cantidad = null, int? ProveedorId = null, decimal? Monto = null)
    {
        public string Clave => $"{Tipo}:{ProductoId}";
    }

    public async Task<ResultadoGeneracionDto> GenerarAsync()
    {
        var cfg = await config.GetTodoAsync();
        var preds = await prediccion.CalcularProductosAsync();
        var datos = await CargarDatosProveedoresAsync();
        var enCamino = await EnCaminoAsync();
        var productos = await db.Productos.AsNoTracking().Where(p => p.Activo)
            .ToDictionaryAsync(p => p.Id);

        var candidatas = new List<Candidata>();

        // 1) Compras
        var z = MotorRecomendacion.ZDeNivel(cfg["Compra.NivelServicio"]);
        foreach (var p in preds)
        {
            var prop = ConstruirCompra(p, datos, enCamino, cfg["Compra.DiasCobertura"], z);
            if (!prop.Salida.Recomendar) continue;

            var mejor = prop.Proveedores.FirstOrDefault()?.C;
            var motivos = new List<string>(prop.Motivos);
            if (mejor is not null)
                motivos.AddRange(MotorRecomendacion.Justificar(prop.Proveedores).Select(l => $"Proveedor {mejor.Nombre}: {l}"));

            candidatas.Add(new Candidata(TipoRec.Compra, p.ProductoId,
                $"Comprar {prop.Salida.Cantidad} u de {p.Nombre}", motivos, prop.Salida.Prioridad,
                prop.Salida.Cantidad, mejor?.ProveedorId, prop.Salida.Cantidad * prop.PrecioUnitario));
        }

        // 2) Promociones
        var candidatosPromo = new List<CandidatoPromocion>();
        foreach (var p in preds)
        {
            if (!productos.TryGetValue(p.ProductoId, out var prod) || prod.PrecioVenta <= 0) continue;
            var u30 = p.Serie.TakeLast(30).Sum();
            var margen = (double)((prod.PrecioVenta - prod.PrecioCompra) / prod.PrecioVenta * 100m);
            candidatosPromo.Add(new CandidatoPromocion(p.ProductoId, u30, margen, p.StockActual, (double)p.Resultado.DemandaDiaria));
        }
        foreach (var r in MotorRecomendacion.Promocionables(candidatosPromo, cfg["Promocion.Cantidad"]))
        {
            var prod = productos[r.C.ProductoId];
            var diasStock = r.C.DemandaDiaria > 0 ? r.C.Stock / r.C.DemandaDiaria : 0;
            candidatas.Add(new Candidata(TipoRec.Promocion, prod.Id, $"Promocionar {prod.Nombre}",
                new List<string>
                {
                    $"Rotación alta: {r.C.Unidades30d} u vendidas en 30 días (supera al {r.PctRotacion * 100:0} % de los productos evaluados).",
                    $"Buen margen: {r.C.MargenPct:0.#} % (S/ {prod.PrecioVenta - prod.PrecioCompra:0.00} por unidad; supera al {r.PctMargen * 100:0} %).",
                    $"Stock suficiente: {r.C.Stock} u (alcanza para unos {diasStock:0} días de venta)."
                },
                r.Puntaje >= 0.85 ? "Alta" : "Media"));
        }

        // 3) Descontinuar
        candidatas.AddRange(await CandidatasDescontinuarAsync(cfg, productos));

        return await GuardarCandidatasAsync(candidatas);
    }

    private async Task<List<Candidata>> CandidatasDescontinuarAsync(
        Dictionary<string, int> cfg, Dictionary<int, Producto> productos)
    {
        var umbral = cfg["Descontinuar.UmbralUnidades"];
        var ventana = cfg["Descontinuar.DiasVentana"];
        var diasSin = cfg["Descontinuar.DiasSinMovimiento"];
        var ahora = DateTime.UtcNow;
        var desde = ahora.AddDays(-ventana);

        var vendidas = (await db.DetallesVenta.AsNoTracking()
                .Where(d => d.Venta.Estado == EstadoVenta.Completada && d.Venta.Fecha >= desde)
                .GroupBy(d => d.ProductoId)
                .Select(g => new { ProductoId = g.Key, Unidades = g.Sum(x => x.Cantidad) })
                .ToListAsync())
            .ToDictionary(x => x.ProductoId, x => x.Unidades);

        var ultimosMov = (await db.Movimientos.AsNoTracking()
                .GroupBy(m => m.ProductoId)
                .Select(g => new { ProductoId = g.Key, Ultima = g.Max(m => m.Fecha) })
                .ToListAsync())
            .ToDictionary(x => x.ProductoId, x => x.Ultima);

        var ultimasVentas = (await db.DetallesVenta.AsNoTracking()
                .Where(d => d.Venta.Estado == EstadoVenta.Completada)
                .GroupBy(d => d.ProductoId)
                .Select(g => new { ProductoId = g.Key, Ultima = g.Max(x => x.Venta.Fecha) })
                .ToListAsync())
            .ToDictionary(x => x.ProductoId, x => x.Ultima);

        var lista = new List<Candidata>();
        foreach (var prod in productos.Values)
        {
            if (prod.StockActual <= 0) continue;   // sin stock no hay nada que liberar

            // Último movimiento = lo más reciente entre movimientos de stock y ventas.
            // Un producto sin ningún movimiento no se evalúa: se desconoce su antigüedad.
            DateTime? ultima = null;
            if (ultimosMov.TryGetValue(prod.Id, out var m)) ultima = m;
            if (ultimasVentas.TryGetValue(prod.Id, out var v) && (ultima is null || v > ultima)) ultima = v;
            if (ultima is null) continue;

            var unidades = vendidas.GetValueOrDefault(prod.Id);
            var dias = (int)(ahora - ultima.Value).TotalDays;
            if (unidades >= umbral || dias < diasSin) continue;

            var inmovilizado = prod.StockActual * prod.PrecioCompra;
            lista.Add(new Candidata(TipoRec.Descontinuar, prod.Id, $"Evaluar descontinuar {prod.Nombre}",
                new List<string>
                {
                    $"Baja rotación: {unidades} u vendidas en los últimos {ventana} días (umbral configurado: menos de {umbral}).",
                    $"Lleva {dias} días sin movimiento (último: {FechasLima.AHoraLima(ultima.Value):dd/MM/yyyy}).",
                    $"Stock inmovilizado: {prod.StockActual} u (S/ {inmovilizado:0.00} a precio de compra)."
                },
                dias >= diasSin * 2 ? "Alta" : "Media"));
        }
        return lista;
    }

    // Guarda las candidatas sin duplicar y sin volver a molestar con lo que ya se gestionó
    private async Task<ResultadoGeneracionDto> GuardarCandidatasAsync(List<Candidata> candidatas)
    {
        var ahora = DateTime.UtcNow;
        var limite = ahora.AddDays(-30);
        int nuevas = 0, actualizadas = 0, vencidas = 0;

        var existentes = await db.Recomendaciones
            .Where(r => r.Estado == EstadoRec.Pendiente || (r.FechaGestion != null && r.FechaGestion >= limite))
            .ToListAsync();

        var clavesActuales = candidatas.Select(c => c.Clave).ToHashSet();

        foreach (var c in candidatas)
        {
            var pendiente = existentes.FirstOrDefault(r => r.Clave == c.Clave && r.Estado == EstadoRec.Pendiente);
            if (pendiente is not null)
            {
                Aplicar(pendiente, c, ahora);
                actualizadas++;
                continue;
            }

            // Ya se aplicó o descartó hace menos de 30 días: no se vuelve a proponer
            if (existentes.Any(r => r.Clave == c.Clave
                                    && (r.Estado == EstadoRec.Aplicada || r.Estado == EstadoRec.Descartada)))
                continue;

            var nueva = new Recomendacion { Tipo = c.Tipo, ProductoId = c.ProductoId, Clave = c.Clave, Estado = EstadoRec.Pendiente };
            Aplicar(nueva, c, ahora);
            db.Recomendaciones.Add(nueva);
            nuevas++;
        }

        // Pendientes cuya situación ya no se cumple: se marcan vencidas
        foreach (var r in existentes.Where(r => r.Estado == EstadoRec.Pendiente && !clavesActuales.Contains(r.Clave)))
        {
            r.Estado = EstadoRec.Vencida;
            r.FechaGestion = ahora;
            vencidas++;
        }

        await db.SaveChangesAsync();
        await config.GuardarTextoAsync(ConfiguracionIaService.ClaveUltimaGeneracion, ahora.ToString("O"));

        logger.LogInformation("Recomendaciones generadas: {Nuevas} nuevas, {Act} actualizadas, {Venc} vencidas", nuevas, actualizadas, vencidas);
        return new ResultadoGeneracionDto(nuevas, actualizadas, vencidas,
            $"{nuevas} recomendaciones nuevas, {actualizadas} actualizadas y {vencidas} vencidas.");
    }

    private static void Aplicar(Recomendacion r, Candidata c, DateTime ahora)
    {
        r.Titulo = c.Titulo.Length > 200 ? c.Titulo[..200] : c.Titulo;
        r.Motivo = string.Join("\n", c.Motivos);
        r.Prioridad = c.Prioridad;
        r.Cantidad = c.Cantidad;
        r.ProveedorId = c.ProveedorId;
        r.MontoEstimado = c.Monto.HasValue ? Math.Round(c.Monto.Value, 2) : null;
        r.FechaGeneracion = ahora;
    }

    // ── Aplicar o descartar (HU-35) ───────────────────────────────────────────
    public async Task<Recomendacion> GestionarAsync(int id, string nuevoEstado, int usuarioId)
    {
        var rec = await db.Recomendaciones.FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new KeyNotFoundException("Recomendación no encontrada");
        if (rec.Estado != EstadoRec.Pendiente)
            throw new InvalidOperationException($"Esta recomendación ya está {rec.Estado.ToLower()}.");

        rec.Estado = nuevoEstado;
        rec.FechaGestion = DateTime.UtcNow;
        rec.GestionadaPorId = usuarioId;
        await db.SaveChangesAsync();
        return rec;
    }

    // ── Complementarios durante una venta (HU-33) ─────────────────────────────
    public async Task<List<ComplementarioDto>> ComplementariosAsync(IReadOnlyCollection<int> productoIds)
    {
        var baseIds = productoIds.Distinct().ToList();
        if (baseIds.Count == 0) return new List<ComplementarioDto>();

        var desde = FechasLima.InicioDiaUtc(FechasLima.HoyLima.AddDays(-365));

        // Solo las ventas del último año que contienen alguno de los productos de la venta actual
        var ventasConBase = db.DetallesVenta
            .Where(d => baseIds.Contains(d.ProductoId)
                        && d.Venta.Estado == EstadoVenta.Completada && d.Venta.Fecha >= desde)
            .Select(d => d.VentaId)
            .Distinct();

        var lineas = await db.DetallesVenta.AsNoTracking()
            .Where(d => ventasConBase.Contains(d.VentaId))
            .Select(d => new { d.VentaId, d.ProductoId })
            .ToListAsync();

        var complementos = MotorRecomendacion.Complementarios(
            lineas.Select(l => (l.VentaId, l.ProductoId)).ToList(), baseIds);
        if (complementos.Count == 0) return new List<ComplementarioDto>();

        // Solo productos del catálogo, activos, con stock y precio de venta
        var ids = complementos.Select(c => c.ProductoId).Concat(complementos.Select(c => c.ProductoBaseId)).Distinct().ToList();
        var productos = await db.Productos.AsNoTracking()
            .Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        return complementos
            .Where(c => productos.TryGetValue(c.ProductoId, out var p) && p.Activo && p.StockActual > 0 && p.PrecioVenta > 0)
            .Take(3)
            .Select(c =>
            {
                var p = productos[c.ProductoId];
                var baseNombre = productos.TryGetValue(c.ProductoBaseId, out var b) ? b.Nombre : "otro producto de la venta";
                return new ComplementarioDto(p.Id, p.Codigo, p.Nombre, p.PrecioVenta, p.StockActual,
                    c.VecesJuntos, (int)Math.Round(c.Confianza * 100),
                    $"Se vendió junto a {baseNombre} en {c.VecesJuntos} de {c.VentasBase} ventas ({c.Confianza * 100:0} %).");
            })
            .ToList();
    }
}
