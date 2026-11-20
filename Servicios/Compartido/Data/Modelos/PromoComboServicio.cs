using System;
using System.Collections.Generic;

namespace Compartido.Data.Modelos
{
    public partial class PromoComboServicio
    {
        public int id { get; set; }
        public int? promoCombo { get; set; }
        public int? servicio { get; set; }

        public virtual PromoCombo promoComboNavigation { get; set; }
        public virtual Servicio servicioNavigation { get; set; }
    }
}
