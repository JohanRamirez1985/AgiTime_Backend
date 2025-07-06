using System;
using System.Collections.Generic;

namespace Usuarios.API.Models
{
    public partial class TipoDocumento
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
    }
}
