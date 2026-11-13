namespace backend.Models;

// Parámetros editables del módulo de IA (umbrales, días de cobertura, etc.).
// Se guardan en la base de datos para poder cambiarlos desde la aplicación sin reiniciar.
public class ConfiguracionIa
{
    public string Clave { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
}
