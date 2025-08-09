using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class Funcionalidade
    {
        public Funcionalidade()
        {
            FuncionalidadesSets = new HashSet<FuncionalidadesSet>();
        }

        public int id { get; set; }
        public string funcionalidad { get; set; }
        public string codigo { get; set; }

        public virtual ICollection<FuncionalidadesSet> FuncionalidadesSets { get; set; }
    }
}
