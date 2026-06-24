using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class ProductoLote
    {
        public ProductoLote()
        {
            VentaDetalleLotes = new HashSet<VentaDetalleLote>();
        }

        public int id { get; set; }
        public int producto { get; set; }
        public int cantidadInicial { get; set; }
        public int cantidadDisponible { get; set; }
        public decimal precioCosto { get; set; }
        public DateTime fechaIngreso { get; set; }
        public string estado { get; set; }
        public int? usuarioCreacion { get; set; }
        public DateTime fechaCreacion { get; set; }

        public virtual Producto productoNavigation { get; set; }
        public virtual ICollection<VentaDetalleLote> VentaDetalleLotes { get; set; }
    }
}
