using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class CitaServicio
    {
        public int id { get; set; }
        public int cita { get; set; }
        public int servicio { get; set; }

        public virtual Cita citaNavigation { get; set; }
        public virtual Servicio servicioNavigation { get; set; }
    }
}
