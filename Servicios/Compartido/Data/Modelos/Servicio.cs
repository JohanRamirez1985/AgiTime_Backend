using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class Servicio
    {
        public Servicio()
        {
            CitaServicios = new HashSet<CitaServicio>();
        }

        public int id { get; set; }
        public string nombre { get; set; }
        public decimal? valor { get; set; }
        public int? tiempo { get; set; }
        public string estado { get; set; }

        public virtual ICollection<CitaServicio> CitaServicios { get; set; }
    }
}
