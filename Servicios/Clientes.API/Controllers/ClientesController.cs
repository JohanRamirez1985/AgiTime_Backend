using Compartido.Data;
using Compartido.Data.Modelos;
using Compartido;
using Compartido.Constantes;
using Compartido.DTOs;
using Compartido.Metodos;
using Microsoft.AspNetCore.Mvc;
using Clientes.API.Servicios;
using Microsoft.EntityFrameworkCore;

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

        //[HttpPost("login")]
        //public IActionResult LoginCliente([FromBody] LoginRequest request)
        //{
        //    string nombreBase = string.Empty;
        //    try
        //    {
        //        var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == request.empresa);
        //        nombreBase = empresa.conexionBD;

        //        if (empresa.conexionBD == null)
        //        {
        //            return Ok(new
        //            {
        //                mensaje = "La empresa no tiene base de datos asignada",
        //                exito = false
        //            });
        //        }

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

        //[HttpPost("registrarCliente")]
        //public IActionResult CrearCliente([FromBody] ClienteDTO dto)
        //{
        //    string nombreBase = string.Empty;
        //    try
        //    {
        //        var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == dto.empresa);
        //        nombreBase = empresa.conexionBD;

        //        if (empresa.conexionBD == null)
        //        {
        //            return Ok(new
        //            {
        //                mensaje = "La empresa no tiene base de datos asignada",
        //                exito = false
        //            });
        //        }

        //        var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
        //        var connectionString = string.Format(connectionTemplate, empresa.conexionBD);

        //        using var ContextBD = ContextFact.Create(connectionString);

        //        var valida = ContextBD.Clientes.Where(e => e.documento == dto.Documento).FirstOrDefault();

        //        if (valida != null)
        //        {
        //            return Ok(new
        //            {
        //                mensaje = "Ya hay un cliente creado con el número de identificación: " + dto.Documento,
        //                exito = false
        //            });
        //        }

        //        var nuevoCliente = new Cliente
        //        {
        //            nombre = dto.Nombre,
        //            correo = dto.Correo,
        //            login = dto.Correo,
        //            password = EncripPassword.EncriptarPassword(dto.Password),
        //            telefono = dto.Telefono,
        //            fechaNacimiento = dto.FechaNacimiento,
        //            genero = dto.Genero,
        //            tipoDocumento = dto.TipoDocumento,
        //            documento = dto.Documento,
        //            estado = Estados.Activo,
        //            fechaCreacion = DateTime.Now
        //        };

        //        ContextBD.Clientes.Add(nuevoCliente);
        //        ContextBD.SaveChanges();

        //        return Ok(new
        //        {
        //            mensaje = "Cliente registrado exitosamente",
        //            exito = true,
        //            clienteId = nuevoCliente.id
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
    }
}
