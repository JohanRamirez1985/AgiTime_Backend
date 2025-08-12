using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class Rol
    {
        public Rol()
        {
            FuncionalidadesSets = new HashSet<FuncionalidadesSet>();
            idUsuarios = new HashSet<Usuario>();
        }

        public int id { get; set; }
        public string nombre { get; set; }

        public virtual ICollection<FuncionalidadesSet> FuncionalidadesSets { get; set; }

        public virtual ICollection<Usuario> idUsuarios { get; set; }
    }
}
