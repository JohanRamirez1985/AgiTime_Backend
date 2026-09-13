using Compartido.Constantes;
using Compartido.Data;
using Compartido.Data.Modelos;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compartido.Metodos
{
    public class ResultadoValidacion
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; }

        public static ResultadoValidacion Ok()
        {
            return new ResultadoValidacion { Exito = true };
        }

        public static ResultadoValidacion Error(string mensaje)
        {
            return new ResultadoValidacion { Exito = false, Mensaje = mensaje };
        }
    }
    public class ValidacionEmpresa
    {
        public static ResultadoValidacion ValidarEmpresa(Empresa empresa)
        {
            if (empresa == null)
            {
                return ResultadoValidacion.Error("La empresa no está disponible temporalmente.");
            }

            if (string.IsNullOrWhiteSpace(empresa.conexionBD))
            {
                return ResultadoValidacion.Error("La empresa no tiene base de datos asignada.");
            }

            return ResultadoValidacion.Ok();
        }

        public static async Task<Empresa> ObtenerEmpresaPorSubdominio(string host, GeneralContext contextGene)
        {
            if (string.IsNullOrWhiteSpace(host))
                return null;

            var partes = host.Split('.');

            if (partes.Length < 3)
                return null;

            var subdominio = partes[0];

            if (subdominio.Equals("www", StringComparison.OrdinalIgnoreCase))
                return null;

            return await contextGene.Empresas
                .FirstOrDefaultAsync(e =>
                    e.subDominio == subdominio &&
                    e.estado == Estados.Activo);
        }
    }
}
