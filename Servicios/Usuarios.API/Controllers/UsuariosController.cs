using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
            var usuario = ContextUsu.Usuarios.FirstOrDefault(u => u.Login == request.Email && u.Password == request.Password);

            if (usuario == null)
            {
                return Ok(new
                {
                    mensaje = "Credenciales inválidas",
                    exito = false
                });
            }
            else
            {
                var token = GenerarToken(usuario);

                return Ok(new
                {
                    mensaje = "Login exitoso",
                    token,
                    exito = true
                });
            }
        }

        [HttpGet("protegido")]
        [Authorize]
        public IActionResult Protegido() => Ok("Acceso autorizado");

        private string GenerarToken(Usuario usuario)
        {
            var key = Encoding.ASCII.GetBytes(configuracion["JwtSettings:SecretKey"]);
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, usuario.Nombre),
                new Claim(ClaimTypes.Email, usuario.Email)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                //Subject = new ClaimsIdentity(claims),
                Expires = System.DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }

    public class LoginRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }
}
