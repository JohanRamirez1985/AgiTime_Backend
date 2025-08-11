using Compartido.Data;
using Compartido.DTOs;
using Compartido.Metodos;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog(); // Usa Serilog como proveedor de logs

// 🔧 Inyección de servicios
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 🔧 Configuración de tu DbContext (ajusta la cadena de conexión)
builder.Services.AddDbContext<GeneralContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ConnectionGeneral")));

builder.Services.AddScoped<FactoryContext>();

//Configuracion para el envio del email
builder.Services.Configure<SmtpSettings>(
    builder.Configuration.GetSection("SmtpSettings")
);

builder.Services.AddTransient<IEmailService, EmailService>();

// 🔧 Configuración de CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:4200") // Frontend Angular
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// 🔧 Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// ✅ Aplicar CORS antes de Authorization
app.UseCors("PermitirFrontend");

app.UseAuthorization();

app.MapControllers();

app.Run();
