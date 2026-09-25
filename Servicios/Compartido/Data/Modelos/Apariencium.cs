using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class Apariencium
    {
        public int id { get; set; }
        public int empresa { get; set; }
        public string tipoLetra { get; set; }
        public string colorLetra { get; set; }
        public string colorFondo { get; set; }
        public string colorPiePagina { get; set; }
        public string colorCabecera { get; set; }
        public bool? estado { get; set; }
    }
}
