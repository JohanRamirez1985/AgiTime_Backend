using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compartido.DTOs
{
    public partial class ClienteDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = null!;
        public string Correo { get; set; } = null!;
        public string Login { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string Telefono { get; set; } = null!;
        public DateTime FechaNacimiento { get; set; }
        public int Genero { get; set; }
        public int TipoDocumento { get; set; }
        public string Documento { get; set; } = null!;
        public string Estado { get; set; } = null!;
        public DateTime FechaCreacion { get; set; }
        public int empresa { get; set; }
        public int servicio { get; set; }

        public string? NombreGenero { get; set; }
        public string? NombreTipoDocumento { get; set; }
        public string? NombreRol { get; set; }
    }
}
