using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

public class RabbitMqPublisherService
{
   private readonly string _hostName;
   private readonly string _queueName = "cola-procesamiento-archivos";

   public RabbitMqPublisherService(IConfiguration configuration)
   {
      // Lee el host desde appsettings.json o usa "localhost" por defecto
      _hostName = configuration["RabbitMQ:HostName"] ?? "localhost";
   }

   public async Task PublicarMensaje(int idCarga, string rutaArchivo, string usuario)
   {
      var factory = new ConnectionFactory() { HostName = _hostName };
      await using var connection = await factory.CreateConnectionAsync();
      await using var channel = await connection.CreateChannelAsync();

      // Declara la cola de manera durable para asegurar que los mensajes no se pierdan
      await channel.QueueDeclareAsync(
          queue: _queueName,
          durable: true,
          exclusive: false,
          autoDelete: false,
          arguments: null
      );

      // Estructura exacta del mensaje solicitada
      var payload = new
      {
         idCarga = idCarga,
         rutaArchivo = rutaArchivo,
         usuario = usuario
      };

      var bodyJson = JsonSerializer.Serialize(payload);
      var bodyBytes = Encoding.UTF8.GetBytes(bodyJson);

      // Publicar el mensaje en la cola
      await channel.BasicPublishAsync(
        exchange: "",
        routingKey: _queueName,
        body: bodyBytes
    );
   }
}