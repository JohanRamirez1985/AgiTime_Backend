using System;
using System.Collections.Generic;

namespace Clientes.API.Data.Modelos
{
    public partial class Empresa
    {
        public int id { get; set; }
        public string nombre { get; set; } = null!;
        public string nit { get; set; } = null!;
        public string logo { get; set; } = null!;
        public string conexionBD { get; set; } = null!;
        public string estado { get; set; } = null!;
        public bool servicio { get; set; }
    }
}
