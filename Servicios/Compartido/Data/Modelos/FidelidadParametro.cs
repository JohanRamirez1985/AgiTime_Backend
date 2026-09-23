using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class FidelidadParametro
    {
        public int id { get; set; }
        public int cantidad { get; set; }
        public decimal porcentaje { get; set; }
        public bool estado { get; set; }
    }
}
