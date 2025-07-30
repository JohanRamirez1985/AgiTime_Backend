using System;
using System.Collections.Generic;

namespace Clientes.API.Data.Modelos
{
    public partial class Cita
    {
        public Cita()
        {
            CitaServicios = new HashSet<CitaServicio>();
        }

        public int id { get; set; }
        public DateTime fechaInicio { get; set; }
        public DateTime? fechaFin { get; set; }
        public int hora { get; set; }
        public int recurso { get; set; }
        public int cliente { get; set; }
        public decimal? valor { get; set; }
        public string estado { get; set; } = null!;
        public DateTime fechaCreacion { get; set; }
        public string? usuarioCreacion { get; set; }

        public virtual Cliente clienteNavigation { get; set; } = null!;
        public virtual ICollection<CitaServicio> CitaServicios { get; set; }
    }
}
