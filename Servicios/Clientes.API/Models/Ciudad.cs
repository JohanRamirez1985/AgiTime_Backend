using System;
using System.Collections.Generic;

namespace Clientes.API.Models
{
    public partial class Ciudad
    {
        public Ciudad()
        {
            Sedes = new HashSet<Sede>();
        }

        public int Id { get; set; }
        public string Nombre { get; set; } = null!;

        public virtual ICollection<Sede> Sedes { get; set; }
    }
}
