using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BulkProcessMicroservice.Services;

public sealed class RabbitMqConsumerService : BackgroundService
{
   private readonly IConfiguration _configuration;
   private readonly IServiceScopeFactory _scopeFactory;
   private const string QueueName = "cola-procesamiento-archivos";

   public RabbitMqConsumerService(IConfiguration configuration, IServiceScopeFactory scopeFactory)
   {
      _configuration = configuration;
      _scopeFactory = scopeFactory;
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
            var message = JsonSerializer.Deserialize<ProcessMessage>(Encoding.UTF8.GetString(eventArgs.Body.Span))
               ?? throw new InvalidOperationException("Mensaje de procesamiento vacío.");
            await using var scope = _scopeFactory.CreateAsyncScope();
            await using var db = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            var periodo = await db.ExecuteScalarAsync<string>("SELECT Periodo FROM CargaArchivo WHERE Id = @Id", new { Id = message.IdCarga });
            var service = scope.ServiceProvider.GetRequiredService<ExcelProcessService>();
            var resultado = await service.ProcesarArchivoExcelAsync(message.IdCarga, message.RutaArchivo, periodo ?? "N/A");
            if (!resultado.Exito)
               throw new InvalidOperationException("El procesamiento fue rechazado.");
            await channel.BasicAckAsync(eventArgs.DeliveryTag, false, stoppingToken);
         }
         catch
         {
            await channel.BasicNackAsync(eventArgs.DeliveryTag, false, requeue: false, stoppingToken);
         }
      };

      await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer, stoppingToken);
      await Task.Delay(Timeout.Infinite, stoppingToken);
   }

   private sealed record ProcessMessage(int IdCarga, string RutaArchivo, string Usuario);
}