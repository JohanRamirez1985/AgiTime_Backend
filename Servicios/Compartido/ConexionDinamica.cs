using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Compartido
{
    public static class ConexionDinamica
    {
        public static string ObtenerConexionEmpresa(IConfiguration configuration, string nombreBase)
        {
            var formato = configuration.GetConnectionString("FormatoConnectionEmpresa");

            if (string.IsNullOrEmpty(formato))
                throw new Exception("La cadena de conexión no está configurada.");

            return formato.Replace("{NombreBase}", nombreBase);
        }
    }
}
