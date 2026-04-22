using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class LiquidacionCitum
    {
        public int id { get; set; }
        public int cita { get; set; }
        public DateTime? fechaCita { get; set; }
        public string tipo { get; set; }
        public decimal? valorServicio { get; set; }
        public decimal? valorBarberia { get; set; }
        public decimal? valorBarbero { get; set; }
        public int? numero { get; set; }
        public bool? bono { get; set; }
        public DateTime fechaCreacion { get; set; }
        public string usuarioCreacion { get; set; }

        public virtual Cita citaNavigation { get; set; }
    }
}
