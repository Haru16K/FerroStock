using System.Net;
using System.Net.Mail;

namespace backend.Services;

public interface IEmailSender
{
    Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml, string cuerpoTexto);
}

/// <summary>
/// Envía correos por SMTP. Se configura en appsettings.json:
///   "Smtp": { "Host": "smtp.gmail.com", "Port": 587, "EnableSsl": true,
///             "User": "cuenta@gmail.com", "Password": "clave-de-aplicacion", "From": "cuenta@gmail.com" }
/// Si no hay Smtp:Host (por ejemplo en desarrollo), NO se envía nada: el contenido del
/// correo, incluido el enlace, se escribe en la consola del API para poder probar el flujo.
/// </summary>
public class EmailSender(IConfiguration config, ILogger<EmailSender> logger) : IEmailSender
{
    public async Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml, string cuerpoTexto)
    {
        var host = config["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogWarning(
                "SMTP no configurado: el correo NO se envió. Para: {Para} | Asunto: {Asunto}{Nueva}{Cuerpo}",
                destinatario, asunto, Environment.NewLine, cuerpoTexto);
            return;
        }

        var usuario = config["Smtp:User"];
        var clave   = config["Smtp:Password"];
        var desde   = config["Smtp:From"] ?? usuario ?? "no-reply@ferrotech.pe";

        using var smtp = new SmtpClient(host, config.GetValue("Smtp:Port", 587))
        {
            EnableSsl = config.GetValue("Smtp:EnableSsl", true)
        };
        if (!string.IsNullOrEmpty(usuario))
            smtp.Credentials = new NetworkCredential(usuario, clave);

        using var mensaje = new MailMessage(new MailAddress(desde, "FerroStock"), new MailAddress(destinatario))
        {
            Subject = asunto,
            Body = cuerpoHtml,
            IsBodyHtml = true
        };

        await smtp.SendMailAsync(mensaje);
    }
}
