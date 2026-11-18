using Compartido.Constantes;
using Compartido.Data;
using Compartido.Data.Modelos;
using Compartido.DTOs;
using Compartido.Metodos;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Agendamiento.API.Servicios
{
    public class AgendamientoService
    {
        public string mensajeEmailCliente (string nombre, DateTime fecha, string nombreEmpresa, string sede, string logo)
        {
            var mensaje = "";

            mensaje = $@"
            <html>
            <head>
                <style>
                    body {{
                        font-family: Arial, sans-serif;
                        background-color: #f8f9fa;
                        color: #333;
                        padding: 20px;
                    }}
                    .container {{
                        max-width: 600px;
                        margin: auto;
                        background-color: white;
                        border-radius: 8px;
                        padding: 20px;
                        box-shadow: 0px 2px 8px rgba(0,0,0,0.1);
                    }}
                    .footer {{
                        font-size: 12px;
                        color: #777;
                        margin-top: 20px;
                        border-top: 1px solid #ddd;
                        padding-top: 10px;
                    }}
                    .logo {{
                        max-width: 150px;
                        margin-bottom: 20px;
                    }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <img class='logo' src='{logo}' alt='Logo de la empresa' />
                    <h1>Hola {nombre}</h1>
                    <p>Tu cita ha sido agendada en <strong>{nombreEmpresa}</strong>, en la sede <strong>{sede}</strong>, para el día <strong>{fecha:dd/MM/yyyy HH:mm}</strong>.</p>
                    <p>Gracias por utilizar nuestros servicios.</p>
                    <div class='footer'>
                        Este es un mensaje automático, por favor no responda a este correo.
                    </div>
                </div>
            </body>
            </html>";

            return mensaje;
        }

        public string mensajeEmailUsuario(string nombreCliente, string telefonoCliente, string nombreUsuario, DateTime fecha, string logo)
        {
            var mensaje = "";

            mensaje = $@"
            <html>
            <head>
                <style>
                    body {{
                        font-family: Arial, sans-serif;
                        background-color: #f8f9fa;
                        color: #333;
                        padding: 20px;
                    }}
                    .container {{
                        max-width: 650px;
                        margin: auto;
                        background-color: white;
                        border-radius: 8px;
                        padding: 20px;
                        box-shadow: 0px 2px 8px rgba(0,0,0,0.1);
                    }}
                    .footer {{
                        font-size: 12px;
                        color: #777;
                        margin-top: 20px;
                        border-top: 1px solid #ddd;
                        padding-top: 10px;
                    }}
                    .highlight {{
                        background-color: #e9ecef;
                        padding: 8px;
                        border-radius: 4px;
                    }}
                    .logo {{
                        max-width: 150px;
                        margin-bottom: 20px;
                    }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <img class='logo' src='{logo}' alt='Logo de la empresa' />
                    <h2>Notificación de nueva cita</h2>
                    <p>Hola <strong>{nombreUsuario}</strong>,</p>
                    <p>Se ha agendado una nueva cita con el siguiente detalle:</p>

                    <div class='highlight'>
                        <p><strong>Cliente:</strong> {nombreCliente}</p>
                        <p><strong>Celular:</strong> {telefonoCliente}</p>
                        <p><strong>Fecha y hora:</strong> {fecha:dd/MM/yyyy HH:mm}</p>
                    </div>

                    <div class='footer'>
                        Este es un mensaje automático del sistema, por favor no responda a este correo.
                    </div>
                </div>
            </body>
            </html>";

            return mensaje;
        }

        public RespuestaDTO ValidarDisponibilidad(ModeloContext ContextBD, List<CitaDTO> citas)
        {
            foreach (var detalleCita in citas)
            {
                var fechaInicio = detalleCita.FechaIni;
                var fechaFin = detalleCita.FechaIni.AddMinutes(detalleCita.Hora);

                var validaHora = ContextBD.Citas
                    .Where(c =>
                        c.recurso == detalleCita.recurso &&
                        (c.estado == Estados.Activo ||
                         c.estado == Estados.Asistio) &&

                        fechaInicio < c.fechaFin &&
                        fechaFin > c.fechaInicio
                    )
                    .FirstOrDefault();

                if (validaHora != null)
                {
                    var nombreUsuario = ContextBD.Usuarios
                        .Where(u => u.id == detalleCita.recurso)
                        .Select(u => u.nombre)
                        .FirstOrDefault();

                    return new RespuestaDTO
                    {
                        Disponible = false,
                        EnvioMensaje = false
                    };
                }
            }

            return new RespuestaDTO
            {
                Disponible = true,
                EnvioMensaje = false
            };
        }

        public RespuestaDTO ValidarDisponibilidadMultiple(ModeloContext ContextBD, List<CitaDTO> citas)
        {
            int indice = 0;

            foreach (var detalleCita in citas)
            {
                var fechaInicio = detalleCita.FechaIni;
                var fechaFin = detalleCita.FechaIni.AddMinutes(detalleCita.Hora);

                var validaHora = ContextBD.Citas
                    .Where(c =>
                        c.recurso == detalleCita.recurso &&
                        (c.estado == Estados.Activo ||
                         c.estado == Estados.Asistio) &&

                        fechaInicio < c.fechaFin &&
                        fechaFin > c.fechaInicio
                    )
                    .FirstOrDefault();

                if (validaHora != null)
                {
                    if (indice == 0)
                    {
                        return new RespuestaDTO
                        {
                            Disponible = false,
                            EnvioMensaje = false
                        };
                    }
                    else
                    {
                        return new RespuestaDTO
                        {
                            Disponible = true,
                            EnvioMensaje = true
                        };
                    }
                }

                indice++;
            }

            return new RespuestaDTO
            {
                Disponible = true,
                EnvioMensaje = false
            };
        }

        /// <summary>
        /// Nombre de cada recurso de las citas: primero en Usuarios (marcados como recurso)
        /// y, si no está ahí, en la tabla Recursos (empresas con recurso múltiple).
        /// </summary>
        public Dictionary<int, string> ObtenerNombresRecursos(IEnumerable<int> recursoIds, ModeloContext ContextBD)
        {
            var ids = recursoIds.Distinct().ToList();

            var nombres = ContextBD.Usuarios
                .Where(u => ids.Contains(u.id) && u.recurso)
                .Select(u => new { u.id, u.nombre })
                .ToDictionary(u => u.id, u => u.nombre);

            var faltantes = ids.Where(id => !nombres.ContainsKey(id)).ToList();

            if (faltantes.Any())
            {
                foreach (var r in ContextBD.Recursos
                             .Where(r => faltantes.Contains(r.id))
                             .Select(r => new { r.id, r.nombre }))
                {
                    nombres[r.id] = r.nombre;
                }
            }

            return nombres;
        }

        /// <summary>
        /// Mensaje de fidelidad de las citas recién creadas, con las mismas reglas y formato de
        /// Agendamiento/consultarCitasRecurso. Devuelve cadena vacía si no aplica.
        /// </summary>
        public string ObtenerMensajeFidelidad(int idCliente, IEnumerable<int> citasIds, Empresa empresa, ModeloContext ContextBD)
        {
            if (idCliente <= 0)
                return string.Empty;

            var manejaFidelidad = bool.TryParse(ContextBD.Parametros.FirstOrDefault(p => p.tipo == "ManejaFidelidad")?.valor, out bool resulFidelidad) && resulFidelidad;

            if (!manejaFidelidad)
                return string.Empty;

            var ids = citasIds.ToList();

            // Igual que consultarCitasRecurso: solo citas activas, con la misma regla de valores
            var citas = ContextBD.Citas
                .Where(c => ids.Contains(c.id) && c.estado == Estados.Activo)
                .Select(c => new
                {
                    c.fechaInicio,
                    c.usuarioCreacion,
                    TieneServicioPago = c.CitaServicios.Any(cs => cs.servicioNavigation.esPago == true),
                    Valores = c.CitaServicios
                        .Select(cs =>
                            c.bono == true
                            || c.fidelidad == true
                            || c.descuento == true
                            || cs.servicioNavigation.valor == 0
                            ? (c.valor ?? 0)
                            : (cs.servicioNavigation.valor ?? 0))
                        .ToList()
                })
                .ToList();

            // Empresa 2: igual que al sumar la visita de fidelidad, no cuentan las citas creadas
            // por el usuario ni las que no tienen ningún servicio de pago
            if (empresa.id == 2)
            {
                citas = citas
                    .Where(c => c.usuarioCreacion != "Usuario" && c.TieneServicioPago)
                    .ToList();
            }

            if (!citas.Any())
                return string.Empty;

            var fidelidadCantidad = ContextBD.Fidelidads
                .Where(f => f.cliente == idCliente)
                .Select(f => (int?)f.cantidad)
                .FirstOrDefault();

            if (!fidelidadCantidad.HasValue)
                return string.Empty;

            // La cita actual todavía no está contabilizada
            var visitaActual = fidelidadCantidad.Value + 1;

            var fidelidadParametro = ContextBD.FidelidadParametros
                .FirstOrDefault(p => p.cantidad == visitaActual && p.estado);

            if (fidelidadParametro == null)
                return string.Empty;

            var nombreCliente = ContextBD.Clientes
                .Where(cl => cl.id == idCliente)
                .Select(cl => cl.nombre)
                .FirstOrDefault();

            var valorServicios = citas.Sum(c => c.Valores.Sum());
            var cultura = new CultureInfo("es-CO");

            if (empresa.servicio == true)
            {
                var valorFinalServicio = valorServicios - (valorServicios * fidelidadParametro.porcentaje / 100);

                return $"Es la {visitaActual} visita del cliente " +
                       $"{nombreCliente} y tiene un " +
                       $"{fidelidadParametro.porcentaje:0}% de descuento " +
                       $"para un total de " +
                       $"{valorFinalServicio.ToString("C0", cultura)}";
            }

            if (empresa.recursoMultiple == true)
            {
                var fechaCita = citas.First().fechaInicio.Date;

                // Recursos diferentes del mismo cliente y día
                var cantidadRecursos = ContextBD.Citas
                    .Where(cita =>
                        cita.cliente == idCliente &&
                        cita.fechaInicio.Date == fechaCita &&
                        (cita.estado == Estados.Activo || cita.estado == Estados.Asistio))
                    .Select(cita => cita.recurso)
                    .Distinct()
                    .Count();

                cantidadRecursos = Math.Max(cantidadRecursos, 1);

                // El porcentaje se divide entre los recursos diferentes
                var porcentajeFidelidad = fidelidadParametro.porcentaje / cantidadRecursos;

                var valorFinalMultiple = valorServicios - (valorServicios * porcentajeFidelidad / 100);

                return $"Es la {visitaActual} visita del cliente " +
                       $"{nombreCliente} y tiene un " +
                       $"descuento del {porcentajeFidelidad:0.##}% " +
                       $"para un total de " +
                       $"{valorFinalMultiple.ToString("C0", cultura)}";
            }

            var valorFinal = valorServicios - (valorServicios * fidelidadParametro.porcentaje / 100);

            return $"Es la {visitaActual} visita del cliente " +
                   $"{nombreCliente} y tiene un " +
                   $"descuento del {fidelidadParametro.porcentaje:0}% " +
                   $"para un total de " +
                   $"{valorFinal.ToString("C0", cultura)}";
        }

        public UsuarioDTO GetInfoRecurso(int recurso, ModeloContext ContextBD)
        {
            var infoRecurso = ContextBD.Usuarios
                                .Include(u => u.sedeNavigation)
                                .Where(e => e.estado == Estados.Activo
                                    && e.id == recurso)
                                .FirstOrDefault();

            if (infoRecurso != null)
            {
                return new UsuarioDTO
                {
                    Nombre = infoRecurso.nombre,
                    NombreSede = infoRecurso.sedeNavigation?.Nombre,
                    Correo = infoRecurso.correo
                };
            }
            else
            {
                var Recurso = ContextBD.Recursos
                                .Include(u => u.sedeNavigation)
                                .Where(e => e.estado == Estados.Activo
                                    && e.id == recurso)
                                .FirstOrDefault();
                if (Recurso != null)
                {
                    return new UsuarioDTO
                    {
                        NombreSede = Recurso.sedeNavigation?.Nombre
                    };
                }
            }

            return null;
        }

        public int GetInt(Parametro? parametro)
        {
            if (parametro == null)
                return 0;

            if (int.TryParse(parametro.valor, out var resultado))
                return resultado;

            return 0;
        }

        public decimal GetDecimal(Parametro? parametro)
        {
            if (parametro == null)
                return 0m;

            if (decimal.TryParse(parametro.valor, out var resultado))
                return resultado;

            return 0m;
        }
    }
}
