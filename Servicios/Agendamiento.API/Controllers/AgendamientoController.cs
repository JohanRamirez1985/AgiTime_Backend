using Agendamiento.API.Data;
using Agendamiento.API.Data.Modelos;
using Compartido;
using Compartido.Constantes;
using Compartido.DTOs;
using Compartido.Metodos;
using Microsoft.AspNetCore.Mvc;
//using Agendamiento.API.Servicios;
using Microsoft.EntityFrameworkCore;

namespace Agendamiento.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AgendamientoController : ControllerBase
    {
        private readonly GeneralContext ContextGene;
        private readonly FactoryContext ContextFact;
        private readonly IConfiguration configuracion;
        //private readonly ClientesService ClienteService;

        public AgendamientoController(GeneralContext gen, FactoryContext fac, IConfiguration config/*, ClientesService cliser*/)
        {
            ContextGene = gen;
            ContextFact = fac;
            configuracion = config;
            //ClienteService = cliser;
        }

        [HttpPost("registrarCita")]
        public IActionResult CrearCita([FromBody] ClienteDTO dto)
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
