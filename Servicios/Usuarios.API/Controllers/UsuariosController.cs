using Compartido;
using Compartido.Constantes;
using Compartido.DTOs;
using Compartido.Metodos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Usuarios.API.Data;
using Usuarios.API.Data.Modelos;
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
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == request.empresa);
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

                string claveSecreta = configuracion["Jwt:Key"];
                int expiracion = int.Parse(configuracion["Jwt:ExpiracionMinutos"]);

                var token = Token.GenerarToken(usuario.login, empresa.conexionBD, claveSecreta, expiracion);

                var user = UsuarioService.ProcesarUsuario(usuario, empresa, nombreBase);

                //var usuarioDto = UsuarioService.ProcesarUsuario(usuario);

                //var funcionalidad = UsuarioService.ProcesarFuncionabilidad(usuario, nombreBase);

                return Ok(new LoginResponse
                {
                    Token = token,
                    Datos = user, //ArregloLogin.ConstruirData(info, "Usuario"),
                    Exito = true
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

        [HttpPost("registrarUsuario")]
        public IActionResult CrearUsuario([FromBody] UsuarioDTO dto)
        {
            string nombreBase = string.Empty;
            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == dto.empresa);
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

                var rol = ContextBD.Rols.FirstOrDefault(r => r.id == dto.rol);
                if (rol == null)
                {
                    return BadRequest(new { mensaje = "Rol no válido" });
                }

                var nuevoUsuario = new Usuario
                {
                    nombre = dto.Nombre,
                    correo = dto.Correo,
                    login = dto.Correo,
                    password = EncripPassword.EncriptarPassword(dto.Password),
                    telefono = dto.Telefono,
                    fechaNacimiento = dto.FechaNacimiento,
                    genero = dto.Genero,
                    tipoDocumento = dto.TipoDocumento,
                    sede = dto.Sede,
                    documento = dto.Documento,
                    estado = Estados.Activo,
                    fechaCreacion = DateTime.Now,
                    idRols = new List<Rol> { rol }
                };

                ContextBD.Usuarios.Add(nuevoUsuario);
                ContextBD.SaveChanges();

                return Ok(new
                {
                    mensaje = "Cliente registrado exitosamente",
                    exito = true,
                    clienteId = nuevoUsuario.id
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
