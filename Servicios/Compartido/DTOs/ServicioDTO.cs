using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compartido.DTOs
{
    public class ServicioDTO
    {
        public int Id { get; set; }
        public string? Nombre { get; set; }
        public decimal? Valor { get; set; }
        public int? Tiempo { get; set; }
    }
}
