using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compartido.DTOs
{
    public partial class BloqueoDTO
    {
        public int Id { get; set; }
        public DateTime FechaIni { get; set; }
        public DateTime FechaFin { get; set; }
        public int usuario { get; set; }
        public string Motivo { get; set; }
        public int empresa { get; set; }
        public int Hora { get; set; }
    }
}
