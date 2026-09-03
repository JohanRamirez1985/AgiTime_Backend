using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compartido.DTOs
{
    public partial class OrdenDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Estado { get; set; }
        public string Observaciones { get; set; }
        public int cliente { get; set; }
        public int empresa { get; set; }
        public string UsuarioCreacion { get; set; }
        public DateTime FechaCreacion { get; set; }
        public int cantidad { get; set; }
        public string NombreServicio { get; set; }
        public List<SesionesDTO> sesiones { get; set; } = new();
    }
}
