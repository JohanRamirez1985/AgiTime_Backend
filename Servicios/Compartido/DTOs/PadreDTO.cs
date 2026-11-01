using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compartido.DTOs
{
    public partial class PadreDTO
    {
        public int Id { get; set; }
        public DateTime FechaIni { get; set; }
        public int empresa { get; set; }
        public string UsuarioCreacion { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string UsuarioCancelacion { get; set; }
        public DateTime FechaCancelacion { get; set; }
        public bool validaHorario { get; set; }
        public List<CitaDTO> Citas { get; set; } = new();
        public ClienteDTO clienteInfo { get; set; }
        public string alias { get; set; }
    }
}
