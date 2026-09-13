using Compartido.Data.Modelos;
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
    }
}
