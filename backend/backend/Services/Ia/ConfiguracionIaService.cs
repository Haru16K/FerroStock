using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Ia;

public class ConfiguracionIaService(FerroStockDbContext db)
{
    // clave -> (valor por defecto, mínimo, máximo)
    public static readonly Dictionary<string, (int Defecto, int Min, int Max)> Parametros = new()
    {
        ["Compra.DiasCobertura"]          = (30, 7, 180),    // días de venta que debe cubrir cada compra
        ["Compra.NivelServicio"]          = (90, 80, 99),    // % de probabilidad de no quedarse sin stock
        ["Promocion.Cantidad"]            = (5, 1, 20),      // cuántos productos proponer para promocionar
        ["Descontinuar.UmbralUnidades"]   = (3, 0, 1000),    // vende menos que esto en la ventana => baja rotación
        ["Descontinuar.DiasVentana"]      = (180, 30, 730),  // período en que se cuentan las ventas
        ["Descontinuar.DiasSinMovimiento"] = (90, 30, 730)   // tiempo mínimo sin movimiento
    };

    public const string ClaveUltimaGeneracion = "Recomendaciones.UltimaGeneracion";

    public async Task<Dictionary<string, int>> GetTodoAsync()
    {
        var filas = await db.ConfiguracionesIa.AsNoTracking()
            .ToDictionaryAsync(c => c.Clave, c => c.Valor);

        var r = new Dictionary<string, int>();
        foreach (var (clave, p) in Parametros)
            r[clave] = filas.TryGetValue(clave, out var v) && int.TryParse(v, out var n) ? n : p.Defecto;
        return r;
    }

    // Todo o nada: si un valor no es válido no se guarda ninguno
    public async Task GuardarAsync(Dictionary<string, int> valores)
    {
        foreach (var (clave, valor) in valores)
        {
            if (!Parametros.TryGetValue(clave, out var p))
                throw new InvalidOperationException($"Parámetro desconocido: {clave}");
            if (valor < p.Min || valor > p.Max)
                throw new InvalidOperationException($"{clave} debe estar entre {p.Min} y {p.Max}");
            if (clave == "Compra.NivelServicio" && !MotorRecomendacion.NivelesServicio.Contains(valor))
                throw new InvalidOperationException(
                    $"El nivel de servicio debe ser uno de: {string.Join(", ", MotorRecomendacion.NivelesServicio)}");
        }

        foreach (var (clave, valor) in valores)
            await GuardarTextoAsync(clave, valor.ToString());
    }

    public async Task<string?> GetTextoAsync(string clave) =>
        await db.ConfiguracionesIa.AsNoTracking().Where(c => c.Clave == clave)
            .Select(c => c.Valor).FirstOrDefaultAsync();

    public async Task GuardarTextoAsync(string clave, string valor)
    {
        var fila = await db.ConfiguracionesIa.FirstOrDefaultAsync(c => c.Clave == clave);
        if (fila is null) db.ConfiguracionesIa.Add(new ConfiguracionIa { Clave = clave, Valor = valor });
        else fila.Valor = valor;
        await db.SaveChangesAsync();
    }
}
