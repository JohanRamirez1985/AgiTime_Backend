using System;
using System.Collections.Generic;

namespace Usuarios.API.Models
{
    public partial class EmpresaUsuario
    {
        public int Id { get; set; }
        public int IdEmpresa { get; set; }
        public int Idusuario { get; set; }
        public DateTime FechaRegistro { get; set; }

        public virtual Empresa IdEmpresaNavigation { get; set; } = null!;
        public virtual Usuario IdusuarioNavigation { get; set; } = null!;
    }
}
