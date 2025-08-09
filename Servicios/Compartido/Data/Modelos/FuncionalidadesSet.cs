using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class FuncionalidadesSet
    {
        public int id { get; set; }
        public int funcionalidad { get; set; }
        public int rol { get; set; }
        public string estado { get; set; }

        public virtual Funcionalidade funcionalidadNavigation { get; set; }
        public virtual Rol rolNavigation { get; set; }
    }
}
