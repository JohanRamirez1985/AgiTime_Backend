using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compartido.DTOs
{
    public partial class CitaDTO
    {
        public int Id { get; set; }
        public DateTime FechaIni { get; set; }
        public DateTime FechaFin { get; set; }
        public int recurso { get; set; }
        public int Cliente { get; set; }
        public int Servicio { get; set; }
        public decimal Valor { get; set; }
        public string Estado { get; set; }
        public string UsuarioCreacion { get; set; }
        public DateTime FechaCreacion { get; set; }
        public int empresa { get; set; }

        public string? NombreCliente { get; set; }
        public string? NombreServicio { get; set; }
    }
}
