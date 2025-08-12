using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class Producto
    {
        public Producto()
        {
            DetalleProductos = new HashSet<DetalleProducto>();
            VentaDetalles = new HashSet<VentaDetalle>();
        }

        public int id { get; set; }
        public string Nombre { get; set; }
        public string codigo { get; set; }
        public decimal precio { get; set; }
        public int stock { get; set; }
        public string estado { get; set; }

        public virtual ICollection<DetalleProducto> DetalleProductos { get; set; }
        public virtual ICollection<VentaDetalle> VentaDetalles { get; set; }
    }
}
