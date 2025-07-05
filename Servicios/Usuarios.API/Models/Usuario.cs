using System;
using System.Collections.Generic;

namespace Usuarios.API.Models
{
    public partial class Usuario
    {
        public Usuario()
        {
            EmpresaUsuarios = new HashSet<EmpresaUsuario>();
            UsuarioRols = new HashSet<UsuarioRol>();
        }

        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public string Correo { get; set; } = null!;
        public string Login { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string Telefono { get; set; } = null!;
        public DateTime? FechaNacimiento { get; set; }
        public string Estado { get; set; } = null!;

        public virtual ICollection<EmpresaUsuario> EmpresaUsuarios { get; set; }
        public virtual ICollection<UsuarioRol> UsuarioRols { get; set; }
    }
}
