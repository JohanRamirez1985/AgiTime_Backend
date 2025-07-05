using System;
using System.Collections.Generic;

namespace Usuarios.API.Models
{
    public partial class UsuarioRol
    {
        public int Id { get; set; }
        public int IdRol { get; set; }
        public int Idusuario { get; set; }
        public DateTime FechaRegistro { get; set; }

        public virtual Usuario IdusuarioNavigation { get; set; } = null!;
    }
}
