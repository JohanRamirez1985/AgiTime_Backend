using Clientes.API.Servicios;
using Compartido;
using Compartido.Constantes;
using Compartido.Data;
using Compartido.Data.Modelos;
using Compartido.DTOs;
using Compartido.Metodos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Clientes.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClientesController : ControllerBase
    {
        private readonly GeneralContext ContextGene;
        private readonly FactoryContext ContextFact;
        private readonly IConfiguration configuracion;
        private readonly ClientesService ClienteService;

        public ClientesController(GeneralContext gen, FactoryContext fac, IConfiguration config, ClientesService cliser)
        {
            ContextGene = gen;
            ContextFact = fac;
            configuracion = config;
            ClienteService = cliser;
        }

        [HttpGet("clientes")]
        public IActionResult ObtenerClientes([FromQuery] int emp)
        {
            string nombreBase = string.Empty;
            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == emp && e.estado == Estados.Activo);
                var resultado = ValidacionEmpresa.ValidarEmpresa(empresa);

                if (!resultado.Exito)
                {
                    return Ok(new
                    {
                        mensaje = resultado.Mensaje,
                        exito = false
                    });
                }

                nombreBase = empresa.conexionBD;

                var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
                var connectionString = string.Format(connectionTemplate, empresa.conexionBD);

                using var ContextBD = ContextFact.Create(connectionString);

                var lista = ContextBD.Clientes.OrderBy(o => o.id).ToList();

                if (lista == null || lista.Count == 0)
                {
                    return Ok(new
                    {
                        mensaje = "No hay clientes registrados",
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
                LogsCatch.GuardarError(ex, configuracion, ContextFact, nombreBase);

                return StatusCode(500, new
                {
                    mensaje = "Ocurrió un error interno",
                    exito = false
                });
            }
        }

        //[HttpPost("login")]
        //public IActionResult LoginCliente([FromBody] LoginRequest request)
        //{
        //    string nombreBase = string.Empty;
        //    try
        //    {
        //        var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == request.empresa && e.estado == Estados.Activo);
        //        var resultado = ValidacionEmpresa.ValidarEmpresa(empresa);

        //if (!resultado.Exito)
        //{
        //    return Ok(new
        //    {
        //        mensaje = resultado.Mensaje,
        //        exito = false
        //    });
        //}

        //nombreBase = empresa.conexionBD;

        //        var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
        //        var connectionString = string.Format(connectionTemplate, empresa.conexionBD);

        //        using var ContextBD = ContextFact.Create(connectionString);

        //        var usuario = ContextBD.Clientes
        //                    .Include(u => u.generoNavigation)
        //                    .Include(u => u.tipoDocumentoNavigation)
        //                    .FirstOrDefault(u => u.login == request.Login);

        //        if (usuario == null || !EncripPassword.VerificarPassword(request.Password, usuario.password))
        //        {
        //            return Ok(new
        //            {
        //                mensaje = "Credenciales inválidas",
        //                exito = false
        //            });
        //        }

        //        string claveSecreta = configuracion["Jwt:Key"];
        //        int expiracion = int.Parse(configuracion["Jwt:ExpiracionMinutos"]);

        //        var token = Token.GenerarToken(usuario.login, empresa.conexionBD, claveSecreta, expiracion);

        //        var user = ClienteService.ProcesarUsuario(usuario, empresa, nombreBase);

        //        //var funcionalidad = ClienteService.ProcesarFuncionabilidad(usuario, nombreBase);

        //        return Ok(new LoginResponse
        //        {
        //            Token = token,
        //            Datos = user, //ArregloLogin.ConstruirData(info, "Usuario"),
        //            Exito = true
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        LogsCatch.GuardarError(ex, configuracion, ContextFact, nombreBase);

        //        return StatusCode(500, new
        //        {
        //            mensaje = "Ocurrió un error interno",
        //            exito = false
        //        });
        //    }
        //}

        [HttpPost("registrarCliente")]
        public IActionResult CrearCliente([FromBody] ClienteDTO dto)
        {
            string nombreBase = string.Empty;
            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == dto.empresa && e.estado == Estados.Activo);

                if (empresa == null)
                {
                    return Ok(new
                    {
                        mensaje = "Empresa no encontrada o inactiva",
                        exito = false
                    });
                }

                var resultadoValida = ValidacionEmpresa.ValidarEmpresa(empresa);

                if (!resultadoValida.Exito)
                {
                    return Ok(new
                    {
                        mensaje = resultadoValida.Mensaje,
                        exito = false
                    });
                }

                nombreBase = empresa.conexionBD;

                var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
                var connectionString = string.Format(connectionTemplate, empresa.conexionBD);

                using var ContextBD = ContextFact.Create(connectionString);

                var valida = ContextBD.Clientes.Where(e => e.telefono == dto.Telefono).FirstOrDefault();

                if (valida != null)
                {
                    return Ok(new
                    {
                        mensaje = "Ya hay un cliente creado con el número de celular: " + dto.Telefono,
                        exito = false
                    });
                }

                var nuevoCliente = new Cliente
                {
                    nombre = dto.Nombre,
                    correo = string.IsNullOrWhiteSpace(dto.Correo) ? "na@na.com" : dto.Correo.Trim(),
                    telefono = dto.Telefono,
                    fechaNacimiento = dto.FechaNacimiento,
                    genero = dto.Genero > 0 ? dto.Genero : (int?)null,
                    tipoDocumento = dto.TipoDocumento > 0 ? dto.TipoDocumento : (int?)null,
                    documento = string.IsNullOrWhiteSpace(dto.Documento) ? null : dto.Documento,
                    estado = Estados.Activo,
                    fechaCreacion = dto.FechaCreacion
                };

                ContextBD.Clientes.Add(nuevoCliente);
                ContextBD.SaveChanges();

                return Ok(new
                {
                    mensaje = "Cliente registrado exitosamente",
                    exito = true,
                    clienteId = nuevoCliente.id
                });
            }
            catch (Exception ex)
            {
                LogsCatch.GuardarError(ex, configuracion, ContextFact, nombreBase);

                return StatusCode(500, new
                {
                    mensaje = "Ocurrió un error interno",
                    exito = false
                });
            }
        }

        [HttpGet("clientesRecordatorio")]
        public IActionResult ObtenerRecordatorio([FromQuery] int emp, int dias)
        {
            string nombreBase = string.Empty;
            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == emp && e.estado == Estados.Activo);
                var resultado = ValidacionEmpresa.ValidarEmpresa(empresa);

                if (!resultado.Exito)
                {
                    return Ok(new
                    {
                        mensaje = resultado.Mensaje,
                        exito = false
                    });
                }

                nombreBase = empresa.conexionBD;

                var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
                var connectionString = string.Format(connectionTemplate, empresa.conexionBD);

                using var ContextBD = ContextFact.Create(connectionString);

                var parametros = ContextBD.Parametros.Where(p => p.estado).ToList();

                var fechaHoy = DateTime.Today;
                var fechaObjetivo = fechaHoy.AddDays(-dias);

                var lista = ContextBD.Clientes
                    .Select(c => new
                    {
                        c.id,
                        c.nombre,
                        c.telefono,
                        c.correo,

                        UltimaFechaAsistio = ContextBD.Citas
                            .Where(ci => ci.cliente == c.id &&
                                   ci.estado == Estados.Asistio)
                            .Max(ci => (DateTime?)ci.fechaInicio)
                    })
                    .Where(c => c.UltimaFechaAsistio.HasValue &&
                                c.UltimaFechaAsistio.Value >= fechaObjetivo &&
                                c.UltimaFechaAsistio.Value < fechaObjetivo.AddDays(1))
                    .OrderBy(c => c.nombre)
                    .ToList();

                if (lista == null || lista.Count == 0)
                {
                    return Ok(new
                    {
                        mensaje = "No hay clientes registrados",
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
                LogsCatch.GuardarError(ex, configuracion, ContextFact, nombreBase);

                return StatusCode(500, new
                {
                    mensaje = "Ocurrió un error interno",
                    exito = false
                });
            }
        }
    }
}
