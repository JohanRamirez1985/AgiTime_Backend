using System;
using System.Collections.Generic;

namespace Usuarios.API.Data.Modelos
{
    public partial class Rol
    {
        public Rol()
        {
            idUsuarios = new HashSet<Usuario>();
        }

        public int id { get; set; }
        public string nombre { get; set; } = null!;

        public virtual ICollection<Usuario> idUsuarios { get; set; }
    }
}
