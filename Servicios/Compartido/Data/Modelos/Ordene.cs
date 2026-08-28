using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class Ordene
    {
        public Ordene()
        {
            Sesiones = new HashSet<Sesione>();
        }

        public int id { get; set; }
        public string nombre { get; set; }
        public string estado { get; set; }
        public string observaciones { get; set; }
        public string usuarioCreacion { get; set; }
        public DateTime fechaCreacion { get; set; }

        public virtual ICollection<Sesione> Sesiones { get; set; }
    }
}
