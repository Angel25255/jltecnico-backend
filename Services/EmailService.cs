using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace JLTecnico.Auth.Services;

public class EmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task EnviarAlertaDispositivoNuevo(string correoDestino, string nombreUsuario, string ip, string userAgent)
    {
        var mensaje = new MimeMessage();
        mensaje.From.Add(new MailboxAddress(_config["Email:NombreRemitente"], _config["Email:Usuario"]));
        mensaje.To.Add(new MailboxAddress(nombreUsuario, correoDestino));
        mensaje.Subject = "⚠️ Nuevo inicio de sesión detectado - JL Técnico EIRL";

        mensaje.Body = new TextPart("plain")
        {
            Text = $@"Hola {nombreUsuario},

Se detectó un inicio de sesión en tu cuenta desde un dispositivo o ubicación no reconocido anteriormente.

  Fecha y hora: {DateTime.Now:dd/MM/yyyy HH:mm:ss}
  Dirección IP: {ip}
  Dispositivo/Navegador: {userAgent}

Si fuiste tú, puedes ignorar este mensaje.

Si NO reconoces esta actividad:
  1. Cambia tu contraseña de inmediato.
  2. Revisa tus sesiones activas dentro del sistema y ciérralas.
  3. Comunícate con el administrador del sistema.

— Sistema de Gestión JL Técnico EIRL"
        };

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(
                _config["Email:SmtpHost"],
                _config.GetValue<int>("Email:SmtpPort"),
                SecureSocketOptions.StartTls);

            await client.AuthenticateAsync(_config["Email:Usuario"], _config["Email:Password"]);
            await client.SendAsync(mensaje);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            // No queremos que el login falle si el correo no se pudo enviar,
            // pero sí queremos que quede registrado en el log del servidor.
            _logger.LogError(ex, "No se pudo enviar la alerta de dispositivo nuevo a {Correo}", correoDestino);
        }
    }
}
