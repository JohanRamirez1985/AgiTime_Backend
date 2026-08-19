using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class IngresoMesPrueba
    {
        public int id { get; set; }
        public DateTime fecha { get; set; }
        public int recurso { get; set; }
        public int numero { get; set; }
        public decimal? valorServicio { get; set; }
        public decimal? valorBarberia { get; set; }
        public decimal? valorBarbero { get; set; }
        public string sede { get; set; }
        public DateTime fechaCreacion { get; set; }
        public string usuarioCreacion { get; set; }

        public virtual Usuario recursoNavigation { get; set; }
    }
}
