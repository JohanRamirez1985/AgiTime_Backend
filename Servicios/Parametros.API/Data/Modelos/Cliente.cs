using System;
using System.Collections.Generic;

namespace Parametros.API.Data.Modelos
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
        public string telefono { get; set; } = null!;
        public DateTime? fechaNacimiento { get; set; }
        public int? genero { get; set; }
        public int? tipoDocumento { get; set; }
        public string? documento { get; set; }
        public string estado { get; set; } = null!;
        public DateTime fechaCreacion { get; set; }

        public virtual Genero? generoNavigation { get; set; }
        public virtual TipoDocumento? tipoDocumentoNavigation { get; set; }
        public virtual ICollection<Cita> Cita { get; set; }
    }
}
