using Clientes.API.Data;
using Clientes.API.Data.Modelos;
using Compartido;
using Compartido.Constantes;
using Compartido.DTOs;
using Compartido.Metodos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Clientes.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClientesControllers : ControllerBase
    {
        private readonly GeneralContext ContextGene;
        private readonly FactoryContext ContextFact;
        private readonly IConfiguration configuracion;

        public ClientesControllers(GeneralContext gen, FactoryContext fac, IConfiguration config)
        {
            ContextGene = gen;
            ContextFact = fac;
            configuracion = config;
        }

        [HttpPost("login")]
        public IActionResult LoginCliente([FromBody] LoginRequest request)
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

                var usuario = ContextBD.Clientes.FirstOrDefault(u => u.login == request.Login);

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

                return Ok(new LoginResponse
                {
                    Token = token,
                    Datos = ArregloLogin.ConstruirData(usuario, usuario, "Usuario"),
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

        [HttpPost("registrarCliente")]
        public IActionResult CrearCliente([FromBody] ClienteDTO dto)
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

                var nuevoCliente = new Cliente
                {
                    nombre = dto.Nombre,
                    correo = dto.Correo,
                    login = dto.Correo,
                    password = EncripPassword.EncriptarPassword(dto.Password),
                    telefono = dto.Telefono,
                    fechaNacimiento = dto.FechaNacimiento,
                    genero = dto.Genero,
                    tipoDocumento = dto.TipoDocumento,
                    documento = dto.Documento,
                    estado = Estados.Activo,
                    fechaCreacion = DateTime.Now
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
