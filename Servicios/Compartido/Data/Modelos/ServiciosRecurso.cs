using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class ServiciosRecurso
    {
        public int id { get; set; }
        public int? servicio { get; set; }
        public int? recurso { get; set; }

        public virtual Servicio servicioNavigation { get; set; }
    }
}
