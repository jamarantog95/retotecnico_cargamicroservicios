
using BulkProcessMicroservice.Services;
using Microsoft.OpenApi.Models;
using Microsoft.Data.SqlClient;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

// 1. Agregar servicios al contenedor
builder.Services.AddControllers();
builder.Services.AddScoped<IDbConnection>(_ =>
    new SqlConnection(builder.Configuration.GetConnectionString("DefaultConnection")));

// Registrar el servicio de procesamiento de Excel como Dependencia
builder.Services.AddHttpClient<ExcelProcessService>();
builder.Services.AddSingleton<NotificationPublisherService>();
builder.Services.AddHostedService<RabbitMqConsumerService>();

// Configurar Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "BulkProcessMicroservice", Version = "v1" });
});

var app = builder.Build();

// 2. Configurar el pipeline de solicitudes HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "BulkProcessMicroservice v1");
        c.RoutePrefix = string.Empty; // Hace que Swagger abra directamente en la raíz ("/")
    });
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();

