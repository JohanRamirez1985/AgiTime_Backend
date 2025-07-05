using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Compartido.Contextos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;


namespace Compartido
{
    public static class LogsEmpresa
    {
        public static void GuardarLog(string connectionString, string nivel, string mensaje, string exception = null)
        {
            var optionsBuilder = new DbContextOptionsBuilder<BDEmpresaContext>();
            optionsBuilder.UseSqlServer(connectionString);

            using var context = new BDEmpresaContext(optionsBuilder.Options);

            var log = new Log
            {
                Timestamp = DateTime.Now,
                Level = nivel,
                Message = mensaje,
                Exception = exception
            };

            context.Logs.Add(log);
            context.SaveChanges();
        }
    }
}
