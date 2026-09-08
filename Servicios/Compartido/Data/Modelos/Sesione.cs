using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class Sesione
    {
        public int id { get; set; }
        public int numero { get; set; }
        public int orden { get; set; }
        public string estado { get; set; }
        public int? cita { get; set; }

        public virtual Ordene ordenNavigation { get; set; }
    }
}
