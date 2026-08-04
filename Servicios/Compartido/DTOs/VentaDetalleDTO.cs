using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compartido.DTOs
{
    public partial class VentaDetalleDTO
    {
        public int Id { get; set; }
        public int Producto { get; set; }
        public int NombreProducto { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioVenta { get; set; }
        public decimal Total { get; set; }
        public decimal Porcentaje { get; set; }
        public decimal Descuento { get; set; }
    }
}
