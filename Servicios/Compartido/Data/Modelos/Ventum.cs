using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class Ventum
    {
        public Ventum()
        {
            VentaDetalles = new HashSet<VentaDetalle>();
        }

        public int id { get; set; }
        public string factura { get; set; }
        public DateTime fechaVenta { get; set; }
        public int usuarioVenta { get; set; }

        public virtual ICollection<VentaDetalle> VentaDetalles { get; set; }
    }
}
