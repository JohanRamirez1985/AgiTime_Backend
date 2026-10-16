using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class RotacionRecursoAutomatica
    {
        public int id { get; set; }
        public int sede { get; set; }
        public DateTime fecha { get; set; }
        public int ultimoRecurso { get; set; }
        public DateTime fechaActualizacion { get; set; }
    }
}
