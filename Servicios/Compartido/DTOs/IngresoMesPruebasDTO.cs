using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Compartido.DTOs
{
    public partial class IngresoMesPruebasDTO
    {
        public int id { get; set; }
        public decimal valorServicio { get; set; }
        public decimal valorBarberia { get; set; }
        public decimal valorBarbero { get; set; }
        public DateTime fecha { get; set; }
        public string usuarioCreacion { get; set; }
        public DateTime fechaCreacion { get; set; }
        public int empresa { get; set; }
        public int numero { get; set; }
        public int recurso { get; set; }
        public string sede { get; set; }
    }
}
