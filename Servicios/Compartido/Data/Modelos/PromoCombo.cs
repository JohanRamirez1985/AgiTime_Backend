using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class PromoCombo
    {
        public PromoCombo()
        {
            PromoCombosServicios = new HashSet<PromoComboServicio>();
        }

        public int id { get; set; }
        public decimal? valor { get; set; }
        public DateTime? fechaIni { get; set; }
        public DateTime? fechaFin { get; set; }

        public virtual ICollection<PromoComboServicio> PromoCombosServicios { get; set; }
    }
}
