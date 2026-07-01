using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class Empresa
    {
        public int id { get; set; }
        public string nombre { get; set; }
        public string nit { get; set; }
        public string logo { get; set; }
        public string conexionBD { get; set; }
        public string estado { get; set; }
        public bool servicio { get; set; }
        public string telefono { get; set; }
    }
}
