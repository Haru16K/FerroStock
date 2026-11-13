using Xunit;
using backend.Models;

namespace FerroStock.Tests;

public class ProductoTests
{
    [Fact]
    public void Producto_Nuevo_TieneValoresPorDefecto()
    {
        var p = new Producto();
        Assert.Equal(0, p.Id);
        Assert.Equal(string.Empty, p.Codigo);
        Assert.Equal(string.Empty, p.Nombre);
        Assert.Equal(0, p.StockActual);
        Assert.Equal(0, p.StockMinimo);
        Assert.True(p.Activo);
        Assert.False(p.EsEstacional);
        Assert.Null(p.MesPicoEstacional);
    }

    [Fact]
    public void Producto_AsignacionDePropiedades_Funciona()
    {
        var p = new Producto
        {
            Id = 1,
            Codigo = "P-001",
            Nombre = "Cemento Portland Tipo I",
            CategoriaId = 1,
            PrecioCompra = 25.00m,
            PrecioVenta = 32.00m,
            StockActual = 120,
            StockMinimo = 30,
            Activo = true,
            EsEstacional = true,
            MesPicoEstacional = 11
        };

        Assert.Equal(1, p.Id);
        Assert.Equal("P-001", p.Codigo);
        Assert.Equal(120, p.StockActual);
        Assert.Equal(25.00m, p.PrecioCompra);
        Assert.Equal(32.00m, p.PrecioVenta);
        Assert.True(p.EsEstacional);
        Assert.Equal(11, p.MesPicoEstacional);
    }

    [Fact]
    public void Producto_PrecioVentaMayorQuePrecioCompra_EsRentable()
    {
        var p = new Producto { PrecioCompra = 25.00m, PrecioVenta = 32.00m };
        Assert.True(p.PrecioVenta > p.PrecioCompra);
    }
}