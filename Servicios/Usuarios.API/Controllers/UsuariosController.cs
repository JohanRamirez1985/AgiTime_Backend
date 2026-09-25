using Compartido;
using Compartido.Constantes;
using Compartido.Data;
using Compartido.Data.Modelos;
using Compartido.DTOs;
using Compartido.Metodos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using System.Drawing;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Usuarios.API.Servicios;

namespace Usuarios.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsuariosController : ControllerBase
    {
        private readonly GeneralContext ContextGene;
        private readonly FactoryContext ContextFact;
        private readonly IConfiguration configuracion;
        private readonly UsuarioService UsuarioService;

        public UsuariosController(GeneralContext gen, FactoryContext fac, IConfiguration config, UsuarioService ususer)
        {
            ContextGene = gen;
            ContextFact = fac;
            configuracion = config;
            UsuarioService = ususer;
        }

        [HttpPost("login")]
        public IActionResult LoginUsuario([FromBody] LoginRequest request)
        {
            string nombreBase = string.Empty;
            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == request.empresa && e.estado == Estados.Activo);
                var apariencia = ContextGene.Apariencia.Where(a => a.estado == true && a.empresa == request.empresa).FirstOrDefault();
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

                var usuario = ContextBD.Usuarios
                            .Include(u => u.generoNavigation)
                            .Include(u => u.tipoDocumentoNavigation)
                            .Include(u => u.sedeNavigation)
                            .Include(u => u.idRols)
                            .FirstOrDefault(u => u.login == request.Login);

                if (usuario == null || !EncripPassword.VerificarPassword(request.Password, usuario.password))
                {
                    return Ok(new
                    {
                        mensaje = "Credenciales inválidas",
                        exito = false
                    });
                }

                if (usuario.estado == Estados.Inactivo)
                {
                    return Ok(new
                    {
                        mensaje = "Usuario Inactivo",
                        exito = false
                    });
                }

                string claveSecreta = configuracion["Jwt:Key"];
                int expiracion = int.Parse(configuracion["Jwt:ExpiracionMinutos"]);

                var token = Token.GenerarToken(usuario.login, empresa.conexionBD, claveSecreta, expiracion);

                var user = UsuarioService.ProcesarUsuario(usuario, empresa, nombreBase);

                return Ok(new LoginResponse
                {
                    Token = token,
                    Datos = user,
                    Apariencia = apariencia,
                    Exito = true
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

        [HttpPost("registrarUsuario")]
        public IActionResult CrearUsuario([FromBody] UsuarioDTO dto)
        {
            string nombreBase = string.Empty;
            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == dto.empresa && e.estado == Estados.Activo);
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

                var valida = ContextBD.Usuarios.Where(e => e.telefono == dto.Telefono).FirstOrDefault();

                if(valida != null)
                {
                    return Ok(new
                    {
                        mensaje = "Ya hay un usuario creado con el número de celular: " + dto.Telefono,
                        exito = false
                    });
                }

                var rol = ContextBD.Rols.FirstOrDefault(r => r.id == dto.rol);
                if (rol == null)
                {
                    return Ok(new
                    {
                        mensaje = "Rol no válido",
                        exito = false
                    });
                }

                var nuevoUsuario = new Usuario
                {
                    nombre = dto.Nombre,
                    correo = dto.Correo,
                    login = dto.Documento,
                    password = EncripPassword.EncriptarPassword(dto.Documento),
                    telefono = dto.Telefono,
                    fechaNacimiento = dto.FechaNacimiento,
                    genero = dto.Genero,
                    tipoDocumento = dto.TipoDocumento,
                    sede = dto.Sede,
                    documento = dto.Documento,
                    estado = Estados.Activo,
                    fechaCreacion = dto.FechaCreacion,
                    recurso = dto.recurso,
                    idRols = new List<Rol> { rol }
                };

                ContextBD.Usuarios.Add(nuevoUsuario);
                ContextBD.SaveChanges();

                return Ok(new
                {
                    mensaje = "Usuario registrado exitosamente",
                    exito = true,
                    clienteId = nuevoUsuario.id
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

        [HttpPost("editarContrasena")]
        public IActionResult editarContrasena([FromBody] UsuarioDTO dto)
        {
            string nombreBase = string.Empty;
            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == dto.empresa && e.estado == Estados.Activo);
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

                var usuario = ContextBD.Usuarios.Where(e => e.id == dto.Id).FirstOrDefault();

                if (usuario == null)
                {
                    return Ok(new
                    {
                        mensaje = "El usuario no existe",
                        exito = false
                    });
                }

                usuario.password = EncripPassword.EncriptarPassword(dto.Documento);

                ContextBD.SaveChanges();

                return Ok(new
                {
                    mensaje = "Contraseña actualizada correctamente.",
                    exito = true
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

        [HttpPost("editarPorcentaje")]
        public IActionResult EditarPorcentaje([FromBody] UsuarioDTO dto)
        {
            string nombreBase = string.Empty;
            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == dto.empresa && e.estado == Estados.Activo);
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

                var usu = ContextBD.Usuarios.Where(s => s.id == dto.Id).FirstOrDefault();

                if (usu == null)
                {
                    return Ok(new
                    {
                        mensaje = "Usuario no encontrado.",
                        exito = false
                    });
                }
                else
                {
                    usu.porcentaje = dto.Porcentaje;
                    ContextBD.SaveChanges();

                    return Ok(new
                    {
                        mensaje = "Porcentaje actualizado exitosamente",
                        exito = true
                    });
                }
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

        [HttpPost("registrarServicio")]
        public IActionResult CrearServicio([FromBody] ServicioDTO dto)
        {
            string nombreBase = string.Empty;
            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == dto.empresa && e.estado == Estados.Activo);
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
                using var transaction = ContextBD.Database.BeginTransaction();

                try
                {
                    var nuevoServicio = new Servicio
                    {
                        nombre = dto.Nombre,
                        valor = dto.Valor,
                        tiempo = dto.Tiempo,
                        orden = dto.Orden,
                        sede = dto.Sede,
                        estado = Estados.Activo,
                        esPago = dto.EsPago,
                        observaciones = dto.Observaciones 
                    };

                    ContextBD.Servicios.Add(nuevoServicio);
                    ContextBD.SaveChanges();

                    transaction.Commit();

                    return Ok(new
                    {
                        mensaje = "Servicio registrado exitosamente",
                        exito = true
                    });

                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    LogsCatch.GuardarError(ex, configuracion, ContextFact, nombreBase);

                    return StatusCode(500, new
                    {
                        mensaje = "Ocurrió un error interno",
                        exito = false
                    });
                }
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

        [HttpPost("editarServicio")]
        public IActionResult EditarServicio([FromBody] ServicioDTO dto)
        {
            string nombreBase = string.Empty;
            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == dto.empresa && e.estado == Estados.Activo);
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

                var serv = ContextBD.Servicios.Where(s => s.id == dto.Id).FirstOrDefault();

                if (serv == null)
                {
                    return Ok(new
                    {
                        mensaje = "Servicio no encontrado.",
                        exito = false
                    });
                }
                else
                {
                    if(!serv.nombre.Equals(dto.Nombre))
                        serv.nombre = dto.Nombre;
                    if (!serv.valor.Equals(dto.Valor))
                        serv.valor = dto.Valor;
                    if (!serv.tiempo.Equals(dto.Tiempo))
                        serv.tiempo = dto.Tiempo;
                    if (!serv.orden.Equals(dto.Orden))
                        serv.orden = dto.Orden;
                    if (!serv.sede.Equals(dto.Sede))
                        serv.sede = dto.Sede;
                    if (!serv.estado.Equals(dto.Estado))
                        serv.estado = dto.Estado;
                    if (!serv.esPago.Equals(dto.EsPago))
                        serv.esPago = dto.EsPago;
                    if (!serv.observaciones.Equals(dto.Observaciones))
                        serv.observaciones = dto.Observaciones;

                    ContextBD.SaveChanges();

                    return Ok(new
                    {
                        mensaje = "Servicio editado exitosamente",
                        exito = true
                    });
                }
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

        [HttpGet("getServicios")]
        public IActionResult ObtenerServicios([FromQuery] int emp)
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

                var lista = ContextBD.Servicios
                    .Include(s => s.sedeNavigation)
                    .OrderBy(s => s.orden)
                    .Select(s => new ServicioDTO
                    {
                        Id = s.id,
                        Nombre = s.nombre,
                        Valor = s.valor,
                        Tiempo = s.tiempo,
                        Orden = s.orden,
                        Sede = s.sede,
                        Estado = s.estado, 
                        EsPago = s.esPago,
                        Observaciones = s.observaciones,
                        NombreSede = s.sedeNavigation != null ? s.sedeNavigation.Nombre : null 
                    })
                    .ToList();

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
