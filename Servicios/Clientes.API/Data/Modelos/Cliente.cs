using System;
using System.Collections.Generic;

namespace Clientes.API.Data.Modelos
{
    public partial class Cliente
    {
        public Cliente()
        {
            Cita = new HashSet<Cita>();
        }

        public int id { get; set; }
        public string nombre { get; set; } = null!;
        public string correo { get; set; } = null!;
        public string login { get; set; } = null!;
        public string password { get; set; } = null!;
        public string telefono { get; set; } = null!;
        public DateTime fechaNacimiento { get; set; }
        public int genero { get; set; }
        public int tipoDocumento { get; set; }
        public string documento { get; set; } = null!;
        public string estado { get; set; } = null!;
        public DateTime fechaCreacion { get; set; }

        public virtual Genero generoNavigation { get; set; } = null!;
        public virtual TipoDocumento tipoDocumentoNavigation { get; set; } = null!;
        public virtual ICollection<Cita> Cita { get; set; }
    }
}
