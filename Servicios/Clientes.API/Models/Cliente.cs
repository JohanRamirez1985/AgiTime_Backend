using System;
using System.Collections.Generic;

namespace Clientes.API.Models
{
    public partial class Cliente
    {
        public Cliente()
        {
            EmpresaClientes = new HashSet<EmpresaCliente>();
        }

        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public string Correo { get; set; } = null!;
        public string Login { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string Telefono { get; set; } = null!;
        public DateTime? FechaNacimiento { get; set; }
        public string? Genero { get; set; }
        public int TipoDocumento { get; set; }
        public string Documento { get; set; } = null!;
        public string Estado { get; set; } = null!;

        public virtual TipoDocumento TipoDocumentoNavigation { get; set; } = null!;
        public virtual ICollection<EmpresaCliente> EmpresaClientes { get; set; }
    }
}
