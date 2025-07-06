using System;
using System.Collections.Generic;

namespace Clientes.API.Models
{
    public partial class EmpresaCliente
    {
        public int Id { get; set; }
        public int IdEmpresa { get; set; }
        public int IdCliente { get; set; }
        public DateTime FechaRegistro { get; set; }

        public virtual Cliente IdClienteNavigation { get; set; } = null!;
        public virtual Empresa IdEmpresaNavigation { get; set; } = null!;
    }
}
