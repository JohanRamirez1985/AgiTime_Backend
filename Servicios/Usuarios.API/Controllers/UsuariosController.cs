using Compartido;
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

namespace Usuarios.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsuariosController : ControllerBase
    {
        private readonly GeneralContext ContextGene;
        private readonly FactoryContext ContextFact;
        private readonly IConfiguration configuracion;

        public UsuariosController(GeneralContext gen, FactoryContext fac, IConfiguration config)
        {
            ContextGene = gen;
            ContextFact = fac;
            configuracion = config;
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

                var usuario = ContextBD.Usuarios.FirstOrDefault(u => u.login == request.Login && u.password == request.Password);

                if (usuario == null)
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

                return Ok(new LoginResponse 
                { 
                    Token = token,
                    Datos = ArregloLogin.ConstruirData(usuario, usuario,"Usuario")
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
