using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compartido.DTOs
{
    public class LoginResponse
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; }
        public string Token { get; set; }
        public object Datos { get; set; }
        public object Apariencia { get; set; }
    }
}
