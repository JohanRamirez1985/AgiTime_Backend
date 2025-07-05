using System;
using System.Collections.Generic;

namespace Usuarios.API.Models
{
    public partial class Empresa
    {
        public Empresa()
        {
            EmpresaUsuarios = new HashSet<EmpresaUsuario>();
            Sedes = new HashSet<Sede>();
        }

        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public string Nit { get; set; } = null!;
        public string Logo { get; set; } = null!;
        public string ConexionBd { get; set; } = null!;
        public string Estado { get; set; } = null!;

        public virtual ICollection<EmpresaUsuario> EmpresaUsuarios { get; set; }
        public virtual ICollection<Sede> Sedes { get; set; }
    }
}
