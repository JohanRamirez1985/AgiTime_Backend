using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class ServiciosParametro
    {
        public int id { get; set; }
        public int? servicio { get; set; }
        public bool? alterno { get; set; }
        public bool? cliente { get; set; }
        public decimal? porcentaje { get; set; }

        public virtual Servicio servicioNavigation { get; set; }
    }
}
