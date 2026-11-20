using System;
using System.Collections.Generic;

namespace Compartido.DTOs
{
    public partial class PromoComboDTO
    {
        public int Id { get; set; }
        public int empresa { get; set; }
        public decimal? Valor { get; set; }
        public DateTime? FechaIni { get; set; }
        public DateTime? FechaFin { get; set; }
        public List<int> Servicios { get; set; } = new List<int>();
    }
}
