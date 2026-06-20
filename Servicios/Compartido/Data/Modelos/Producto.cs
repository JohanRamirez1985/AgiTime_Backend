using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class Producto
    {
        public Producto()
        {
            ProductoLotes = new HashSet<ProductoLote>();
            VentaDetalles = new HashSet<VentaDetalle>();
        }

        public int id { get; set; }
        public string nombre { get; set; }
        public string codigo { get; set; }
        public decimal precioVenta { get; set; }
        public string estado { get; set; }
        public decimal? porcentaje { get; set; }

        public virtual ICollection<ProductoLote> ProductoLotes { get; set; }
        public virtual ICollection<VentaDetalle> VentaDetalles { get; set; }
    }
}
