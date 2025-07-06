using System;
using System.Collections.Generic;

namespace Clientes.API.Models
{
    public partial class TipoDocumento
    {
        public TipoDocumento()
        {
            Clientes = new HashSet<Cliente>();
        }

        public int Id { get; set; }
        public string Nombre { get; set; } = null!;

        public virtual ICollection<Cliente> Clientes { get; set; }
    }
}
