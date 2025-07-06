using System;
using System.Collections.Generic;

namespace Clientes.API.Models
{
    public partial class Sede
    {
        public int Id { get; set; }
        public int IdEmpresa { get; set; }
        public int IdCiudad { get; set; }
        public string Direccion { get; set; } = null!;
        public string Telefono { get; set; } = null!;
        public string Barrio { get; set; } = null!;
        public string Estado { get; set; } = null!;

        public virtual Ciudad IdCiudadNavigation { get; set; } = null!;
        public virtual Empresa IdEmpresaNavigation { get; set; } = null!;
    }
}
