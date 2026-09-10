using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace NotificationMicroservice.Services;

public sealed class EmailService
{
   private readonly IConfiguration _configuration;

   public EmailService(IConfiguration configuration)
   {
      _configuration = configuration;
   }

   public async Task EnviarAsync(string destinatario, int idCarga, string periodo, CancellationToken cancellationToken)
   {
      if (string.IsNullOrWhiteSpace(destinatario))
         throw new InvalidOperationException("La carga no tiene un destinatario de notificación.");

      var message = new MimeMessage();
      message.From.Add(new MailboxAddress(
         _configuration["Smtp:FromName"] ?? "Sistema de cargas",
         _configuration["Smtp:FromAddress"] ?? throw new InvalidOperationException("No se configuró Smtp:FromAddress.")));
      message.To.Add(MailboxAddress.Parse(destinatario));
      message.Subject = $"Carga {idCarga} finalizada";
      message.Body = new TextPart("plain")
      {
         Text = $"Hola {destinatario}, la carga {idCarga} del periodo {periodo} fue procesada correctamente y está lista."
      };

      using var smtp = new SmtpClient();
      var host = _configuration["Smtp:Host"] ?? throw new InvalidOperationException("No se configuró Smtp:Host.");
      var port = _configuration.GetValue("Smtp:Port", 587);
      var security = _configuration.GetValue("Smtp:UseSsl", true)
         ? SecureSocketOptions.StartTls
         : SecureSocketOptions.Auto;

      await smtp.ConnectAsync(host, port, security, cancellationToken);
      var username = _configuration["Smtp:Username"];
      var password = _configuration["Smtp:Password"];
      if (!string.IsNullOrWhiteSpace(username))
      {
         if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Se configuró Smtp:Username pero falta Smtp:Password.");

         await smtp.AuthenticateAsync(username, password, cancellationToken);
      }

      await smtp.SendAsync(message, cancellationToken);
      await smtp.DisconnectAsync(true, cancellationToken);
   }
}