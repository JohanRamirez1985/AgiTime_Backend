using Compartido.Data.Modelos;

namespace Agendamiento.API.Servicios
{
    public class AgendamientoService
    {
        public string mensajeEmailCliente (string nombre, DateTime fecha)
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
                </style>
            </head>
            <body>
                <div class='container'>
                    <h1>Hola {nombre}</h1>
                    <p>Tu cita ha sido agendada para <strong>{fecha:dd/MM/yyyy HH:mm}</strong>.</p>
                    <p>Gracias por utilizar nuestros servicios.</p>
                    <div class='footer'>
                        Este es un mensaje automático, por favor no responda a este correo.
                    </div>
                </div>
            </body>
            </html>";

            return mensaje;
        }

        public string mensajeEmailUsuario(string nombreCliente, string nombreUsuario, DateTime fecha)
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
                </style>
            </head>
            <body>
                <div class='container'>
                    <h2>Notificación de nueva cita</h2>
                    <p>Hola <strong>{nombreUsuario}</strong>,</p>
                    <p>Se ha agendado una nueva cita con el siguiente detalle:</p>

                    <div class='highlight'>
                        <p><strong>Cliente:</strong> {nombreCliente}</p>
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
    }
}
