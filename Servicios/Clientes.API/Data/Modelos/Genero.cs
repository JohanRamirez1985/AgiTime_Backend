using System;
using System.Collections.Generic;

namespace Clientes.API.Data.Modelos
{
    public partial class Genero
    {
        public Genero()
        {
            Clientes = new HashSet<Cliente>();
        }

        public int id { get; set; }
        public string nombre { get; set; } = null!;

        public virtual ICollection<Cliente> Clientes { get; set; }
    }
}
