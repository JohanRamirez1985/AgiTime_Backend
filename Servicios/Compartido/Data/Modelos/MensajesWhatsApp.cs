using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class MensajesWhatsApp
    {
        public int id { get; set; }
        public string tipo { get; set; }
        public string mensaje { get; set; }
        public string observacion { get; set; }
    }
}
