using Microsoft.EntityFrameworkCore;
using Usuarios.API.Data;

var builder = WebApplication.CreateBuilder(args);

// 🔧 Inyección de servicios
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 🔧 Configuración de tu DbContext (ajusta la cadena de conexión)
builder.Services.AddDbContext<GeneralContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ConnectionGeneral")));

builder.Services.AddScoped<FactoryContext>();

// 🔧 Configuración para autenticación JWT si la usas (opcional según tu caso)

builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:4200") // Cambia este al origen de tu frontend
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

// ✅ Habilita CORS antes de Authorization
app.UseCors("PermitirFrontend");

app.UseAuthorization();

app.MapControllers();

app.Run();