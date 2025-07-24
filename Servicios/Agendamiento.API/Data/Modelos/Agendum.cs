using System;
using System.Collections.Generic;

namespace Agendamiento.API.Data.Modelos
{
    public partial class Agendum
    {
        public int id { get; set; }
        public TimeSpan valor { get; set; }
        public bool estado { get; set; }
    }
}
