namespace backend.Services.Ia;

// Motor estadístico de predicción de demanda. Es una clase estática y pura (sin base de datos),
// así sus resultados son reproducibles y se pueden verificar con pruebas unitarias.
//
// Entrada: unidades vendidas por día, del más antiguo al más reciente. El último día de la
// serie es AYER (las ventas de hoy entran al cálculo desde mañana, pero el stock sí se
// actualiza al instante, por eso la fecha de quiebre cambia tras cada venta).

public record ResultadoPrediccion(
    decimal DemandaDiaria,
    decimal Demanda30d,
    decimal Minima30d,
    decimal Maxima30d,
    int ConfianzaPct,
    string NivelConfianza,
    int DiasHistorial,
    decimal ConsumoReciente,
    int? DiasHastaQuiebre,
    bool EsEstacional,
    int? MesPico,
    decimal FactorEstacional,
    int SemanasUsadas,
    decimal DesviacionSemanal);

public record ObservacionBacktest(decimal Pronostico, decimal Real);

public static class MotorPrediccion
{
    public const int VentanaCorta = 28;    // 4 semanas: lo más reciente
    public const int VentanaLarga = 84;    // 12 semanas: estabiliza el promedio
    public const int Horizonte = 30;       // días a pronosticar
    public const double PesoCorta = 0.6;
    public const double PesoLarga = 0.4;
    private const double Z80 = 1.2816;     // percentil 90 de la normal => intervalo central de ~80 %

    public static readonly string[] Meses =
        { "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto",
          "septiembre", "octubre", "noviembre", "diciembre" };

    // Índice del primer día con ventas dentro de [0, hasta). -1 si no hay ninguna.
    public static int PrimerDiaConVentas(int[] diario, int hasta)
    {
        for (var i = 0; i < hasta; i++)
            if (diario[i] > 0) return i;
        return -1;
    }

    // Promedio diario de los últimos 'ventana' días antes de 'hasta'. Si el producto es más
    // nuevo que la ventana, solo se promedian los días desde su primera venta (para no diluirlo).
    public static double TasaDiaria(int[] diario, int hasta, int primer, int ventana)
    {
        if (primer < 0 || hasta <= primer) return 0;
        var dias = Math.Min(ventana, hasta - primer);
        var suma = 0;
        for (var i = hasta - dias; i < hasta; i++) suma += diario[i];
        return (double)suma / dias;
    }

    // Tasa base de demanda: 60 % últimos 28 días + 40 % últimos 84 días
    public static double TasaBase(int[] diario, int hasta, int primer) =>
        PesoCorta * TasaDiaria(diario, hasta, primer, VentanaCorta) +
        PesoLarga * TasaDiaria(diario, hasta, primer, VentanaLarga);

