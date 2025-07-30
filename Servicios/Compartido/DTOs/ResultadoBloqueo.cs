using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compartido.DTOs
{
    public partial class ResultadoBloqueo
    {
        public bool DiaCompletoBloqueado { get; set; }
        public List<TimeSpan> HorasBloqueadas { get; set; } = new();
    }
}
