using System;
using System.Collections.Generic;

namespace Usuarios.API.Models
{
    public partial class Usuario
    {
        public int Id { get; set; }
        public int Sede { get; set; }
        public string? Nombre { get; set; }
        public string? Identificacion { get; set; }
        public string? Email { get; set; }
        public string? Telefono { get; set; }
        public string? Login { get; set; }
        public string? Password { get; set; }
        public string? Estado { get; set; }
        public string? Bd { get; set; }
        public string? Celular { get; set; }
        public DateTime Fecharegistro { get; set; }
    }
}
