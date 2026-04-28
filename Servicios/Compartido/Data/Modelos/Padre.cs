using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class Padre
    {
        public Padre()
        {
            CitaPadres = new HashSet<CitaPadre>();
        }

        public int id { get; set; }
        public DateTime fechaCita { get; set; }

        public virtual ICollection<CitaPadre> CitaPadres { get; set; }
    }
}
