using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ControlMicroservice.Services;
using Microsoft.OpenApi.Models; // Asegúrate de tener este using

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientWeb", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Registrar HttpClient y el servicio de SeaweedFS
builder.Services.AddHttpClient<SeaweedFsService>();
builder.Services.AddTransient<SeaweedFsService>();
builder.Services.AddTransient<RabbitMqPublisherService>();

// builder.Services.AddSwaggerGen();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ControlMicroservice", Version = "v1" });

    // Configuración para que aparezca el botón Authorize en Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Ejemplo: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Dependency Injection for Services
builder.Services.AddScoped<ControlService>();


// JWT Authentication Setup (Debe coincidir con el Microservicio de Auth)
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Secret"]!))
    };
});

var builderApp = builder.Build();

// Configure the HTTP request pipeline.
if (builderApp.Environment.IsDevelopment())
{
    builderApp.UseSwagger();
    builderApp.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ControlMicroservice v1");
        c.RoutePrefix = string.Empty; // Esto hace que Swagger abra en la raíz ("/")
    });
}

builderApp.UseHttpsRedirection();
builderApp.UseCors("ClientWeb");

// ¡Importante para que reconozca el token!
builderApp.UseAuthentication();
builderApp.UseAuthorization();

builderApp.MapControllers();

builderApp.Run();