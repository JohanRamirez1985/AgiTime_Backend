using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compartido.DTOs
{
    public partial class SesionesDTO
    {
        public int Id { get; set; }
        public string Estado { get; set; }
        public int Numero { get; set; }
        public int Orden { get; set; }
        public int Cita { get; set; }
        public DateTime? FechaCita { get; set; }
    }
}
