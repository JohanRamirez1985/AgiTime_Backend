using Compartido;
using Compartido.Constantes;
using Compartido.Metodos;
using Microsoft.AspNetCore.Mvc;
using Parametros.API.Data;

namespace Parametros.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ParametrosController : ControllerBase
    {
        private readonly GeneralContext ContextGene;
        private readonly FactoryContext ContextFact;
        private readonly IConfiguration configuracion;

        public ParametrosController(GeneralContext gen, FactoryContext fac, IConfiguration config)
        {
            ContextGene = gen;
            ContextFact = fac;
            configuracion = config;
        }

        [HttpGet("tipodocumento")]
        public IActionResult ObtenerTiposDocumento([FromQuery] int emp)
        {
            string nombreBase = string.Empty;
            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == emp);
                nombreBase = empresa.conexionBD;

                if (empresa.conexionBD == null)
                {
                    return Ok(new
                    {
                        mensaje = "La empresa no tiene base de datos asignada",
                        exito = false
                    });
                }

                var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
                var connectionString = string.Format(connectionTemplate, empresa.conexionBD);

                using var ContextBD = ContextFact.Create(connectionString);

                var lista = ContextBD.TipoDocumentos.ToList();

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
                LogsCatch.GuardarError(ex, configuracion, nombreBase);

                return StatusCode(500, new
                {
                    mensaje = "Ocurrió un error interno",
                    exito = false
                });
            }
        }

        [HttpGet("ciudades")]
        public IActionResult ObtenerCiudades([FromQuery] int emp)
        {
            string nombreBase = string.Empty;
            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == emp);
                nombreBase = empresa.conexionBD;

                if (empresa.conexionBD == null)
                {
                    return Ok(new
                    {
                        mensaje = "La empresa no tiene base de datos asignada",
                        exito = false
                    });
                }

                var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
                var connectionString = string.Format(connectionTemplate, empresa.conexionBD);

                using var ContextBD = ContextFact.Create(connectionString);

                var lista = ContextBD.Ciudads.ToList();

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
                LogsCatch.GuardarError(ex, configuracion, nombreBase);

                return StatusCode(500, new
                {
                    mensaje = "Ocurrió un error interno",
                    exito = false
                });
            }
        }

        [HttpGet("genero")]
        public IActionResult ObtenerGeneros([FromQuery] int emp)
        {
            string nombreBase = string.Empty;
            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == emp);
                nombreBase = empresa.conexionBD;

                if (empresa.conexionBD == null)
                {
                    return Ok(new
                    {
                        mensaje = "La empresa no tiene base de datos asignada",
                        exito = false
                    });
                }

                var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
                var connectionString = string.Format(connectionTemplate, empresa.conexionBD);

                using var ContextBD = ContextFact.Create(connectionString);

                var lista = ContextBD.Generos.ToList();

                if (lista == null || lista.Count == 0)
                {
                    return Ok(new
                    {
                        mensaje = "No hay generos registrados",
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
                LogsCatch.GuardarError(ex, configuracion, nombreBase);

                return StatusCode(500, new
                {
                    mensaje = "Ocurrió un error interno",
                    exito = false
                });
            }
        }

        [HttpGet("rol")]
        public IActionResult ObtenerRoles([FromQuery] int emp)
        {
            string nombreBase = string.Empty;
            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == emp);
                nombreBase = empresa.conexionBD;

                if (empresa.conexionBD == null)
                {
                    return Ok(new
                    {
                        mensaje = "La empresa no tiene base de datos asignada",
                        exito = false
                    });
                }

                var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
                var connectionString = string.Format(connectionTemplate, empresa.conexionBD);

                using var ContextBD = ContextFact.Create(connectionString);

                var lista = ContextBD.Rols.ToList();

                if (lista == null || lista.Count == 0)
                {
                    return Ok(new
                    {
                        mensaje = "No hay roles registrados",
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
                LogsCatch.GuardarError(ex, configuracion, nombreBase);

                return StatusCode(500, new
                {
                    mensaje = "Ocurrió un error interno",
                    exito = false
                });
            }
        }

        [HttpGet("sede")]
        public IActionResult ObtenerSedes([FromQuery] int emp)
        {
            string nombreBase = string.Empty;
            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == emp);
                nombreBase = empresa.conexionBD;

                if (empresa.conexionBD == null)
                {
                    return Ok(new
                    {
                        mensaje = "La empresa no tiene base de datos asignada",
                        exito = false
                    });
                }

                var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
                var connectionString = string.Format(connectionTemplate, empresa.conexionBD);

                using var ContextBD = ContextFact.Create(connectionString);

                var lista = ContextBD.Sedes.Where(e => e.estado == Estados.Activo).ToList();

                if (lista == null || lista.Count == 0)
                {
                    return Ok(new
                    {
                        mensaje = "No hay sedes registradas",
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
                LogsCatch.GuardarError(ex, configuracion, nombreBase);

                return StatusCode(500, new
                {
                    mensaje = "Ocurrió un error interno",
                    exito = false
                });
            }
        }

        [HttpGet("usuarios")]
        public IActionResult ObtenerUsuarios([FromQuery] int emp)
        {
            string nombreBase = string.Empty;
            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == emp);
                nombreBase = empresa.conexionBD;

                if (empresa.conexionBD == null)
                {
                    return Ok(new
                    {
                        mensaje = "La empresa no tiene base de datos asignada",
                        exito = false
                    });
                }

                var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
                var connectionString = string.Format(connectionTemplate, empresa.conexionBD);

                using var ContextBD = ContextFact.Create(connectionString);

                var lista = ContextBD.Usuarios.Where(e => e.estado == Estados.Activo).ToList();

                if (lista == null || lista.Count == 0)
                {
                    return Ok(new
                    {
                        mensaje = "No hay usuarios registrados",
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
                LogsCatch.GuardarError(ex, configuracion, nombreBase);

                return StatusCode(500, new
                {
                    mensaje = "Ocurrió un error interno",
                    exito = false
                });
            }
        }

        [HttpGet("empresas")]
        public IActionResult ObtenerEmpresas()
        {
            string nombreBase = string.Empty;
            try
            {
                var lista = ContextGene.Empresas.Where(e => e.estado == Estados.Activo).ToList();

                if (lista == null || lista.Count == 0)
                {
                    return Ok(new
                    {
                        mensaje = "No hay empresas registradas",
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
                LogsCatch.GuardarError(ex, configuracion, nombreBase);

                return StatusCode(500, new
                {
                    mensaje = "Ocurrió un error interno",
                    exito = false
                });
            }
        }
    }
}
