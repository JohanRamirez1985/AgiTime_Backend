using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class Reporte
    {
        public int id { get; set; }
        public string nombre { get; set; }
        public string descripcion { get; set; }
        public string procedimiento { get; set; }
        public bool? activo { get; set; }
        public int? orden { get; set; }
    }
}
