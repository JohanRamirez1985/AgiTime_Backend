using System;
using System.Collections.Generic;

namespace Usuarios.API.Data.Modelos
{
    public partial class Cita
    {
        public int id { get; set; }
        public DateTime fechaInicio { get; set; }
        public DateTime fechaFin { get; set; }
        public int usuario { get; set; }
        public int cliente { get; set; }
        public decimal? valor { get; set; }
        public string estado { get; set; } = null!;

        public virtual Cliente clienteNavigation { get; set; } = null!;
        public virtual Usuario usuarioNavigation { get; set; } = null!;
    }
}
