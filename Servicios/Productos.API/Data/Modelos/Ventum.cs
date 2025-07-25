using System;
using System.Collections.Generic;

namespace Productos.API.Data.Modelos
{
    public partial class Ventum
    {
        public Ventum()
        {
            VentaDetalles = new HashSet<VentaDetalle>();
        }

        public int id { get; set; }
        public string factura { get; set; } = null!;
        public DateTime fechaVenta { get; set; }
        public int usuarioVenta { get; set; }

        public virtual ICollection<VentaDetalle> VentaDetalles { get; set; }
    }
}
