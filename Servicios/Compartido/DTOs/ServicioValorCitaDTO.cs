using System.Collections.Generic;
using System.Linq;

namespace Compartido.DTOs
{
    /// <summary>
    /// Servicio de una cita con el valor que le corresponde.
    /// Se serializa como { id, nombre, valor }.
    /// </summary>
    public class ServicioValorCitaDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public decimal Valor { get; set; }

        /// <summary>
        /// Cuando la cita usa su propio valor (bono, fidelidad, descuento, combo o algún servicio en 0),
        /// el valor de la cita se asigna una sola vez al primer servicio y los demás quedan en 0,
        /// para que la suma de los servicios sea igual al valor de la cita.
        /// Si no, cada servicio conserva su valor.
        /// </summary>
        public static List<ServicioValorCitaDTO> Repartir(List<ServicioValorCitaDTO> servicios, bool usaValorCita, decimal valorCita)
        {
            if (!usaValorCita)
                return servicios;

            return servicios
                .Select((s, i) => new ServicioValorCitaDTO
                {
                    Id = s.Id,
                    Nombre = s.Nombre,
                    Valor = i == 0 ? valorCita : 0
                })
                .ToList();
        }
    }
}
