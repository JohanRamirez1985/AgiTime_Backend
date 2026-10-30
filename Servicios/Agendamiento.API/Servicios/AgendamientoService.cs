using Compartido.Constantes;
using Compartido.Data;
using Compartido.Data.Modelos;
using Compartido.DTOs;
using Compartido.Metodos;
using Microsoft.EntityFrameworkCore;

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
