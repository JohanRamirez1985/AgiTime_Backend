using System;
using System.Collections.Generic;

namespace Agendamiento.API.Data.Modelos
{
    public partial class Sede
    {
        public Sede()
        {
            Usuarios = new HashSet<Usuario>();
        }

        public int id { get; set; }
        public int idCiudad { get; set; }
        public string Nombre { get; set; } = null!;
        public string direccion { get; set; } = null!;
        public string telefono { get; set; } = null!;
        public string barrio { get; set; } = null!;
        public string estado { get; set; } = null!;

        public virtual ICollection<Usuario> Usuarios { get; set; }
    }
}
