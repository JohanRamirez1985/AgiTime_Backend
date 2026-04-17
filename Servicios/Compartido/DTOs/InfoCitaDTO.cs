using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compartido.DTOs
{
    public partial class InfoCitaDTO
    {
        public int idCita { get; set; }
        public DateTime fechaIni { get; set; }
        public string servicio { get; set; }
        public bool pagoCita { get; set; }
        public bool pagoServicio { get; set; }
        public decimal valor { get; set; }
        public bool bono { get; set; }
    }
}
