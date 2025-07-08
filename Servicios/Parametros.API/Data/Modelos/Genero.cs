using System;
using System.Collections.Generic;

namespace Parametros.API.Data.Modelos
{
    public partial class Genero
    {
        public Genero()
        {
            Usuarios = new HashSet<Usuario>();
        }

        public int id { get; set; }
        public string nombre { get; set; } = null!;

        public virtual ICollection<Usuario> Usuarios { get; set; }
    }
}
