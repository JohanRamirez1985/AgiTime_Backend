using System;
using System.Collections.Generic;

namespace Clientes.API.Data.Modelos
{
    public partial class FuncionalidadesSet
    {
        public int id { get; set; }
        public int funcionalidad { get; set; }
        public int rol { get; set; }
        public string estado { get; set; } = null!;

        public virtual Funcionalidade funcionalidadNavigation { get; set; } = null!;
        public virtual Rol rolNavigation { get; set; } = null!;
    }
}
