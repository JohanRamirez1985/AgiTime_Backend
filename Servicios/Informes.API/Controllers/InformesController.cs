using Compartido;
using Compartido.Constantes;
using Compartido.Data;
using Compartido.Data.Modelos;
using Compartido.DTOs;
using Compartido.Metodos;
using Informes.API.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
        public async Task<IActionResult> InformacionDiaria([FromQuery] DateTime fecha, int emp, int? usuario = null)
        {
            string nombreBase = string.Empty;
            string nombreEmpresa = string.Empty;
            string logo = string.Empty;
            List<Usuario> usuarios;
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

                if (usuario != null)
                {
                    usuarios = ContextBD.Usuarios
                                .Where(u => u.id == usuario)
                                .ToList();
                }
                else
                {
                    usuarios = ContextBD.Usuarios
                                    .Where(u => u.idRols.Any(r => r.id == 3))
                                    .ToList();
                }

                foreach (Usuario u in usuarios)
                {
                    var info = (from c in ContextBD.Citas
                                join sc in ContextBD.CitaServicios on c.id equals sc.cita
                                join s in ContextBD.Servicios on sc.servicio equals s.id
                                where c.fechaInicio >= fecha && c.fechaInicio < fecha.AddDays(1)
                                && c.recurso == u.id
                                && c.estado == Estados.Asistio
                                select new
                                {
                                    idCita = c.id,
                                    fechaIni = c.fechaInicio,
                                    servicio = s.nombre,
                                    pagoCita = c.pago,
                                    pagoServicio = s.esPago,
                                    valor = s.nombre == "Otro" ? c.valor : s.valor
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

                    if (pagas.Count > 0 || obsequios.Count > 0 || noPagas.Count > 0)
                        resumenUsuarios.Add(resultado);
                }

                if (resumenUsuarios.Count > 0)
                {
                    var pdfBytes = await informeService.GenerarInformeDiario(resumenUsuarios, fecha.ToString("dd/MM/yyyy"), nombreEmpresa, logo);

                    return File(pdfBytes, "application/pdf", $"Resumen_Diario_{DateTime.Now:yyyyMMdd}.pdf");
                }
                else
                {
                    return Ok(new
                    {
                        mensaje = "No se encontraron registros para el día solicitado.",
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

        [HttpGet("informacionSemanal")]
        public async Task<IActionResult> InformacionSemanal([FromQuery] DateTime fechaInicio, DateTime fechaFin, int emp, int? usuario = null, string? barberos = null)
        {
            string nombreBase = string.Empty;
            string nombreEmpresa = string.Empty;
            string logo = string.Empty;
            decimal? porcentaje = 0;
            decimal? porcentajeBarber = 0;
            int total = 0;
            decimal valorDescuentoPorCorte = 0;
            List<Usuario> usuarios;
            List<int> listaBarberos = new();

            int can1 = 0;
            int can2 = 0;
            int can3 = 0;
            int can4 = 0;
            decimal val1 = 0;
            decimal val2 = 0;
            decimal val3 = 0;
            decimal val4 = 0;
            decimal inasistencia = 0;
            bool esAntiguo = false;

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

                if (!string.IsNullOrWhiteSpace(barberos))
                {
                    listaBarberos = barberos
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => int.Parse(x))
                        .ToList();
                }

                if (usuario != null)
                {
                    usuarios = ContextBD.Usuarios
                                .Where(u => u.id == usuario)
                                .ToList();
                }
                else
                {
                    usuarios = ContextBD.Usuarios
                                    .Where(u => u.idRols.Any(r => r.id == 3))
                                    .ToList();
                }

                foreach (Usuario u in usuarios)
                {
                    var info = (from c in ContextBD.Citas
                                join sc in ContextBD.CitaServicios on c.id equals sc.cita
                                join s in ContextBD.Servicios on sc.servicio equals s.id
                                where c.fechaInicio >= fechaInicio && c.fechaInicio <= fechaFin
                                && c.recurso == u.id
                                && c.estado == Estados.Asistio
                                select new
                                {
                                    idCita = c.id,
                                    fechaIni = c.fechaInicio,
                                    servicio = s.nombre,
                                    pagoCita = c.pago,
                                    pagoServicio = s.esPago,
                                    valor = s.nombre == "Otro" ? c.valor : s.valor
                                })
                               .ToList();

                    var pagas = info.Where(i => i.pagoCita == true && i.pagoServicio == true).ToList();
                    var obsequios = info.Where(i => i.pagoServicio == false).ToList();
                    var noPagas = info.Where(i => i.pagoCita == false).ToList();

                    var parametros = ContextBD.Parametros.Where(p => p.estado).ToList();

                    can1 = GetInt(parametros.FirstOrDefault(p => p.tipo == "Total_Cortes_1"));
                    can2 = GetInt(parametros.FirstOrDefault(p => p.tipo == "Total_Cortes_2"));
                    can3 = GetInt(parametros.FirstOrDefault(p => p.tipo == "Total_Cortes_3"));
                    can4 = GetInt(parametros.FirstOrDefault(p => p.tipo == "Total_Cortes_4"));

                    var antiguo = ContextBD.Parametros.Where(p => p.tipo == "Antiguo" && p.estado == true).FirstOrDefault();
                    esAntiguo = antiguo.valor.Split(',').Any(x => x.Trim() == u.id.ToString());

                    switch (u.sede)
                    {
                        case 1:
                            val1 = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Valor_Corte_1"));
                            val2 = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Valor_Corte_2"));
                            val3 = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Valor_Corte_3"));
                            val4 = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Valor_Corte_4"));
                            break;

                        case 2:
                            val1 = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Valor_Corte_Trailer_1"));
                            val2 = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Valor_Corte_Trailer_2"));
                            val3 = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Valor_Corte_Trailer_3"));
                            val4 = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Valor_Corte_Trailer_4"));
                            break;

                        default:
                            break;
                    }

                    total = pagas.Count;

                    foreach (var item in pagas)
                    {
                        if (item.servicio.Equals("Corte VIP"))
                        {
                            total++;
                        }
                    }

                    //porcentaje = total <= int.Parse(can1.valor)
                    //            ? u.porcentaje
                    //            : (total > int.Parse(can1.valor) && total <= int.Parse(can2.valor))
                    //                ? decimal.Parse(par1.valor)
                    //                : (total > int.Parse(can2.valor))
                    //                    ? decimal.Parse(par2.valor)
                    //                    : 0;
                    //porcentajeBarber = 100 - porcentaje;

                    bool validaBarberos = listaBarberos.Any() && listaBarberos.Contains(u.id);
                    inasistencia = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Inasistencia"));

                    if (validaBarberos)
                    {
                        valorDescuentoPorCorte = inasistencia;
                    }
                    else 
                    {
                        if (esAntiguo)
                        {
                            valorDescuentoPorCorte = val4;
                        }
                        else
                        {
                            if (total <= can1)
                                valorDescuentoPorCorte = val1;
                            else if (total >= can2 && total < can3)
                                valorDescuentoPorCorte = val2;
                            else if (total >= can3 && total < can4)
                                valorDescuentoPorCorte = val3;
                            else if (total >= can4)
                                valorDescuentoPorCorte = val4;
                        }
                    }

                    decimal? totalPlata = pagas.Sum(s => s.valor);
                    decimal totalDescuento = total * valorDescuentoPorCorte;
                    decimal? totalEmpleado = totalPlata - totalDescuento;

                    var resultado = new
                    {
                        nombre = u.nombre,
                        totalServicios = total,
                        //porcentaje = porcentaje,
                        totalPlata = totalPlata,
                        //totalEmpleado = pagas.Sum(s => s.valor) * porcentaje / 100,
                        //totalBarber = pagas.Sum(s => s.valor) * porcentajeBarber / 100,
                        totalEmpleado = totalEmpleado,
                        totalBarber = totalDescuento,
                        obsequios = obsequios.Select(s => new { s.servicio, s.valor }).ToList(),
                        totalObsequios = obsequios.Count,
                        totalPlataObsequios = obsequios.Sum(s => s.valor),
                        descanso = noPagas.Select(s => new { s.servicio, s.valor }).ToList(),
                        totaldescanso = noPagas.Count,
                        totalPlataDescanso = noPagas.Sum(s => s.valor),
                        totalIngresoBarber = totalEmpleado + obsequios.Sum(s => s.valor) + noPagas.Sum(s => s.valor),
                    };

                    resumenUsuarios.Add(resultado);
                }

                if (usuario != null)
                {
                    var pdfBytes = await informeService.GenerarInformeSemanalBarber(resumenUsuarios, fechaInicio.ToString("dd/MM/yyyy"), fechaFin.ToString("dd/MM/yyyy"), nombreEmpresa, logo);
                    return File(pdfBytes, "application/pdf", $"Resumen_Semanal_Barbero_{DateTime.Now:yyyyMMdd}.pdf");
                }
                else
                {
                    var pdfBytes = await informeService.GenerarInformeSemanal(resumenUsuarios, fechaInicio.ToString("dd/MM/yyyy"), fechaFin.ToString("dd/MM/yyyy"), nombreEmpresa, logo);
                    return File(pdfBytes, "application/pdf", $"Resumen_Semanal_{DateTime.Now:yyyyMMdd}.pdf");
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

        private decimal GetDecimal(Parametro? parametro)
        {
            if (parametro == null)
                return 0m;

            if (decimal.TryParse(parametro.valor, out var resultado))
                return resultado;

            return 0m;
        }

        private int GetInt(Parametro? parametro)
        {
            if (parametro == null)
                return 0;

            if (int.TryParse(parametro.valor, out var resultado))
                return resultado;

            return 0;
        }
    }
}
