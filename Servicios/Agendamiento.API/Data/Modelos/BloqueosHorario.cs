using System;
using System.Collections.Generic;

namespace Agendamiento.API.Data.Modelos
{
    public partial class BloqueosHorario
    {
        public int id { get; set; }
        public int tipoBloqueo { get; set; }
        public DateTime? fechaInicio { get; set; }
        public DateTime? fechaFin { get; set; }
        public string? diasSemana { get; set; }
        public TimeSpan horaInicio { get; set; }
        public TimeSpan horaFin { get; set; }
        public string motivo { get; set; } = null!;
        public int usuario { get; set; }
        public DateTime fechaCreacion { get; set; }

        public virtual Usuario usuarioNavigation { get; set; } = null!;
    }
}
