namespace backend.Services.Ia;

// Reglas de recomendación, puras y sin base de datos (reproducibles y fáciles de probar).

// ── HU-30: cantidad óptima a comprar ──────────────────────────────────────────
public record EntradaCompra(
    double DemandaDiaria, double DesviacionSemanal, int Stock, int EnCamino, int StockMinimo,
    int DiasEntrega, int DiasCobertura, double Z, bool TieneHistorial);

public record SalidaCompra(
    bool Recomendar, int Cantidad, double PuntoReposicion, double StockSeguridad,
    double NivelObjetivo, double? DiasDeStock, string Prioridad);

// ── HU-32: proveedor más conveniente ──────────────────────────────────────────
public record CandidatoProveedor(
    int ProveedorId, string Nombre, decimal Precio, bool PrecioEsReferencia,
    int DiasEntrega, bool EntregaEsReal, decimal? Calificacion);

public record ProveedorPuntuado(CandidatoProveedor C, double Puntaje);

// ── HU-31: productos a promover ───────────────────────────────────────────────
public record CandidatoPromocion(int ProductoId, int Unidades30d, double MargenPct, int Stock, double DemandaDiaria);
public record ResultadoPromocion(CandidatoPromocion C, double PctRotacion, double PctMargen, double Puntaje);

// ── HU-33: complementarios ────────────────────────────────────────────────────
public record Complemento(int ProductoId, int VecesJuntos, double Confianza, int ProductoBaseId, int VentasBase);

public static class MotorRecomendacion
{
    public static readonly int[] NivelesServicio = { 80, 85, 90, 95, 97, 99 };

    // Factor z (distribución normal) de cada nivel de servicio
    public static double ZDeNivel(int nivel) => nivel switch
    {
        80 => 0.84, 85 => 1.04, 90 => 1.28, 95 => 1.645, 97 => 1.88, 99 => 2.33, _ => 1.28
    };

    // Política de reposición con punto de pedido:
    //   stock de seguridad  = z x desviación diaria x raíz(plazo de entrega)
    //   punto de reposición = demanda x plazo + stock de seguridad
    //   nivel objetivo      = demanda x (plazo + días de cobertura) + stock de seguridad
    // Se pide cuando (stock + lo que ya viene en camino) <= punto de reposición, y la cantidad
    // es lo que falta para llegar al nivel objetivo.
    public static SalidaCompra CalcularCompra(EntradaCompra e)
    {
        var posicion = e.Stock + e.EnCamino;

        // Sin historial de ventas no hay demanda que proyectar: se usa el stock mínimo como referencia
        if (!e.TieneHistorial)
        {
            if (e.StockMinimo > 0 && posicion <= e.StockMinimo)
            {
                var objetivoMin = e.StockMinimo * 2;
                return new SalidaCompra(true, objetivoMin - posicion, e.StockMinimo, 0, objetivoMin,
                    null, e.Stock <= 0 ? "Media" : "Baja");
            }
            return new SalidaCompra(false, 0, e.StockMinimo, 0, e.StockMinimo * 2, null, "Baja");
        }

        var d = Math.Max(0, e.DemandaDiaria);
        var sigmaDia = Math.Max(0, e.DesviacionSemanal) / Math.Sqrt(7);
        var plazo = Math.Max(1, e.DiasEntrega);

        var seguridad = e.Z * sigmaDia * Math.Sqrt(plazo);
        var puntoReposicion = d * plazo + seguridad;
        var objetivo = d * (plazo + e.DiasCobertura) + seguridad;

        double? diasDeStock = d > 0 ? e.Stock / d : null;

        if (posicion > puntoReposicion)
            return new SalidaCompra(false, 0, puntoReposicion, seguridad, objetivo, diasDeStock, "Baja");

        var cantidad = (int)Math.Ceiling(objetivo - posicion);
        if (cantidad <= 0)
            return new SalidaCompra(false, 0, puntoReposicion, seguridad, objetivo, diasDeStock, "Baja");

        // Urgencia: ¿se agota antes de que pueda llegar un pedido?
        string prioridad;
        if (diasDeStock is null) prioridad = e.Stock <= 0 ? "Alta" : "Baja";
        else if (diasDeStock < plazo) prioridad = "Alta";
        else if (diasDeStock <= plazo + 7) prioridad = "Media";
        else prioridad = "Baja";

        return new SalidaCompra(true, cantidad, puntoReposicion, seguridad, objetivo, diasDeStock, prioridad);
    }

