using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class Servicio
    {
        public Servicio()
        {
            CitaServicios = new HashSet<CitaServicio>();
            ServiciosParametros = new HashSet<ServiciosParametro>();
            ServiciosRecursos = new HashSet<ServiciosRecurso>();
        }

        public int id { get; set; }
        public string nombre { get; set; }
        public decimal? valor { get; set; }
        public int? tiempo { get; set; }
        public int? orden { get; set; }
        public int sede { get; set; }
        public string estado { get; set; }
        public bool? esPago { get; set; }
        public string observaciones { get; set; }

        public virtual Sede sedeNavigation { get; set; }
        public virtual ICollection<CitaServicio> CitaServicios { get; set; }
        public virtual ICollection<ServiciosParametro> ServiciosParametros { get; set; }
        public virtual ICollection<ServiciosRecurso> ServiciosRecursos { get; set; }
    }
}
