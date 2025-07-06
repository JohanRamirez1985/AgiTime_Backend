using Microsoft.EntityFrameworkCore;
using Clientes.API.Data;

var builder = WebApplication.CreateBuilder(args);

// 🔧 Inyección de servicios
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 🔧 Configuración de tu DbContext (ajusta la cadena de conexión)
builder.Services.AddDbContext<ClientesContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ConnectionGeneral")));

// 🔧 Configuración para autenticación JWT si la usas (opcional según tu caso)

// Carga de configuración desde appsettings.json
var app = builder.Build();

// 🔧 Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
