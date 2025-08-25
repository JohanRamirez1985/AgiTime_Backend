using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
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
        public string nombre { get; set; }
        public int tipoDocumento { get; set; }
        public string documento { get; set; }
        public string correo { get; set; }
        public string login { get; set; }
        public string password { get; set; }
        public string telefono { get; set; }
        public int genero { get; set; }
        public DateTime fechaNacimiento { get; set; }
        public string estado { get; set; }
        public DateTime fechaCreacion { get; set; }
        public int sede { get; set; }
        public bool recurso { get; set; }
        public string foto { get; set; }
        public int? porcentaje { get; set; }

        public virtual Genero generoNavigation { get; set; }
        public virtual Sede sedeNavigation { get; set; }
        public virtual TipoDocumento tipoDocumentoNavigation { get; set; }
        public virtual ICollection<BloqueosHorario> BloqueosHorarios { get; set; }
        public virtual ICollection<DetalleProducto> DetalleProductos { get; set; }

        public virtual ICollection<Rol> idRols { get; set; }
    }
}
