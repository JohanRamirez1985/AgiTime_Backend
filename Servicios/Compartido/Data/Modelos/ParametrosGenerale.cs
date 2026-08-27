using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class ParametrosGenerale
    {
        public int id { get; set; }
        public string tipo { get; set; }
        public string valor { get; set; }
        public bool estado { get; set; }
    }
}
