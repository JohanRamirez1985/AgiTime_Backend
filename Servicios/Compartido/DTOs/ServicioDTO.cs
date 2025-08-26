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
        public int? Orden { get; set; }
        public int Sede { get; set; }
        public string Estado { get; set; }
        public bool? EsPago { get; set; }
        public int empresa { get; set; }
        public string? NombreSede { get; set; }
    }
}