    public static ResultadoPrediccion Calcular(int[] diario, int stockActual, DateTime hoyLima)
    {
        var n = diario.Length;
        var primer = PrimerDiaConVentas(diario, n);

        // Sin historial no hay base para pronosticar
        if (primer < 0)
            return new ResultadoPrediccion(0, 0, 0, 0, 0, "Baja", 0, 0,
                stockActual <= 0 ? 0 : null, false, null, 1m, 0, 0m);

        var diasHistorial = n - primer;
        var tasaCorta = TasaDiaria(diario, n, primer, VentanaCorta);
        var tasaBase  = TasaBase(diario, n, primer);

        // Semanas completas (hasta 12) dentro del historial; la más reciente va al final
        var semanas = Math.Min(12, diasHistorial / 7);
        var sem = new double[semanas];
        for (var w = 0; w < semanas; w++)
        {
            var fin = n - 7 * (semanas - 1 - w);   // exclusivo
            var suma = 0;
            for (var i = fin - 7; i < fin; i++) suma += diario[i];
            sem[w] = suma;
        }

        var media = semanas > 0 ? sem.Average() : tasaBase * 7;
        double desv;
        if (semanas >= 2)
            desv = Math.Sqrt(sem.Sum(x => (x - media) * (x - media)) / (semanas - 1));
        else
            desv = media;                           // casi sin evidencia: incertidumbre alta
        var cv = media > 0 ? desv / media : 0;

        // Estacionalidad: si hay patrón, se reemplaza la época de los datos recientes por la de los próximos 30 días
        var (esEstacional, mesPico, indices) = DetectarEstacionalidad(diario, primer, hoyLima);
        double factor = 1;
        if (esEstacional)
        {
            var reciente = IndiceEnVentana(indices, hoyLima, -VentanaLarga, 0);
            var futuro   = IndiceEnVentana(indices, hoyLima, 0, Horizonte);
            factor = Math.Clamp(futuro / reciente, 0.4, 2.5);
        }

        var demanda   = tasaBase * Horizonte * factor;
        var intervalo = Z80 * desv * Math.Sqrt(Horizonte / 7.0);
        var minima    = Math.Max(0, demanda - intervalo);
        var maxima    = demanda + intervalo;

        // Confianza (0-100): cantidad de historial (40 %), estabilidad de las ventas (40 %)
        // y regularidad, es decir semanas con ventas (20 %)
        var s1 = Math.Min(1.0, semanas / 12.0);
        var s2 = media > 0 ? 1.0 / (1.0 + cv) : 0.5;
        var s3 = semanas > 0 ? sem.Count(x => x > 0) / (double)semanas : 0;
        var confianza = (int)Math.Round(100 * (0.4 * s1 + 0.4 * s2 + 0.2 * s3));
        var nivel = confianza >= 70 ? "Alta" : confianza >= 40 ? "Media" : "Baja";

        // Quiebre: stock actual / consumo promedio reciente (últimos 28 días)
        int? diasHastaQuiebre = null;
        if (stockActual <= 0) diasHastaQuiebre = 0;
        else if (tasaCorta > 0) diasHastaQuiebre = (int)Math.Floor(stockActual / tasaCorta);

        return new ResultadoPrediccion(
            R2(tasaBase * factor), R2(demanda), R2(minima), R2(maxima),
            confianza, nivel, diasHistorial, R2(tasaCorta), diasHastaQuiebre,
            esEstacional, mesPico, R2(factor), semanas, R2(desv));
    }

    // ── Estacionalidad (HU-27) ────────────────────────────────────────────────
    // Exige al menos un año de historial. Compara el promedio de cada mes calendario contra el
    // promedio mensual general; si algún mes llega a 1,5 veces o más, el producto es estacional.
    // Con 12 meses se detecta el pico; con 24 o más se confirma que se repite.
    public static (bool Es, int? MesPico, double[] Indices) DetectarEstacionalidad(
        int[] diario, int primer, DateTime hoyLima)
    {
        var indices = Enumerable.Repeat(1.0, 12).ToArray();
        var n = diario.Length;
        if (primer < 0 || n - primer < 365) return (false, null, indices);

        var totales = new Dictionary<(int Anio, int Mes), double>();
        var dias    = new Dictionary<(int Anio, int Mes), int>();
        for (var i = primer; i < n; i++)
        {
            var fecha = hoyLima.AddDays(-(n - i));   // el último elemento es ayer
            var k = (fecha.Year, fecha.Month);
            totales[k] = totales.GetValueOrDefault(k) + diario[i];
            dias[k]    = dias.GetValueOrDefault(k) + 1;
        }

        // Solo meses completos dentro del historial
        var completos = totales.Keys.Where(k => dias[k] == DateTime.DaysInMonth(k.Anio, k.Mes)).ToList();
        if (completos.Count < 10) return (false, null, indices);

        var sumaMes = new double[12];
        var cuenta  = new int[12];
        foreach (var k in completos) { sumaMes[k.Mes - 1] += totales[k]; cuenta[k.Mes - 1]++; }

        var meses = Enumerable.Range(0, 12).Where(m => cuenta[m] > 0).ToList();
        var general = meses.Average(m => sumaMes[m] / cuenta[m]);
        var unidades = completos.Sum(k => totales[k]);
        if (general <= 0 || unidades < 30) return (false, null, indices);

        foreach (var m in meses) indices[m] = (sumaMes[m] / cuenta[m]) / general;

        var pico = meses.OrderByDescending(m => indices[m]).First();
        var es = indices[pico] >= 1.5;
        return (es, es ? (int?)(pico + 1) : null, indices);
    }

