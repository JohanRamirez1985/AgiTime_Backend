using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compartido.DTOs
{
    public partial class ReporteDTO
    {
        public int Id { get; set; }
        
        public string Nombre { get; set; }

        public string Descripcion { get; set; }

        public string Procedimiento { get; set; }

        public bool Activo { get; set; }

        public int? Orden { get; set; }

        public int empresa { get; set; }
    }
}
