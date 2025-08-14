using Compartido.Data;
using Compartido.Data.Modelos;
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
    using LogDB = Compartido.Data.Modelos.Log;

    public static class LogsCatch
    {
        private static readonly GeneralContext ContextGene;
        private static readonly FactoryContext ContextFact;
        
        public static void GuardarError(Exception ex, IConfiguration config, FactoryContext factory, string nombreBase = null)
        {
            try
            {
                Serilog.Log.Error(ex, "Error en el sistema: {Mensaje}", ex.Message);

                if (!string.IsNullOrEmpty(nombreBase))
                {
                    var connectionTemplate = config["ConnectionStrings:ConnectionFactory"];
                    var connectionString = string.Format(connectionTemplate, nombreBase);

                    using var ContextBD = factory.Create(connectionString);

                    var log = new LogDB
                    {
                        Timestamp = DateTime.Now,
                        Level = "Error",
                        Message = ex.Message,
                        Exception = ex.StackTrace
                    };

                    ContextBD.Logs.Add(log);
                    ContextBD.SaveChanges();
                }
            }
            catch (Exception exInterno)
            {
                Serilog.Log.Error(exInterno, "Fallo al intentar guardar log en la base del cliente: {Mensaje}", exInterno.Message);
            }
        }
    }
}