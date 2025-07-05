using Compartido.Contextos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compartido
{
    using LogDB = Compartido.Contextos.Log;

    public static class LogsCatch
    {
        public static void GuardarError(Exception ex, IConfiguration config, string nombreBase = null)
        {
            try
            {
                // Siempre se guarda en el log centralizado (Serilog)
                Serilog.Log.Error(ex, "Error en el sistema: {Mensaje}", ex.Message);

                // Si ya se conoce la base del cliente, guardar también en la tabla Logs
                if (!string.IsNullOrEmpty(nombreBase))
                {
                    string connectionString = ConexionDinamica.ObtenerConexionEmpresa(config, nombreBase);

                    var optionsBuilder = new DbContextOptionsBuilder<BDEmpresaContext>();
                    optionsBuilder.UseSqlServer(connectionString);

                    using var context = new BDEmpresaContext(optionsBuilder.Options);

                    var log = new LogDB
                    {
                        Timestamp = DateTime.Now,
                        Level = "Error",
                        Message = ex.Message,
                        Exception = ex.StackTrace
                    };

                    context.Logs.Add(log);
                    context.SaveChanges();
                }
            }
            catch (Exception exInterno)
            {
                Serilog.Log.Error(exInterno, "Fallo al intentar guardar log en la base del cliente: {Mensaje}", exInterno.Message);
            }
        }
    }
}