using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class Cliente
    {
        public int id { get; set; }
        public string nombre { get; set; }
        public string correo { get; set; }
        public string telefono { get; set; }
        public DateTime? fechaNacimiento { get; set; }
        public int? genero { get; set; }
        public int? tipoDocumento { get; set; }
        public string documento { get; set; }
        public string estado { get; set; }
        public DateTime fechaCreacion { get; set; }
        public string alias { get; set; }
        public int? usuarioCreacion { get; set; }

        public virtual Genero generoNavigation { get; set; }
        public virtual TipoDocumento tipoDocumentoNavigation { get; set; }
    }
}
