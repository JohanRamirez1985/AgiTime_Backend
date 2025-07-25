using System;
using System.Collections.Generic;

namespace Productos.API.Data.Modelos
{
    public partial class Sede
    {
        public Sede()
        {
            Recursos = new HashSet<Recurso>();
            Usuarios = new HashSet<Usuario>();
        }

        public int id { get; set; }
        public int idCiudad { get; set; }
        public string Nombre { get; set; } = null!;
        public string direccion { get; set; } = null!;
        public string telefono { get; set; } = null!;
        public string barrio { get; set; } = null!;
        public string estado { get; set; } = null!;

        public virtual ICollection<Recurso> Recursos { get; set; }
        public virtual ICollection<Usuario> Usuarios { get; set; }
    }
}
