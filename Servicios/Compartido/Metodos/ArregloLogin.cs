using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compartido.Metodos
{
    public static class ArregloLogin
    {
        public static object ConstruirData(object info, string valor)
        {

            if (valor == "Usuario")
            {

            }

            var respuesta = new
            {
                Usuario = info
            };

            return (respuesta);
        }
    }
}
