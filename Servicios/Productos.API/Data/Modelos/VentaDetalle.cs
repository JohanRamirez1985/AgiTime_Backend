using System;
using System.Collections.Generic;

namespace Productos.API.Data.Modelos
{
    public partial class VentaDetalle
    {
        public int id { get; set; }
        public int venta { get; set; }
        public int producto { get; set; }
        public int cantidad { get; set; }
        public decimal valor { get; set; }
        public decimal total { get; set; }

        public virtual Producto productoNavigation { get; set; } = null!;
        public virtual Ventum ventaNavigation { get; set; } = null!;
    }
}
