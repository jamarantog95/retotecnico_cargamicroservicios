using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationMicroservice.Services;

public sealed class RabbitMqNotificationConsumerService : BackgroundService
{
   private const string QueueName = "cola-notificaciones";
   private readonly IConfiguration _configuration;
   private readonly EmailService _emailService;
   private readonly ILogger<RabbitMqNotificationConsumerService> _logger;

   public RabbitMqNotificationConsumerService(
      IConfiguration configuration,
      EmailService emailService,
      ILogger<RabbitMqNotificationConsumerService> logger)
   {
      _configuration = configuration;
      _emailService = emailService;
      _logger = logger;
   }

   protected override async Task ExecuteAsync(CancellationToken stoppingToken)
   {
      var factory = new ConnectionFactory { HostName = _configuration["RabbitMQ:HostName"] ?? "localhost" };
      await using var connection = await factory.CreateConnectionAsync(stoppingToken);
      await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
      await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);
      await channel.BasicQosAsync(0, 1, false, stoppingToken);

      var consumer = new AsyncEventingBasicConsumer(channel);
      consumer.ReceivedAsync += async (_, eventArgs) =>
      {
         try
         {
            var notification = JsonSerializer.Deserialize<NotificationMessage>(Encoding.UTF8.GetString(eventArgs.Body.Span))
               ?? throw new InvalidOperationException("Mensaje de notificación vacío.");

            await using var db = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            await db.OpenAsync(stoppingToken);
            var estado = await db.ExecuteScalarAsync<string>(
               "SELECT Estado FROM CargaArchivo WHERE Id = @Id",
               new { Id = notification.IdCarga });
            var periodo = await db.ExecuteScalarAsync<string>(
               "SELECT Periodo FROM CargaArchivo WHERE Id = @Id",
               new { Id = notification.IdCarga });

            if (string.Equals(estado, "Notificado", StringComparison.OrdinalIgnoreCase))
            {
               await channel.BasicAckAsync(eventArgs.DeliveryTag, false, stoppingToken);
               return;
            }

            if (estado is null)
            {
               // Carga inexistente: error permanente, reencolar sería un bucle infinito.
               _logger.LogWarning("Se descarta la notificación de la carga {IdCarga}: no existe en CargaArchivo.", notification.IdCarga);
               await channel.BasicAckAsync(eventArgs.DeliveryTag, false, stoppingToken);
               return;
            }

            if (!string.Equals(estado, "Finalizado", StringComparison.OrdinalIgnoreCase))
               throw new InvalidOperationException($"La carga {notification.IdCarga} no está finalizada: {estado}.");

            var periodoParaCorreo = !string.IsNullOrWhiteSpace(periodo)
               ? periodo
               : notification.Periodo;
            await _emailService.EnviarAsync(notification.Usuario, notification.IdCarga, periodoParaCorreo, stoppingToken);
            await db.ExecuteAsync(
               "UPDATE CargaArchivo SET Estado = 'Notificado', FechaFin = GETDATE() WHERE Id = @Id AND Estado = 'Finalizado'",
               new { Id = notification.IdCarga });
            await channel.BasicAckAsync(eventArgs.DeliveryTag, false, stoppingToken);
         }
         catch (Exception exception)
         {
            _logger.LogError(exception, "No se pudo enviar la notificación de la carga.");
            await channel.BasicNackAsync(eventArgs.DeliveryTag, false, requeue: true, stoppingToken);
         }
      };

      await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer, stoppingToken);
      await Task.Delay(Timeout.Infinite, stoppingToken);
   }

   private sealed record NotificationMessage(int IdCarga, string Periodo, string Usuario);
}