    // ── Proveedores ───────────────────────────────────────────────────────────
    public const double PesoPrecio = 0.5;
    public const double PesoEntrega = 0.3;
    public const double PesoCalificacion = 0.2;
    public const decimal CalificacionNeutra = 3.0m;   // si el proveedor no está calificado

    // Cada factor se normaliza de 0 a 1 (1 = el mejor de los candidatos) y se pondera.
    public static List<ProveedorPuntuado> Puntuar(IReadOnlyList<CandidatoProveedor> cs)
    {
        if (cs.Count == 0) return new();

        var precios = cs.Where(c => c.Precio > 0).Select(c => c.Precio).ToList();
        var minPrecio = precios.Count > 0 ? precios.Min() : 0m;
        var minEntrega = cs.Min(c => Math.Max(1, c.DiasEntrega));

        return cs.Select(c =>
        {
            var sPrecio = c.Precio > 0 && minPrecio > 0 ? (double)(minPrecio / c.Precio) : 1.0;
            var sEntrega = (double)minEntrega / Math.Max(1, c.DiasEntrega);
            var sCalif = (double)((c.Calificacion ?? CalificacionNeutra) / 5m);
            return new ProveedorPuntuado(c,
                PesoPrecio * sPrecio + PesoEntrega * sEntrega + PesoCalificacion * sCalif);
        })
        .OrderByDescending(p => p.Puntaje)
        .ThenBy(p => p.C.Precio)
        .ToList();
    }

    // Explica en lenguaje claro por qué ganó el primero, factor por factor
    public static List<string> Justificar(IReadOnlyList<ProveedorPuntuado> ordenados)
    {
        var lineas = new List<string>();
        if (ordenados.Count == 0) return lineas;

        var mejor = ordenados[0].C;
        var otros = ordenados.Skip(1).Select(o => o.C).ToList();

        if (otros.Count == 0)
        {
            lineas.Add("Es el único proveedor asociado a este producto.");
            return lineas;
        }

        // Precio
        var masBarato = ordenados.Select(o => o.C).Where(c => c.Precio > 0).OrderBy(c => c.Precio).FirstOrDefault();
        if (masBarato is null) lineas.Add("Ninguno tiene precio registrado, así que el precio no pesó en la comparación.");
        else if (masBarato.ProveedorId == mejor.ProveedorId)
        {
            var siguiente = otros.Where(c => c.Precio > 0).OrderBy(c => c.Precio).FirstOrDefault();
            lineas.Add(siguiente is null
                ? $"Precio: S/ {mejor.Precio:0.00}."
                : $"Tiene el precio más bajo: S/ {mejor.Precio:0.00} ({Pct(siguiente.Precio, mejor.Precio):0.#} % menos que {siguiente.Nombre}).");
        }
        else
            lineas.Add($"Su precio (S/ {mejor.Precio:0.00}) es {Pct(mejor.Precio, masBarato.Precio):0.#} % mayor que el más barato ({masBarato.Nombre}, S/ {masBarato.Precio:0.00}), pero compensa en otros factores.");

        // Entrega
        var masRapido = ordenados.Select(o => o.C).OrderBy(c => c.DiasEntrega).First();
        if (masRapido.ProveedorId == mejor.ProveedorId)
        {
            var masLento = otros.OrderByDescending(c => c.DiasEntrega).First();
            lineas.Add(masLento.DiasEntrega > mejor.DiasEntrega
                ? $"Es de los más rápidos: entrega en {mejor.DiasEntrega} días ({masLento.DiasEntrega - mejor.DiasEntrega} menos que {masLento.Nombre})."
                : $"Entrega en {mejor.DiasEntrega} días, igual que los demás.");
        }
        else
            lineas.Add($"Entrega en {mejor.DiasEntrega} días ({mejor.DiasEntrega - masRapido.DiasEntrega} más que {masRapido.Nombre}).");

        // Calificación
        var mejorCalif = ordenados.Select(o => o.C).Where(c => c.Calificacion.HasValue)
            .OrderByDescending(c => c.Calificacion).FirstOrDefault();
        if (mejor.Calificacion is null)
            lineas.Add($"No tiene calificación registrada (se asumió {CalificacionNeutra:0.0} de 5 como valor neutro).");
        else if (mejorCalif is not null && mejorCalif.ProveedorId == mejor.ProveedorId)
            lineas.Add($"Es el mejor calificado: {mejor.Calificacion:0.0} de 5.");
        else if (mejorCalif is not null)
            lineas.Add($"Calificación {mejor.Calificacion:0.0} de 5 ({mejorCalif.Nombre} tiene {mejorCalif.Calificacion:0.0}).");

        lineas.Add($"Puntaje ponderado: precio {PesoPrecio * 100:0} %, entrega {PesoEntrega * 100:0} %, calificación {PesoCalificacion * 100:0} %.");
        return lineas;
    }

