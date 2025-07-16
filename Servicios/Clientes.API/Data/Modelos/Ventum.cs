using System;
using System.Collections.Generic;

namespace Clientes.API.Data.Modelos
{
    public partial class Ventum
    {
        public int id { get; set; }
        public string factura { get; set; } = null!;
        public DateTime fechaVenta { get; set; }
        public int usuarioVenta { get; set; }
    }
}