    // Índice estacional promedio de los días [hoy + desde, hoy + hasta)
    private static double IndiceEnVentana(double[] indices, DateTime hoy, int desde, int hasta)
    {
        double suma = 0;
        var dias = Math.Max(1, hasta - desde);
        for (var d = desde; d < hasta; d++)
            suma += Math.Clamp(indices[hoy.AddDays(d).Month - 1], 0.25, 3.0);
        return suma / dias;
    }

    // ── Backtest (HU-28) ──────────────────────────────────────────────────────
    // Para cada una de las últimas 'semanas' semanas: se pronostica esa semana usando SOLO lo
    // anterior y se compara contra lo que realmente se vendió.
    public static List<ObservacionBacktest> Backtest(int[] diario, int semanas = 4)
    {
        var obs = new List<ObservacionBacktest>();
        var n = diario.Length;
        var primer = PrimerDiaConVentas(diario, n);
        if (primer < 0) return obs;

        for (var k = 1; k <= semanas; k++)
        {
            var t = n - 7 * k;                          // origen del pronóstico
            if (t - primer < VentanaCorta) continue;    // en ese momento había poco historial

            var pronostico = TasaBase(diario, t, primer) * 7;
            double real = 0;
            for (var i = t; i < t + 7; i++) real += diario[i];
            obs.Add(new ObservacionBacktest((decimal)pronostico, (decimal)real));
        }
        return obs;
    }

    // MAE (unidades/semana), MAPE (% sobre semanas con ventas) y WAPE (% ponderado por volumen)
    public static (decimal Mae, decimal? Mape, decimal? Wape) Metricas(IReadOnlyCollection<ObservacionBacktest> obs)
    {
        if (obs.Count == 0) return (0, null, null);

        var mae = obs.Average(o => Math.Abs(o.Pronostico - o.Real));

        var conVentas = obs.Where(o => o.Real > 0).ToList();
        decimal? mape = conVentas.Count > 0
            ? conVentas.Average(o => Math.Abs(o.Pronostico - o.Real) / o.Real) * 100m
            : null;

        var totalReal = obs.Sum(o => o.Real);
        decimal? wape = totalReal > 0
            ? obs.Sum(o => Math.Abs(o.Pronostico - o.Real)) / totalReal * 100m
            : null;

        return (Math.Round(mae, 2),
                mape.HasValue ? (decimal?)Math.Round(mape.Value, 1) : null,
                wape.HasValue ? (decimal?)Math.Round(wape.Value, 1) : null);
    }

    // ── Tendencia (HU-26) ─────────────────────────────────────────────────────
    // Regresión lineal por mínimos cuadrados sobre x = 0, 1, 2, ...
    public static (double Pendiente, double Intercepto) Regresion(IReadOnlyList<double> y)
    {
        var n = y.Count;
        if (n == 0) return (0, 0);
        if (n == 1) return (0, y[0]);

        double sx = 0, sy = 0, sxy = 0, sxx = 0;
        for (var i = 0; i < n; i++) { sx += i; sy += y[i]; sxy += i * y[i]; sxx += (double)i * i; }
        var den = n * sxx - sx * sx;
        var pendiente = den == 0 ? 0 : (n * sxy - sx * sy) / den;
        return (pendiente, (sy - pendiente * sx) / n);
    }

    private static decimal R2(double v) => Math.Round((decimal)v, 2);
}
