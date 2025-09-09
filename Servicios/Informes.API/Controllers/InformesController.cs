using Compartido;
using Compartido.Constantes;
using Compartido.Data;
using Compartido.Data.Modelos;
using Compartido.DTOs;
using Compartido.Metodos;
using Informes.API.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
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
        private readonly InformeService informeService;

        public InformesController(GeneralContext gen, FactoryContext fac, IConfiguration config/*, IEmailService email*/, InformeService infSer)
        {
            ContextGene = gen;
            ContextFact = fac;
            configuracion = config;
            informeService = infSer;
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

        [HttpGet("informacionDiaria")]
        public async Task<IActionResult> InformacionDiaria([FromQuery] DateTime fecha, int emp)
        {
            string nombreBase = string.Empty;
            string nombreEmpresa = string.Empty;
            string logo = string.Empty;

            var resumenUsuarios = new List<object>();

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
                nombreEmpresa = empresa.nombre;
                logo = "https://agitime.com.co/assets/img/" + empresa.logo;

                var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
                var connectionString = string.Format(connectionTemplate, empresa.conexionBD);

                using var ContextBD = ContextFact.Create(connectionString);

                var usuarios = ContextBD.Usuarios
                                .Include(u => u.idRols)
                                .Where(u => u.id == 3)
                                .ToList();

                foreach(Usuario u in usuarios)
                {
                    var info = (from c in ContextBD.Citas
                               join sc in ContextBD.CitaServicios on c.id equals sc.cita
                               join s in ContextBD.Servicios on sc.servicio equals s.id
                               where c.fechaInicio >= fecha && c.fechaInicio < fecha.AddDays(1)
                               && c.recurso == u.id
                               && c.estado == Estados.Asistio
                               select new {
                                   idCita = c.id,
                                   fechaIni = c.fechaInicio,
                                   servicio = s.nombre,
                                   pagoCita = c.pago,
                                   pagoServicio = s.esPago,
                                   valor = s.valor
                               })
                               .ToList();

                    var pagas = info.Where(i => i.pagoCita == true && i.pagoServicio == true).ToList();
                    var obsequios = info.Where(i => i.pagoServicio == false).ToList();
                    var noPagas = info.Where(i => i.pagoCita == false).ToList();

                    var resultado = new
                    {
                        nombre = u.nombre,
                        totalServicios = pagas.Count,
                        servicios = pagas.Select(s => new { s.servicio, s.valor }).ToList(),
                        totalPlata = pagas.Sum(s => s.valor),
                        obsequios = obsequios.Select(s => new { s.servicio, s.valor }).ToList(),
                        descanso = noPagas.Select(s => new { s.servicio, s.valor }).ToList()
                    };

                    resumenUsuarios.Add(resultado);
                }

                var pdfBytes = await informeService.GenerarInformeDiario(resumenUsuarios, nombreEmpresa,logo);

                return File(pdfBytes, "application/pdf", $"Resumen_{fecha:yyyyMMdd}.pdf");

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
