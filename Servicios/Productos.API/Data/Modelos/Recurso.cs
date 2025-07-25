using System;
using System.Collections.Generic;

namespace Productos.API.Data.Modelos
{
    public partial class Recurso
    {
        public int id { get; set; }
        public string nombre { get; set; } = null!;
        public int sede { get; set; }
        public string estado { get; set; } = null!;
        public DateTime fechaCreacion { get; set; }

        public virtual Sede sedeNavigation { get; set; } = null!;
    }
}
