using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class TipoDocumento
    {
        public TipoDocumento()
        {
            Clientes = new HashSet<Cliente>();
            Usuarios = new HashSet<Usuario>();
        }

        public int id { get; set; }
        public string nombre { get; set; }

        public virtual ICollection<Cliente> Clientes { get; set; }
        public virtual ICollection<Usuario> Usuarios { get; set; }
    }
}
