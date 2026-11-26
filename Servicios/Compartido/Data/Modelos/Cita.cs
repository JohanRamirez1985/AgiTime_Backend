using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class Cita
    {
        public Cita()
        {
            CitaPadres = new HashSet<CitaPadre>();
            CitaServicios = new HashSet<CitaServicio>();
            LiquidacionCita = new HashSet<LiquidacionCitum>();
        }

        public int id { get; set; }
        public DateTime fechaInicio { get; set; }
        public DateTime? fechaFin { get; set; }
        public int hora { get; set; }
        public int recurso { get; set; }
        public int? cliente { get; set; }
        public decimal? valor { get; set; }
        public string estado { get; set; }
        public bool? pago { get; set; }
        public bool? bono { get; set; }
        public bool? liquidado { get; set; }
        public DateTime fechaCreacion { get; set; }
        public string usuarioCreacion { get; set; }
        public DateTime? fechaCancelacion { get; set; }
        public string usuarioCancelacion { get; set; }
        public int? orden { get; set; }
        public string metodoPago { get; set; }
        public bool? fidelidad { get; set; }
        public bool? descuento { get; set; }
        public string alias { get; set; }
        public string tipoDescuento { get; set; }
        public bool? comboPromo { get; set; }
        public int? sede { get; set; }

        public virtual ICollection<CitaPadre> CitaPadres { get; set; }
        public virtual ICollection<CitaServicio> CitaServicios { get; set; }
        public virtual ICollection<LiquidacionCitum> LiquidacionCita { get; set; }
    }
}
