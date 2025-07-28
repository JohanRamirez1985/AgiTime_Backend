using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compartido.DTOs
{
    public partial class VentaDTO
    {
        public int Id { get; set; }
        public DateTime fechaVenta { get; set; }
        public int usuarioVenta { get; set; }
        public string factura { get; set; }
        public int empresa { get; set; }

        public List<VentaDetalleDTO> Detalles { get; set; } = new();
    }
}
