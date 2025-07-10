using System;
using System.Collections.Generic;

namespace Usuarios.API.Data.Modelos
{
    public partial class Usuario
    {
        public Usuario()
        {
            idRols = new HashSet<Rol>();
        }

        public int id { get; set; }
        public string nombre { get; set; } = null!;
        public int tipoDocumento { get; set; }
        public string documento { get; set; } = null!;
        public string correo { get; set; } = null!;
        public string login { get; set; } = null!;
        public string password { get; set; } = null!;
        public string telefono { get; set; } = null!;
        public int genero { get; set; }
        public DateTime fechaNacimiento { get; set; }
        public string estado { get; set; } = null!;
        public DateTime fechaCreacion { get; set; }
        public int sede { get; set; }

        public virtual Genero generoNavigation { get; set; } = null!;
        public virtual Sede sedeNavigation { get; set; } = null!;
        public virtual TipoDocumento tipoDocumentoNavigation { get; set; } = null!;

        public virtual ICollection<Rol> idRols { get; set; }
    }
}
