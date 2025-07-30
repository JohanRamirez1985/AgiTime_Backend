using System;
using System.Collections.Generic;

namespace Clientes.API.Data.Modelos
{
    public partial class Usuario
    {
        public Usuario()
        {
            BloqueosHorarios = new HashSet<BloqueosHorario>();
            DetalleProductos = new HashSet<DetalleProducto>();
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
        public bool recurso { get; set; }
        public string? foto { get; set; }

        public virtual Genero generoNavigation { get; set; } = null!;
        public virtual Sede sedeNavigation { get; set; } = null!;
        public virtual TipoDocumento tipoDocumentoNavigation { get; set; } = null!;
        public virtual ICollection<BloqueosHorario> BloqueosHorarios { get; set; }
        public virtual ICollection<DetalleProducto> DetalleProductos { get; set; }

        public virtual ICollection<Rol> idRols { get; set; }
    }
}
