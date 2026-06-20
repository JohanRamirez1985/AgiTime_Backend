using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class VentaDetalle
    {
        public VentaDetalle()
        {
            VentaDetalleLotes = new HashSet<VentaDetalleLote>();
        }

        public int id { get; set; }
        public int venta { get; set; }
        public int producto { get; set; }
        public int cantidad { get; set; }
        public decimal valor { get; set; }
        public decimal total { get; set; }

        public virtual Producto productoNavigation { get; set; }
        public virtual Ventum ventaNavigation { get; set; }
        public virtual ICollection<VentaDetalleLote> VentaDetalleLotes { get; set; }
    }
}
