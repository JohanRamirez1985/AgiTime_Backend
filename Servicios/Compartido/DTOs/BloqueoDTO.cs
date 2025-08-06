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
        public int TipoBloqueo { get; set; } 
        public int Usuario { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public string HoraInicio { get; set; } 
        public string HoraFin { get; set; }
        public string? DiasSemana { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public int empresa { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}
