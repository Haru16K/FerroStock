using Xunit;
using backend.Helpers;

namespace FerroStock.Tests;

public class FechasLimaTests
{
    // ──────────────────────────────────────────────────────────
    // AHoraLima
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void AHoraLima_DesdeUtc_Resta5Horas()
    {
        var utc = new DateTime(2026, 11, 13, 15, 0, 0, DateTimeKind.Utc);
        var resultado = FechasLima.AHoraLima(utc);
        Assert.Equal(10, resultado.Hour);
        Assert.Equal(13, resultado.Day);
        Assert.Equal(11, resultado.Month);
        Assert.Equal(2026, resultado.Year);
    }

    [Fact]
    public void AHoraLima_MedianocheUtc_CambiaAlDiaAnterior()
    {
        // 03:00 UTC = 22:00 del día anterior en Lima (UTC-5)
        var utc = new DateTime(2026, 11, 13, 3, 0, 0, DateTimeKind.Utc);
        var resultado = FechasLima.AHoraLima(utc);
        Assert.Equal(22, resultado.Hour);
        Assert.Equal(12, resultado.Day);
    }

    [Fact]
    public void AHoraLima_DevuelveKindUnspecified()
    {
        var utc = new DateTime(2026, 11, 13, 15, 0, 0, DateTimeKind.Utc);
        var resultado = FechasLima.AHoraLima(utc);
        Assert.Equal(DateTimeKind.Unspecified, resultado.Kind);
    }

    [Fact]
    public void AHoraLima_MediodiaUtc_Devuelve7AM()
    {
        var utc = new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);
        var resultado = FechasLima.AHoraLima(utc);
        Assert.Equal(7, resultado.Hour);
    }

    // ──────────────────────────────────────────────────────────
    // InicioDiaUtc
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void InicioDiaUtc_FechaNormal_Devuelve5AMUtc()
    {
        var fechaLima = new DateTime(2026, 11, 4);
        var resultado = FechasLima.InicioDiaUtc(fechaLima);
        Assert.Equal(new DateTime(2026, 11, 4, 5, 0, 0, DateTimeKind.Utc), resultado);
    }

    [Fact]
    public void InicioDiaUtc_ConHora_IgnoraHora()
    {
        var fechaLima = new DateTime(2026, 11, 4, 15, 30, 45);
        var resultado = FechasLima.InicioDiaUtc(fechaLima);
        Assert.Equal(5, resultado.Hour);
        Assert.Equal(0, resultado.Minute);
        Assert.Equal(0, resultado.Second);
    }

    [Fact]
    public void InicioDiaUtc_DevuelveKindUtc()
    {
        var fechaLima = new DateTime(2026, 11, 4);
        var resultado = FechasLima.InicioDiaUtc(fechaLima);
        Assert.Equal(DateTimeKind.Utc, resultado.Kind);
    }

    // ──────────────────────────────────────────────────────────
    // FinDiaExclusivoUtc
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void FinDiaExclusivoUtc_DevuelveInicioDiaSiguiente()
    {
        var fechaLima = new DateTime(2026, 11, 4);
        var resultado = FechasLima.FinDiaExclusivoUtc(fechaLima);
        Assert.Equal(new DateTime(2026, 11, 5, 5, 0, 0, DateTimeKind.Utc), resultado);
    }

    [Fact]
    public void FinDiaExclusivoUtc_UltimoDiaMes_CambiaMes()
    {
        var fechaLima = new DateTime(2026, 11, 30);
        var resultado = FechasLima.FinDiaExclusivoUtc(fechaLima);
        Assert.Equal(12, resultado.Month);
        Assert.Equal(1, resultado.Day);
        Assert.Equal(5, resultado.Hour);
    }

    // ──────────────────────────────────────────────────────────
    // Coherencia entre métodos (pruebas de integración)
    // ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(2026, 1, 1)]
    [InlineData(2026, 6, 15)]
    [InlineData(2026, 11, 13)]
    [InlineData(2026, 12, 31)]
    public void InicioDiaYFinDia_SonCoherentes(int y, int m, int d)
    {
        var fecha = new DateTime(y, m, d);
        var inicio = FechasLima.InicioDiaUtc(fecha);
        var fin = FechasLima.FinDiaExclusivoUtc(fecha);
        Assert.Equal(inicio.AddDays(1), fin);
    }

    [Theory]
    [InlineData(2026, 1, 1)]
    [InlineData(2026, 6, 15)]
    [InlineData(2026, 11, 13)]
    [InlineData(2026, 12, 31)]
    public void InicioDiaUtc_AlConvertirAHoraLima_VuelveAlMismoDia(int y, int m, int d)
    {
        var fecha = new DateTime(y, m, d);
        var inicio = FechasLima.InicioDiaUtc(fecha);
        var enLima = FechasLima.AHoraLima(inicio);
        Assert.Equal(fecha.Date, enLima.Date);
    }

    [Fact]
    public void HoyLima_DevuelveFechaSinHora()
    {
        var hoy = FechasLima.HoyLima;
        Assert.Equal(0, hoy.Hour);
        Assert.Equal(0, hoy.Minute);
        Assert.Equal(0, hoy.Second);
    }
}