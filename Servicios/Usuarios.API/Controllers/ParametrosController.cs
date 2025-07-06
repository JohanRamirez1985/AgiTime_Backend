using Compartido;
using Microsoft.AspNetCore.Mvc;
using Usuarios.API.Data;

namespace Usuarios.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ParametrosController : ControllerBase
    {
        private readonly ParametrosContext Context;
        private readonly IConfiguration configuracion;

        public ParametrosController(ParametrosContext con, IConfiguration config)
        {
            Context = con;
            configuracion = config;
        }

        [HttpGet("tipodocumento")]
        public IActionResult ObtenerTiposDocumento()
        {
            try
            {
                var lista = Context.TipoDocumentos.ToList();

                if (lista == null || lista.Count == 0)
                {
                    return Ok(new
                    {
                        mensaje = "No hay tipos de documento registrados",
                        exito = false
                    });
                }

                return Ok(new
                {
                    exito = true,
                    datos = lista
                });
            }
            catch (Exception ex)
            {
                LogsCatch.GuardarError(ex, configuracion, "");

                return StatusCode(500, new
                {
                    mensaje = "Ocurrió un error interno",
                    exito = false
                });
            }
        }

        [HttpGet("ciudades")]
        public IActionResult ObtenerCiudades()
        {
            try
            {
                var lista = Context.Ciudad.ToList();

                if (lista == null || lista.Count == 0)
                {
                    return Ok(new
                    {
                        mensaje = "No hay ciudades registradas",
                        exito = false
                    });
                }

                return Ok(new
                {
                    exito = true,
                    datos = lista
                });
            }
            catch (Exception ex)
            {
                LogsCatch.GuardarError(ex, configuracion, "");

                return StatusCode(500, new
                {
                    mensaje = "Ocurrió un error interno",
                    exito = false
                });
            }
        }

    }
}
