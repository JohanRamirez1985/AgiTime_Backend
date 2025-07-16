using System;
using System.Collections.Generic;

namespace Usuarios.API.Data.Modelos
{
    public partial class Servicio
    {
        public int id { get; set; }
        public string nombre { get; set; } = null!;
        public decimal? valor { get; set; }
        public string estado { get; set; } = null!;
    }
}
