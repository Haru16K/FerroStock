namespace backend.Helpers;

/// <summary>
/// Las fechas se guardan en UTC, pero los usuarios filtran por día calendario de Lima.
/// Perú no usa horario de verano, así que Lima es siempre UTC-5. Se usa un desfase fijo
/// (y no TimeZoneInfo) para que funcione igual en Windows y Linux, sin depender de los
/// identificadores de zona horaria del sistema operativo.
/// </summary>
public static class FechasLima
{
    private static readonly TimeSpan DesfaseLima = TimeSpan.FromHours(-5);

    /// <summary>Convierte un instante UTC a la hora de pared de Lima.</summary>
    public static DateTime AHoraLima(DateTime utc) =>
        DateTime.SpecifyKind(utc.Add(DesfaseLima), DateTimeKind.Unspecified);

    /// <summary>Día calendario de hoy en Lima (sin hora).</summary>
    public static DateTime HoyLima => AHoraLima(DateTime.UtcNow).Date;

    /// <summary>
    /// Instante UTC en que empieza el día de Lima indicado (00:00 de Lima).
    /// Ej.: 2026-11-04 -> 2026-11-04 05:00 UTC.
    /// </summary>
    public static DateTime InicioDiaUtc(DateTime fechaLima) =>
        DateTime.SpecifyKind(fechaLima.Date - DesfaseLima, DateTimeKind.Utc);

    /// <summary>
    /// Límite superior EXCLUSIVO que cubre todo el día de Lima indicado
    /// (00:00 de Lima del día siguiente, en UTC). Usar con "&lt;".
    /// </summary>
    public static DateTime FinDiaExclusivoUtc(DateTime fechaLima) =>
        InicioDiaUtc(fechaLima.AddDays(1));
}
