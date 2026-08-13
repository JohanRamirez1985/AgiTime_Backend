using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class Sede
    {
        public Sede()
        {
            Recursos = new HashSet<Recurso>();
            Servicios = new HashSet<Servicio>();
            Usuarios = new HashSet<Usuario>();
        }

        public int id { get; set; }
        public int idCiudad { get; set; }
        public string Nombre { get; set; }
        public string direccion { get; set; }
        public string telefono { get; set; }
        public string barrio { get; set; }
        public string estado { get; set; }
        public string longitud { get; set; }
        public string latitud { get; set; }

        public virtual ICollection<Recurso> Recursos { get; set; }
        public virtual ICollection<Servicio> Servicios { get; set; }
        public virtual ICollection<Usuario> Usuarios { get; set; }
    }
}
