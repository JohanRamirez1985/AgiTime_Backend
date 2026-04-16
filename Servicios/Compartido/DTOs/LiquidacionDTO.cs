using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compartido.DTOs
{
    public partial class LiquidacionDTO
    {
        public int id { get; set; }
        public int cita { get; set; }
        public decimal valorServicio { get; set; }
        public decimal valorBarberia { get; set; }
        public decimal valorBarbero { get; set; }
        public bool bono { get; set; }
        public DateTime fechaCita { get; set; }
        public string usuarioCreacion { get; set; }
        public DateTime fechaCreacion { get; set; }
        public int empresa { get; set; }
        public string barberos { get; set; }
        public DateTime fechaIni { get; set; }
        public DateTime fechaFin { get; set; }
    }
}
