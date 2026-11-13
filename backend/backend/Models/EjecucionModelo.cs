namespace backend.Models;

// Registro de cada actualización del modelo de predicción (HU-29).
// Si Estado = "Error", el administrador ve el aviso en la pantalla de Predicciones.
public class EjecucionModelo
{
    public int Id { get; set; }
    public DateTime Inicio { get; set; }
    public DateTime? Fin { get; set; }
    public string Origen { get; set; } = "Automática";   // Automática | Manual
    public string Mes { get; set; } = string.Empty;      // yyyy-MM (mes de Lima en que se ejecutó)
    public string Estado { get; set; } = "Ok";           // Ok | Error
    public string? Mensaje { get; set; }
    public int ProductosProcesados { get; set; }
    public decimal? Mae { get; set; }
    public decimal? Mape { get; set; }
    public int EstacionalesDetectados { get; set; }
}
