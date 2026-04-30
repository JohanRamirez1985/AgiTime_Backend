using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class CitaPadre
    {
        public int id { get; set; }
        public int cita { get; set; }
        public int padre { get; set; }

        public virtual Cita citaNavigation { get; set; }
        public virtual Padre padreNavigation { get; set; }
    }
}
