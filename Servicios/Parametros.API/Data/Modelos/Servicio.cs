using System;
using System.Collections.Generic;

namespace Parametros.API.Data.Modelos
{
    public partial class Servicio
    {
        public Servicio()
        {
            Cita = new HashSet<Cita>();
        }

        public int id { get; set; }
        public string nombre { get; set; } = null!;
        public decimal? valor { get; set; }
        public string estado { get; set; } = null!;

        public virtual ICollection<Cita> Cita { get; set; }
    }
}
