using System;
using System.Collections.Generic;

namespace Clientes.API.Data.Modelos
{
    public partial class BloqueosHorario
    {
        public int id { get; set; }
        public DateTime fechaInicio { get; set; }
        public DateTime? fechaFin { get; set; }
        public int hora { get; set; }
        public string motivo { get; set; } = null!;
        public int usuario { get; set; }

        public virtual Usuario usuarioNavigation { get; set; } = null!;
    }
}
