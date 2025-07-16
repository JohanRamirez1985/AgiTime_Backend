using System;
using System.Collections.Generic;

namespace Usuarios.API.Data.Modelos
{
    public partial class DetalleProducto
    {
        public int id { get; set; }
        public int producto { get; set; }
        public int cantidadIngresada { get; set; }
        public int cantidadStock { get; set; }
        public int cantidadNueva { get; set; }
        public int usuarioCreacion { get; set; }
        public DateTime fechaCreacion { get; set; }

        public virtual Producto productoNavigation { get; set; } = null!;
        public virtual Usuario usuarioCreacionNavigation { get; set; } = null!;
    }
}
