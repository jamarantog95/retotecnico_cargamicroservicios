using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace BulkProcessMicroservice.Services;

public sealed class NotificationPublisherService
{
   private const string QueueName = "cola-notificaciones";
   private readonly string _hostName;

   public NotificationPublisherService(IConfiguration configuration)
   {
      _hostName = configuration["RabbitMQ:HostName"] ?? "localhost";
   }

   public async Task PublicarAsync(int idCarga, string periodo, string usuario)
   {
      var factory = new ConnectionFactory { HostName = _hostName };
      await using var connection = await factory.CreateConnectionAsync();
      await using var channel = await connection.CreateChannelAsync();
      await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

      var message = new NotificationMessage(idCarga, periodo, usuario);
      var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
      await channel.BasicPublishAsync(exchange: string.Empty, routingKey: QueueName, body: body);
   }

   private sealed record NotificationMessage(int IdCarga, string Periodo, string Usuario);
}