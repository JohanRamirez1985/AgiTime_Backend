using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class VentaDetalleLote
    {
        public int id { get; set; }
        public int ventaDetalle { get; set; }
        public int productoLote { get; set; }
        public int cantidad { get; set; }

        public virtual ProductoLote productoLoteNavigation { get; set; }
        public virtual VentaDetalle ventaDetalleNavigation { get; set; }
    }
}
