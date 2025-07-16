using System;
using System.Collections.Generic;

namespace Clientes.API.Data.Modelos
{
    public partial class Producto
    {
        public Producto()
        {
            DetalleProductos = new HashSet<DetalleProducto>();
        }

        public int id { get; set; }
        public string Nombre { get; set; } = null!;
        public string codigo { get; set; } = null!;
        public decimal precio { get; set; }
        public int stock { get; set; }
        public string estado { get; set; } = null!;

        public virtual ICollection<DetalleProducto> DetalleProductos { get; set; }
    }
}
