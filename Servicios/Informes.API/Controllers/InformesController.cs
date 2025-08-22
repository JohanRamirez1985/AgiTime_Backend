using Compartido;
using Compartido.Constantes;
using Compartido.Data;
using Compartido.Data.Modelos;
using Compartido.DTOs;
using Compartido.Metodos;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace Informes.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InformesController : ControllerBase
    {
        private readonly GeneralContext ContextGene;
        private readonly FactoryContext ContextFact;
        private readonly IConfiguration configuracion;
        /*private readonly AgendamientoService agendaService;*/

        public InformesController(GeneralContext gen, FactoryContext fac, IConfiguration config/*, IEmailService email, AgendamientoService ageSer*/)
        {
            ContextGene = gen;
            ContextFact = fac;
            configuracion = config;
            //agendaService = ageSer;
        }

        [HttpGet("informacionDashboard")]
        public IActionResult InformacionDashboard([FromQuery] DateTime fecha, int emp)
        {
            string nombreBase = string.Empty;
            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == emp && e.estado == Estados.Activo);
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

                var info = (from c in ContextBD.Citas
                            where c.fechaInicio >= fecha && c.fechaInicio < fecha.AddDays(1)
                            join r in ContextBD.Recursos on c.recurso equals r.id into recursoJoin
                            from r in recursoJoin.DefaultIfEmpty()
                            join u in ContextBD.Usuarios.Where(x => x.recurso == true) on c.recurso equals u.id into usuarioJoin
                            from u in usuarioJoin.DefaultIfEmpty()
                            let nombreRecurso = r != null ? r.nombre : u.nombre
                            group c by nombreRecurso into g
                            select new
                            {
                                NombreRecurso = g.Key,
                                TotalCitasConfirmadas = g.Count(x => x.estado == Estados.Asistio),
                                TotalCitasNoConfirmadas = g.Count(x => x.estado == Estados.Activo)
                            }).ToList();

                return Ok(new
                {
                    exito = true,
                    datos = info
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
