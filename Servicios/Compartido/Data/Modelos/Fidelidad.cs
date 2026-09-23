using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class Fidelidad
    {
        public int id { get; set; }
        public int cliente { get; set; }
        public int cantidad { get; set; }
    }
}
