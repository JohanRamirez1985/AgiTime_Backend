using Compartido;
using Compartido.Contextos;
using Compartido.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Usuarios.API.Data;
using Usuarios.API.Models;

namespace Usuarios.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsuariosController : ControllerBase
    {
        private readonly UsuariosContext ContextUsu;
        private readonly IConfiguration configuracion;

        public UsuariosController(UsuariosContext usu, IConfiguration config)
        {
            ContextUsu = usu;
            configuracion = config;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            string nombreBase = string.Empty;

            try
            {
                var usuario = ContextUsu.Usuarios
                            .Include(u => u.EmpresaUsuarios)
                            .ThenInclude(eu => eu.IdEmpresaNavigation)
                            .FirstOrDefault(u => u.Login == request.Login && u.Password == request.Password);

                if (usuario == null)
                {
                    return Ok(new
                    {
                        mensaje = "Credenciales inválidas",
                        exito = false
                    });
                }

                var empresa = usuario.EmpresaUsuarios.FirstOrDefault();

                if (empresa != null)
                {
                    return Ok(new
                    {
                        mensaje = "El usuario no tiene empresa asignada",
                        exito = false
                    });
                }

                nombreBase = empresa.IdEmpresaNavigation.ConexionBd;

                string claveSecreta = configuracion["Jwt:Key"];
                int expiracion = int.Parse(configuracion["Jwt:ExpiracionMinutos"]);

                var token = Token.GenerarToken(usuario.Login, nombreBase, claveSecreta, expiracion);

                return Ok(new LoginResponse { Token = token });
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