    private static double Pct(decimal mayor, decimal menor) =>
        menor == 0 ? 0 : (double)((mayor - menor) / menor * 100m);

    // ── Promoción ─────────────────────────────────────────────────────────────
    public static double Percentil(IReadOnlyList<double> valores, double v) =>
        valores.Count == 0 ? 0 : valores.Count(x => x <= v) / (double)valores.Count;

    public const double PercentilMinimo = 0.6;   // ambos criterios dentro del 40 % superior

    // Alta rotación (unidades vendidas en 30 días) y buen margen: ambos en el 40 % superior de los
    // productos elegibles. Se exige stock para al menos 14 días, porque promover algo que se agota no sirve.
    public static List<ResultadoPromocion> Promocionables(IReadOnlyList<CandidatoPromocion> todos, int cantidad)
    {
        var elegibles = todos
            .Where(c => c.Unidades30d > 0 && c.MargenPct > 0 && c.Stock >= c.DemandaDiaria * 14)
            .ToList();
        if (elegibles.Count == 0) return new();

        var rotaciones = elegibles.Select(c => (double)c.Unidades30d).ToList();
        var margenes = elegibles.Select(c => c.MargenPct).ToList();

        return elegibles
            .Select(c =>
            {
                var pr = Percentil(rotaciones, c.Unidades30d);
                var pm = Percentil(margenes, c.MargenPct);
                return new ResultadoPromocion(c, pr, pm, 0.5 * pr + 0.5 * pm);
            })
            .Where(r => r.PctRotacion >= PercentilMinimo && r.PctMargen >= PercentilMinimo)
            .OrderByDescending(r => r.Puntaje)
            .ThenByDescending(r => r.C.Unidades30d)
            .Take(cantidad)
            .ToList();
    }

    // ── Complementarios (productos que se venden juntos) ──────────────────────
    // Confianza(b -> c) = ventas con b y c / ventas con b. Para cada candidato se toma la mejor
    // confianza entre los productos que ya están en la venta.
    public static List<Complemento> Complementarios(
        IReadOnlyList<(int VentaId, int ProductoId)> lineas,
        IReadOnlyCollection<int> baseIds, int minVeces = 2, double minConfianza = 0.10)
    {
        var porVenta = lineas
            .GroupBy(l => l.VentaId)
            .ToDictionary(g => g.Key, g => g.Select(l => l.ProductoId).Distinct().ToList());

        var mejores = new Dictionary<int, Complemento>();
        foreach (var b in baseIds)
        {
            var ventasConB = porVenta.Values.Where(v => v.Contains(b)).ToList();
            if (ventasConB.Count == 0) continue;

            var conteos = ventasConB
                .SelectMany(v => v)
                .Where(p => !baseIds.Contains(p))
                .GroupBy(p => p)
                .Select(g => (Producto: g.Key, Veces: g.Count()));

            foreach (var (producto, veces) in conteos)
            {
                var confianza = veces / (double)ventasConB.Count;
                if (veces < minVeces || confianza < minConfianza) continue;
                if (!mejores.TryGetValue(producto, out var actual) || confianza > actual.Confianza)
                    mejores[producto] = new Complemento(producto, veces, confianza, b, ventasConB.Count);
            }
        }

        return mejores.Values
            .OrderByDescending(c => c.Confianza)
            .ThenByDescending(c => c.VecesJuntos)
            .ToList();
    }
}
