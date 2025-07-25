using System;
using System.Collections.Generic;

namespace Agendamiento.API.Data.Modelos
{
    public partial class Cita
    {
        public int id { get; set; }
        public DateTime fechaInicio { get; set; }
        public DateTime? fechaFin { get; set; }
        public int hora { get; set; }
        public int recurso { get; set; }
        public int cliente { get; set; }
        public decimal? valor { get; set; }
        public int? servicio { get; set; }
        public string estado { get; set; } = null!;
        public DateTime fechaCreacion { get; set; }
        public string? usuarioCreacion { get; set; }

        public virtual Cliente clienteNavigation { get; set; } = null!;
        public virtual Servicio? servicioNavigation { get; set; }
    }
}
