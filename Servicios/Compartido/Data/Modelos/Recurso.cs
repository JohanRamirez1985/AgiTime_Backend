using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class Recurso
    {
        public int id { get; set; }
        public string nombre { get; set; }
        public int sede { get; set; }
        public string estado { get; set; }
        public DateTime fechaCreacion { get; set; }

        public virtual Sede sedeNavigation { get; set; }
    }
}
