using Compartido;
using Compartido.Constantes;
using Compartido.Data;
using Compartido.Data.Modelos;
using Compartido.DTOs;
using Compartido.Metodos;
using Informes.API.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using System.Globalization;
using System.Threading.Tasks;

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
        public async Task<IActionResult> InformacionDashboard([FromQuery] DateTime fechaIni, DateTime fechaFin, int emp)
        {
            string nombreBase = string.Empty;
            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == emp && e.estado == Estados.Activo);

                if (empresa == null)
                {
                    return Ok(new
                    {
                        mensaje = "Empresa no encontrada o inactiva",
                        exito = false
                    });
                }

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

                var infoCitas = await (from c in ContextBD.Citas
                                       where c.fechaInicio >= fechaIni && c.fechaInicio <= fechaFin
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
                                       }).ToArrayAsync();

                var totalIngresos = ContextBD.Citas
                                    .Where(c => c.fechaInicio >= fechaIni
                                             && c.fechaInicio <= fechaFin
                                             && (c.estado == Estados.Asistio || c.estado == Estados.Activo))
                                    .Sum(c => (decimal?)c.valor) ?? 0;

                var porcentajeCitas = ObtenerPorcentajeOcupacion(fechaIni,fechaFin,ContextBD);

                var porcentajeServicios = await (
                                        from c in ContextBD.Citas
                                        where c.fechaInicio >= fechaIni && c.fechaInicio <= fechaFin
                                        join cs in ContextBD.CitaServicios on c.id equals cs.cita
                                        join s in ContextBD.Servicios on cs.servicio equals s.id
                                        join se in ContextBD.Sedes on s.sede equals se.id
                                        group new { s, se } by new
                                        {
                                            s.id,
                                            s.nombre,
                                            idSede = se.id,
                                            nombreSede = se.Nombre
                                        } into g
                                        select new
                                        {
                                            idServicio = g.Key.id,
                                            servicio = g.Key.nombre,
                                            idSede = g.Key.idSede,
                                            sede = g.Key.nombreSede,
                                            cantidad = g.Count()
                                        }
                                        )
                                        .OrderByDescending(x => x.cantidad)
                                        .ToListAsync();

                var valorDiario = await ObtenerValorDiario(ContextBD);

                return Ok(new
                {
                    exito = true,
                    datos = new
                    {
                        infoCitas,
                        totalIngresos,
                        porcentajeCitas,
                        porcentajeServicios,
                        valorDiario
                    }
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
            decimal bono = 0;

            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == emp && e.estado == Estados.Activo);

                if (empresa == null)
                {
                    return Ok(new
                    {
                        mensaje = "Empresa no encontrada o inactiva",
                        exito = false
                    });
                }

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
                                    .Where(u =>
                                        u.recurso == true &&
                                        u.idRols.Any(r => r.id == 2 || r.id == 3))
                                    .OrderBy(u => u.sede)
                                    .ToList();
                }

                var parametros = ContextBD.Parametros.Where(p => p.estado).ToList();

                bono = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Valor_Bono"));

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
                                    valor = s.nombre == "Otro" ? c.valor : s.valor - (c.bono.GetValueOrDefault() ? bono : 0),
                                    bono = c.bono
                                })
                               .ToList();

                    var pagas = info.Where(i => i.pagoCita == true && i.pagoServicio == true).ToList();
                    var obsequios = info.Where(i => i.pagoCita == true && i.pagoServicio == false).ToList();
                    var noPagas = info.Where(i => i.pagoCita == false).ToList();

                    var resultado = new
                    {
                        nombre = u.nombre,
                        totalServicios = pagas.Count,
                        servicios = pagas.Select(s => new { s.servicio, s.valor,s.bono }).ToList(),
                        totalPlata = pagas.Sum(s => s.valor),
                        obsequios = obsequios.Select(s => new { s.servicio, s.valor }).ToList(),
                        totalPlataObsequios = obsequios.Sum(s => s.valor),
                        descanso = noPagas.Select(s => new { s.servicio, s.valor }).ToList(),
                        totalPlataDescanso = noPagas.Sum(s => s.valor)
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
        public async Task<IActionResult> InformacionSemanal([FromQuery] DateTime fechaInicio, DateTime fechaFin, int emp, int? usuario = null, string? barberos = null, bool personal = false)
        {
            string nombreBase = string.Empty;
            string nombreEmpresa = string.Empty;
            string logo = string.Empty;
            //decimal? porcentaje = 0;
            //decimal? porcentajeBarber = 0;
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
            //decimal inasistencia = 0;
            //decimal inasistencia_trailer = 0;
            decimal valAntiguo = 0;
            decimal valGeneral = 0;
            bool esAntiguo = false;
            bool esBarberoGeneral = false;
            decimal bono = 0;
            decimal descuentoBono = 0;

            var resumenUsuarios = new List<object>();

            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == emp && e.estado == Estados.Activo);

                if (empresa == null)
                {
                    return Ok(new
                    {
                        mensaje = "Empresa no encontrada o inactiva",
                        exito = false
                    });
                }

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
                        .Include(u => u.sedeNavigation)
                        .Where(u => u.id == usuario)
                    .ToList();
                }
                else
                {
                    usuarios = ContextBD.Usuarios
                        .Include(u => u.sedeNavigation)
                        .Where(u =>
                        u.recurso == true &&
                        u.idRols.Any(r => r.id == 2 || r.id == 3))
                        .OrderBy(u => u.sede)
                    .ToList();
                }

                var parametros = ContextBD.Parametros.Where(p => p.estado).ToList();

                bono = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Valor_Bono"));
                descuentoBono = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Descuento_Bono"));

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
                                    valor = s.nombre == "Otro" ? c.valor : s.valor - (c.bono.GetValueOrDefault() ? bono : 0),
                                    bono = c.bono
                                })
                               .ToList();

                    var pagas = info.Where(i => i.pagoCita == true && i.pagoServicio == true).ToList();
                    var obsequios = info.Where(i => i.pagoCita == true && i.pagoServicio == false).ToList();
                    var noPagas = info.Where(i => i.pagoCita == false).ToList();
                    var bonos = info.Where(i => i.bono == true).ToList();

                    can1 = GetInt(parametros.FirstOrDefault(p => p.tipo == "Total_Cortes_1"));
                    can2 = GetInt(parametros.FirstOrDefault(p => p.tipo == "Total_Cortes_2"));
                    can3 = GetInt(parametros.FirstOrDefault(p => p.tipo == "Total_Cortes_3"));
                    can4 = GetInt(parametros.FirstOrDefault(p => p.tipo == "Total_Cortes_4"));

                    var antiguo = ContextBD.Parametros.Where(p => p.tipo == "Antiguo" && p.estado == true).FirstOrDefault();
                    esAntiguo = antiguo.valor.Split(',').Any(x => x.Trim() == u.id.ToString());

                    valAntiguo = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Valor_Antiguo"));

                    var BarberoGeneral = ContextBD.Parametros.Where(p => p.tipo == "Barbero_General" && p.estado == true).FirstOrDefault();
                    esBarberoGeneral = BarberoGeneral.valor.Split(',').Any(x => x.Trim() == u.id.ToString());

                    valGeneral = u.porcentaje ?? 0m;

                    switch (u.sede)
                    {
                        case 1:
                            val1 = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Valor_Corte_1"));
                            val2 = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Valor_Corte_2"));
                            val3 = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Valor_Corte_3"));
                            val4 = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Valor_Corte_4"));
                            break;

                        case 2:
                        case 3:
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
                    //inasistencia = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Inasistencia"));
                    //inasistencia_trailer = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Inasistencia_Trailer"));

                    if (validaBarberos)
                    {
                        valorDescuentoPorCorte = u.incumplimiento ?? 0m;
                        //switch (u.sede)
                        //{
                        //    case 1:
                        //        valorDescuentoPorCorte = inasistencia;
                        //        break;

                        //    case 2:
                        //    case 3:
                        //        valorDescuentoPorCorte = inasistencia_trailer;
                        //        break;

                        //    default:
                        //        break;
                        //}
                    }
                    else 
                    {
                        if (esAntiguo)
                        {
                            valorDescuentoPorCorte = valAntiguo;
                        }
                        else
                        {
                            if (esBarberoGeneral) 
                            {
                                valorDescuentoPorCorte = valGeneral;
                            }
                            else
                            {
                                switch (u.sede)
                                {
                                    case 1:
                                        if (total <= can1)
                                            valorDescuentoPorCorte = val1;
                                        else if (total >= can2 && total < can3)
                                            valorDescuentoPorCorte = val2;
                                        else if (total >= can3)
                                            valorDescuentoPorCorte = val3;
                                        break;

                                    case 2:
                                    case 3:
                                        if (total <= can1)
                                            valorDescuentoPorCorte = val1;
                                        else if (total >= can2 && total < can3)
                                            valorDescuentoPorCorte = val2;
                                        else if (total >= can3)
                                            valorDescuentoPorCorte = val3;
                                        break;

                                    default:
                                        break;
                                }
                            }
                        }
                    }

                    decimal? totalPlata = pagas.Sum(s => s.valor);
                    decimal totalDescuento = total * valorDescuentoPorCorte - (bonos.Count * descuentoBono);
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
                        sede = u.sedeNavigation.Nombre,
                        totalBonos = bonos.Count
                    };

                    resumenUsuarios.Add(resultado);
                }

                if (usuario != null && !personal)
                {
                    var pdfBytes = await informeService.GenerarInformeSemanalBarber(resumenUsuarios, fechaInicio.ToString("dd/MM/yyyy"), fechaFin.ToString("dd/MM/yyyy"), nombreEmpresa, emp, logo);
                    return File(pdfBytes, "application/pdf", $"Resumen_Barbero_{DateTime.Now:yyyyMMdd}.pdf");
                }
                else
                {
                    var pdfBytes = await informeService.GenerarInformeSemanal(resumenUsuarios, fechaInicio.ToString("dd/MM/yyyy"), fechaFin.ToString("dd/MM/yyyy"), nombreEmpresa, emp, logo);
                    return File(pdfBytes, "application/pdf", $"Resumen_{DateTime.Now:yyyyMMdd}.pdf");
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

        [HttpPost("liquidacionSemanal")]
        public IActionResult LiquidacionSemanal([FromBody] LiquidacionDTO dto)
        {
            string nombreBase = string.Empty;
            string nombreEmpresa = string.Empty;
            string logo = string.Empty;
            decimal? valorDescuentoPorCorte = 0;
            decimal bono = 0;
            decimal descuentoBono = 0;
            List<Usuario> usuarios;
            List<int> listaBarberos = new();
            bool hayDatos = false;
            var resumenUsuarios = new List<object>();

            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == dto.empresa && e.estado == Estados.Activo);
                
                if (empresa == null)
                {
                    return Ok(new
                    {
                        mensaje = "Empresa no encontrada o inactiva",
                        exito = false
                    });
                }

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

                if (!string.IsNullOrWhiteSpace(dto.barberos))
                {
                    listaBarberos = dto.barberos
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => int.Parse(x))
                        .ToList();
                }

                usuarios = ContextBD.Usuarios
                    .Include(u => u.sedeNavigation)
                    .Where(u =>
                    u.recurso == true &&
                    u.idRols.Any(r => r.id == 2 || r.id == 3))
                    .OrderBy(u => u.sede)
                .ToList();

                var parametros = ContextBD.Parametros.Where(p => p.estado).ToList();

                bono = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Valor_Bono"));
                descuentoBono = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Descuento_Bono"));

                foreach (Usuario u in usuarios)
                {
                    var info = (from c in ContextBD.Citas
                                join sc in ContextBD.CitaServicios on c.id equals sc.cita
                                join s in ContextBD.Servicios on sc.servicio equals s.id
                                where c.fechaInicio >= dto.fechaIni && c.fechaInicio <= dto.fechaFin
                                && c.recurso == u.id
                                && c.estado == Estados.Asistio
                                && c.liquidado != true
                                select new InfoCitaDTO
                                {
                                    idCita = c.id,
                                    fechaIni = c.fechaInicio,
                                    servicio = s.nombre,
                                    pagoCita = c.pago ?? false,
                                    pagoServicio = s.esPago ?? false,
                                    valor = s.nombre == "Otro" ? (c.valor ?? 0) : (s.valor ?? 0) - (c.bono.GetValueOrDefault() ? bono : 0),
                                    bono = c.bono ?? false
                                })
                               .ToList();

                    if (!info.Any())
                        continue;

                    hayDatos = true;

                    var citasIds = info.Select(i => i.idCita).ToList();

                    var citas = ContextBD.Citas
                        .Where(c => citasIds.Contains(c.id))
                        .ToList();

                    foreach (var cita in citas)
                    {
                        cita.liquidado = true;
                    }

                    valorDescuentoPorCorte = GetValorDescuento(info, parametros, listaBarberos, u);

                    foreach (var item in info)
                    {
                        string tipo;
                        decimal valorBarberia;
                        decimal valorBarbero;

                        if (item.pagoCita == true && item.pagoServicio == true)
                        {
                            tipo = "Normal";
                            valorBarberia = (valorDescuentoPorCorte ?? 0) - (item.bono ? descuentoBono : 0);
                            valorBarbero = item.valor - valorBarberia;
                        }
                        else
                        {
                            valorBarberia = 0;
                            valorBarbero = item.valor;

                            tipo = item.pagoCita == true ? "Obsequio" : "Descanso";
                        }

                        var liquidacion = new LiquidacionCitum
                        {
                            cita = item.idCita,
                            tipo = tipo,
                            valorServicio = item.valor,
                            valorBarberia = valorBarberia,
                            valorBarbero = valorBarbero,
                            bono = item.bono,
                            fechaCita = item.fechaIni,
                            numero = item.servicio.Equals("Corte VIP") ? 2 : 1,
                            sede = u.sedeNavigation.Nombre,
                            fechaCreacion = dto.fechaCreacion,
                            usuarioCreacion = dto.usuarioCreacion
                        };

                        ContextBD.LiquidacionCita.Add(liquidacion);
                    }
                }

                if (hayDatos)
                {
                    ContextBD.SaveChanges();

                    return Ok(new
                    {
                        mensaje = "Liquidación registrada exitosamente",
                        exito = true
                    });
                }
                else
                {
                    return Ok(new
                    {
                        mensaje = "No hay usuarios para liquidar en el rango de fechas seleccionado",
                        exito = true
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

        [HttpGet("informacionLiquidacion")]
        public async Task<IActionResult> InformacionLiquidacion([FromQuery] DateTime fechaInicio, DateTime fechaFin, int emp, int? usuario = null, bool personal = false)
        {
            string nombreBase = string.Empty;
            string nombreEmpresa = string.Empty;
            string logo = string.Empty;
            int total = 0;
            List<Usuario> usuarios;
            var resumenUsuarios = new List<object>();

            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == emp && e.estado == Estados.Activo);

                if (empresa == null)
                {
                    return Ok(new
                    {
                        mensaje = "Empresa no encontrada o inactiva",
                        exito = false
                    });
                }

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
                        .Include(u => u.sedeNavigation)
                        .Where(u => u.id == usuario)
                    .ToList();
                }
                else
                {
                    usuarios = ContextBD.Usuarios
                        .Include(u => u.sedeNavigation)
                        .Where(u =>
                        u.recurso == true &&
                        u.idRols.Any(r => r.id == 2 || r.id == 3))
                        .OrderBy(u => u.sede)
                    .ToList();
                }

                var idsUsuarios = usuarios.Select(u => u.id).ToList();
                
                var totalesPorSede = (from c in ContextBD.Citas
                                   join l in ContextBD.LiquidacionCita on c.id equals l.cita
                                   where l.fechaCita >= fechaInicio && l.fechaCita <= fechaFin
                                   && idsUsuarios.Contains(c.recurso)
                                   && l.tipo == "Normal"
                                   group l by l.sede into g
                                   select new TotalSedeDTO
                                   {
                                       Sede = g.Key,
                                       TotalSer = g.Sum(x => x.numero),
                                       TotalBar = g.Sum(x => (decimal)x.valorBarberia),
                                       TotalBon = g.Count(x => x.bono == true)
                                   }).ToList();

                foreach (Usuario u in usuarios)
                {
                    var info = (from c in ContextBD.Citas
                                join l in ContextBD.LiquidacionCita on c.id equals l.cita
                                where l.fechaCita >= fechaInicio && l.fechaCita <= fechaFin
                                && c.recurso == u.id
                                select new
                                {
                                    tipo = l.tipo,
                                    valorServicio = l.valorServicio,
                                    valorBarberia = l.valorBarberia,
                                    valorBarbero = l.valorBarbero,
                                    bono = l.bono,
                                    numero = l.numero,
                                    sede = l.sede
                                }).ToList();

                    var pagas = info.Where(i => i.tipo == "Normal").ToList();
                    var obsequios = info.Where(i => i.tipo == "Obsequio").ToList();
                    var noPagas = info.Where(i => i.tipo == "Descanso").ToList();
                    var bonos = info.Where(i => i.bono == true).ToList();

                    total = info.Where(i => i.tipo == "Normal").Sum(i => i.numero ?? 0);

                    decimal? totalPlata = pagas.Sum(s => s.valorServicio);
                    decimal? totalDescuento = pagas.Sum(s => s.valorBarberia);
                    decimal? totalEmpleado = pagas.Sum(s => s.valorBarbero);

                    var resultado = new
                    {
                        nombre = u.nombre,
                        totalServicios = total,
                        totalPlata = totalPlata,
                        totalEmpleado = totalEmpleado,
                        totalBarber = totalDescuento,
                        totalObsequios = obsequios.Count,
                        totalPlataObsequios = obsequios.Sum(s => s.valorBarbero),
                        totaldescanso = noPagas.Count,
                        totalPlataDescanso = noPagas.Sum(s => s.valorBarbero),
                        totalIngresoBarber = totalEmpleado + obsequios.Sum(s => s.valorBarbero) + noPagas.Sum(s => s.valorBarbero),
                        sede = u.sedeNavigation.Nombre,
                        totalBonos = bonos.Count
                    };

                    resumenUsuarios.Add(resultado);
                }

                if (usuario != null && !personal)
                {
                    var pdfBytes = await informeService.GenerarInformeSemanalBarber(resumenUsuarios, fechaInicio.ToString("dd/MM/yyyy"), fechaFin.ToString("dd/MM/yyyy"), nombreEmpresa, emp, logo);
                    return File(pdfBytes, "application/pdf", $"Resumen_Barbero_{DateTime.Now:yyyyMMdd}.pdf");
                }
                else
                {
                    var pdfBytes = await informeService.GenerarInformeSemanal(resumenUsuarios, fechaInicio.ToString("dd/MM/yyyy"), fechaFin.ToString("dd/MM/yyyy"), nombreEmpresa, emp, logo, totalesPorSede);
                    return File(pdfBytes, "application/pdf", $"Resumen_{DateTime.Now:yyyyMMdd}.pdf");
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
        
        private decimal GetValorDescuento(List<InfoCitaDTO> info,List<Parametro> parametros,List<int> listaBarberos, Usuario u)
        {
            int total = 0;
            int can1 = 0;
            int can2 = 0;
            int can3 = 0;
            int can4 = 0;
            decimal val1 = 0;
            decimal val2 = 0;
            decimal val3 = 0;
            decimal val4 = 0;
            //decimal inasistencia = 0;
            //decimal inasistencia_trailer = 0;
            decimal valAntiguo = 0;
            decimal valGeneral = 0;
            bool esAntiguo = false;
            bool esBarberoGeneral = false;
            decimal valorDescuentoPorCorte = 0;       

            var pagas = info.Where(i => i.pagoCita == true && i.pagoServicio == true).ToList();

            can1 = GetInt(parametros.FirstOrDefault(p => p.tipo == "Total_Cortes_1"));
            can2 = GetInt(parametros.FirstOrDefault(p => p.tipo == "Total_Cortes_2"));
            can3 = GetInt(parametros.FirstOrDefault(p => p.tipo == "Total_Cortes_3"));
            can4 = GetInt(parametros.FirstOrDefault(p => p.tipo == "Total_Cortes_4"));

            var antiguo = parametros.FirstOrDefault(p => p.tipo == "Antiguo" && p.estado);
            esAntiguo = antiguo?.valor?
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Any(x => x.Trim() == u.id.ToString()) ?? false;

            valAntiguo = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Valor_Antiguo"));

            var barberoGeneral = parametros.FirstOrDefault(p => p.tipo == "Barbero_General" && p.estado);
            esBarberoGeneral = barberoGeneral?.valor?
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Any(x => x.Trim() == u.id.ToString()) ?? false;

            valGeneral = u.porcentaje ?? 0m;

            switch (u.sede)
            {
                case 1:
                    val1 = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Valor_Corte_1"));
                    val2 = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Valor_Corte_2"));
                    val3 = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Valor_Corte_3"));
                    val4 = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Valor_Corte_4"));
                    break;

                case 2:
                case 3:
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

            bool validaBarberos = listaBarberos.Any() && listaBarberos.Contains(u.id);
            //inasistencia = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Inasistencia"));
            //inasistencia_trailer = GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Inasistencia_Trailer"));

            if (validaBarberos)
            {
                valorDescuentoPorCorte = u.incumplimiento ?? 0m;
                //switch (u.sede)
                //{
                //    case 1:
                //        valorDescuentoPorCorte = inasistencia;
                //        break;

                //    case 2:
                //    case 3:
                //        valorDescuentoPorCorte = inasistencia_trailer;
                //        break;

                //    default:
                //        break;
                //}
            }
            else
            {
                if (esAntiguo)
                {
                    valorDescuentoPorCorte = valAntiguo;
                }
                else
                {
                    if (esBarberoGeneral)
                    {
                        valorDescuentoPorCorte = valGeneral;
                    }
                    else
                    {
                        switch (u.sede)
                        {
                            case 1:
                                if (total <= can1)
                                    valorDescuentoPorCorte = val1;
                                else if (total >= can2 && total < can3)
                                    valorDescuentoPorCorte = val2;
                                else if (total >= can3)
                                    valorDescuentoPorCorte = val3;
                                break;

                            case 2:
                            case 3:
                                if (total <= can1)
                                    valorDescuentoPorCorte = val1;
                                else if (total >= can2 && total < can3)
                                    valorDescuentoPorCorte = val2;
                                else if (total >= can3)
                                    valorDescuentoPorCorte = val3;
                                break;

                            default:
                                break;
                        }
                    }
                }
            }

            return valorDescuentoPorCorte;
        }

        private decimal ObtenerPorcentajeOcupacion(DateTime fechaInicial, DateTime fechaFinal, ModeloContext ContextBD)
        {
            /*var horariosAgenda = ContextBD.Agenda
                .Where(a => a.estado)
                .OrderBy(a => a.valor)
                .Select(a => a.valor)
                .ToList();

            int totalDias = (fechaFinal.Date - fechaInicial.Date).Days + 1;

            int totalDisponibles = horariosAgenda.Count * totalDias;

            var citas = ContextBD.Citas
                .Where(c =>
                    c.fechaInicio >= fechaInicial &&
                    c.fechaFin <= fechaFinal &&
                    c.estado != Estados.Cancelada)
                .ToList();

            List<string> espaciosOcupados = new List<string>();

            foreach (var cita in citas)
            {
                DateTime inicio = cita.fechaInicio;
                DateTime fin = cita.fechaFin.Value;

                while (inicio < fin)
                {
                    espaciosOcupados.Add(
                        inicio.ToString("yyyy-MM-dd HH:mm")
                    );

                    inicio = inicio.AddMinutes(30);
                }
            }

            int totalOcupados = espaciosOcupados
                .Distinct()
                .Count();

            decimal porcentaje = totalDisponibles == 0
                ? 0
                : ((decimal)totalOcupados / totalDisponibles) * 100;

            return Math.Round(porcentaje, 2);*/

            int rango = Convert.ToInt32(ContextBD.Parametros.Where(p => p.tipo == "Tiempo_Agenda").Select(p => p.valor).FirstOrDefault());

            var horariosAgenda = ContextBD.Agenda
        .Where(a => a.estado)
        .OrderBy(a => a.valor)
        .Select(a => a.valor)
        .ToList();

            List<DateTime> espaciosDisponibles = new List<DateTime>();

            DateTime fecha = fechaInicial.Date;

            while (fecha <= fechaFinal.Date)
            {
                foreach (var hora in horariosAgenda)
                {
                    DateTime fechaHora = DateTime.Parse(
                        $"{fecha:yyyy-MM-dd} {hora}"
                    );

                    espaciosDisponibles.Add(fechaHora);
                }

                fecha = fecha.AddDays(1);
            }

            var bloqueos = ContextBD.BloqueosHorarios
                .Where(b => b.estado == "Activo")
                .ToList();

            List<DateTime> espaciosBloqueados = new List<DateTime>();

            fecha = fechaInicial.Date;

            while (fecha <= fechaFinal.Date)
            {
                int diaSemana = (int)fecha.DayOfWeek;

                diaSemana = diaSemana == 0 ? 7 : diaSemana;

                foreach (var bloqueo in bloqueos)
                {
                    if (string.IsNullOrWhiteSpace(bloqueo.diasSemana))
                        continue;

                    List<int> dias = bloqueo.diasSemana
                        .Split(',')
                        .Select(x => int.Parse(x.Trim()))
                        .ToList();

                    if (!dias.Contains(diaSemana))
                        continue;

                    DateTime horaInicio = fecha.Date + bloqueo.horaInicio;
                    DateTime horaFin = fecha.Date + bloqueo.horaFin;

                    while (horaInicio < horaFin)
                    {
                        espaciosBloqueados.Add(horaInicio);

                        horaInicio = horaInicio.AddMinutes(rango);
                    }
                }

                fecha = fecha.AddDays(1);
            }

            int totalDisponibles = espaciosDisponibles
                .Except(espaciosBloqueados)
                .Distinct()
                .Count();

            var citas = ContextBD.Citas
                .Where(c =>
                    c.fechaInicio >= fechaInicial &&
                    c.fechaFin <= fechaFinal &&
                    c.estado != Estados.Cancelada)
                .ToList();

            List<DateTime> espaciosOcupados = new List<DateTime>();

            foreach (var cita in citas)
            {
                DateTime inicio = cita.fechaInicio;
                DateTime fin = cita.fechaFin.Value;

                while (inicio < fin)
                {
                    espaciosOcupados.Add(inicio);

                    inicio = inicio.AddMinutes(rango);
                }
            }

            int totalOcupados = espaciosOcupados
                .Distinct()
                .Count();

            decimal porcentaje = totalDisponibles == 0
                ? 0
                : ((decimal)totalOcupados / totalDisponibles) * 100;

            return Math.Round(porcentaje, 2);
        }

        public async Task<object> ObtenerValorDiario(ModeloContext ContextBD)
        {
            var zonaColombia = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

            var ahoraColombia = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zonaColombia);

            var hoy = ahoraColombia.Date;

            int diasDesdeLunes = ((int)hoy.DayOfWeek == 0 ? 6 : (int)hoy.DayOfWeek - 1);

            var lunesActual = hoy.AddDays(-diasDesdeLunes);

            var domingoActual = lunesActual.AddDays(7);

            var lunesPasado = lunesActual.AddDays(-7);

            var ingresos = await ContextBD.Citas
                .Where(c =>
                    c.fechaInicio >= lunesPasado &&
                    c.fechaInicio < domingoActual &&
                    c.estado == Estados.Asistio)
                .GroupBy(c => c.fechaInicio.Date)
                .Select(g => new
                {
                    fecha = g.Key,
                    total = g.Sum(x => x.valor)
                })
                .ToListAsync();

            var semanaActual = Enumerable.Range(0, 7)
                .Select(i =>
                {
                    var fecha = lunesActual.AddDays(i);

                    return new CitaDTO
                    {
                        dia = fecha.ToString("dddd", new CultureInfo("es-CO")),
                        totalDia = ingresos
                            .FirstOrDefault(x => x.fecha == fecha)?.total ?? 0
                    };
                }).ToList();

            var semanaPasada = Enumerable.Range(0, 7)
                .Select(i =>
                {
                    var fecha = lunesPasado.AddDays(i);

                    return new CitaDTO
                    {
                        dia = fecha.ToString("dddd", new CultureInfo("es-CO")),
                        totalDia = ingresos
                            .FirstOrDefault(x => x.fecha == fecha)?.total ?? 0
                    };
                }).ToList();

            return new
            {
                semanaActual,
                semanaPasada
            };
        }
    }
}
