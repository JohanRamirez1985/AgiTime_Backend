using System;
using System.Collections.Generic;

namespace Agendamiento.API.Data.Modelos
{
    public partial class Parametro
    {
        public int id { get; set; }
        public string tipo { get; set; } = null!;
        public string valor { get; set; } = null!;
        public bool estado { get; set; }
    }
}
