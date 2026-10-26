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
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ClosedXML.Excel;

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

                var porcentajeCitas = informeService.ObtenerPorcentajeOcupacion(fechaIni,fechaFin,ContextBD);

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

                var valorDiario = await informeService.ObtenerValorDiario(ContextBD);

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
            var resumenInventario = new List<object>();
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

                bono = informeService.GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Valor_Bono"));

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
                                    valor = emp == 2 ? s.nombre == "Otro" || c.bono == true || c.fidelidad == true || s.valor == 0 ? c.valor : s.valor - (c.bono.GetValueOrDefault() ? bono : 0)
                                            : (c.bono == true || c.fidelidad == true || s.valor == 0 ? (c.valor ?? 0) : (s.valor ?? 0)),
                                    bono = c.bono,
                                    metodoPago = c.metodoPago
                                })
                               .ToList();

                    var pagas = info.Where(i => i.pagoCita == true && i.pagoServicio == true).ToList();
                    var obsequios = info.Where(i => i.pagoCita == true && i.pagoServicio == false).ToList();
                    var noPagas = info.Where(i => i.pagoCita == false).ToList();

                    //var comisionVentas = (
                    //        from v in ContextBD.Venta
                    //        join vd in ContextBD.VentaDetalles on v.id equals vd.venta
                    //        where v.usuarioVenta == u.id
                    //           && v.fechaVenta >= fecha
                    //           && v.fechaVenta < fecha.AddDays(1)
                    //        select (decimal?)vd.comision
                    //    ).Sum() ?? 0;

                    var ventas = (from v in ContextBD.Venta
                                    join vd in ContextBD.VentaDetalles on v.id equals vd.venta
                                    join p in ContextBD.Productos on vd.producto equals p.id
                                    where v.usuarioVenta == u.id
                                        && v.fechaVenta >= fecha
                                        && v.fechaVenta < fecha.AddDays(1)
                                    select new
                                    {
                                        producto = p.nombre,
                                        valor = vd.total,       
                                        comision = vd.comision,
                                        cantidad = vd.cantidad
                                    }
                                 ).ToList();

                    var resultado = new
                    {
                        nombre = u.nombre,
                        totalServicios = pagas.Count,
                        servicios = pagas.Select(s => new { s.servicio, s.valor,s.bono, s.metodoPago }).ToList(),
                        totalPlata = pagas.Sum(s => s.valor),
                        obsequios = obsequios.Select(s => new { s.servicio, s.valor }).ToList(),
                        totalPlataObsequios = obsequios.Sum(s => s.valor),
                        descanso = noPagas.Select(s => new { s.servicio, s.valor }).ToList(),
                        totalPlataDescanso = noPagas.Sum(s => s.valor),
                        comisionVentas = ventas
                    };

                    if (pagas.Count > 0 || obsequios.Count > 0 || noPagas.Count > 0 || ventas.Count > 0)
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

                bono = informeService.GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Valor_Bono"));
                descuentoBono = informeService.GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Descuento_Bono"));

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

                    if (validaBarberos)
                        valorDescuentoPorCorte = u.incumplimiento ?? 0m;
                    else
                        valorDescuentoPorCorte = u.porcentaje ?? 0m;

                    decimal? totalPlata = pagas.Sum(s => s.valor);
                    decimal totalDescuento = total * valorDescuentoPorCorte - (bonos.Count * descuentoBono);
                    decimal? totalEmpleado = totalPlata - totalDescuento;

                    var comisionVentas = (
                            from v in ContextBD.Venta
                            join vd in ContextBD.VentaDetalles on v.id equals vd.venta
                            where v.usuarioVenta == u.id
                               && v.fechaVenta >= fechaInicio
                               && v.fechaVenta <= fechaFin
                            select (decimal?)vd.comision
                        ).Sum() ?? 0;

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
                        totalIngresoBarber = totalEmpleado + obsequios.Sum(s => s.valor) + noPagas.Sum(s => s.valor) + comisionVentas,
                        sede = u.sedeNavigation.Nombre,
                        totalBonos = bonos.Count,
                        comisionVentas = comisionVentas
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

                bono = informeService.GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Valor_Bono"));
                descuentoBono = informeService.GetDecimal(parametros.FirstOrDefault(p => p.tipo == "Descuento_Bono"));

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
                                    valor = s.nombre == "Otro" || c.fidelidad == true ? (c.valor ?? 0) : (s.valor ?? 0) - (c.bono.GetValueOrDefault() ? bono : 0),
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

                    valorDescuentoPorCorte = informeService.GetValorDescuento(info, parametros, listaBarberos, u);

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
            var totalServiciosIngresoMes = 0;
            decimal totalPlataIngresoMes = 0;
            decimal totalBarberiaIngresoMes = 0;
            decimal totalBarberoIngresoMes = 0;

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
                                   where l.fechaCita >= fechaInicio 
                                    && l.fechaCita <= fechaFin
                                    && idsUsuarios.Contains(c.recurso)
                                    && l.tipo == "Normal"
                                    && !(emp == 2 && (c.recurso == 3 || c.recurso == 4))
                                   group l by l.sede into g
                                   select new TotalSedeDTO
                                   {
                                       Sede = g.Key,
                                       TotalSer = g.Sum(x => x.numero),
                                       TotalBar = g.Sum(x => (decimal)x.valorBarberia),
                                       TotalBon = g.Count(x => x.bono == true)
                                   }).ToList();

                var totalesPorSedeIngresoMes = ContextBD.IngresoMesPruebas
                                    .Where(i => i.fecha >= fechaInicio
                                             && i.fecha <= fechaFin
                                             && idsUsuarios.Contains(i.recurso)
                                             && !(emp == 2 && (i.recurso == 3 || i.recurso == 4)))
                                    .GroupBy(i => i.sede)
                                    .Select(g => new TotalSedeDTO
                                    {
                                        Sede = g.Key,
                                        TotalSer = g.Sum(x => x.numero),
                                        TotalBar = g.Sum(x => x.valorBarberia ?? 0),
                                        TotalBon = 0
                                    })
                                    .ToList();

                foreach (var ingreso in totalesPorSedeIngresoMes)
                {
                    var sedeExistente = totalesPorSede
                        .FirstOrDefault(x => x.Sede == ingreso.Sede);

                    if (sedeExistente != null)
                    {
                        sedeExistente.TotalSer += ingreso.TotalSer;
                        sedeExistente.TotalBar += ingreso.TotalBar;
                        sedeExistente.TotalBon += ingreso.TotalBon;
                    }
                    else
                    {
                        totalesPorSede.Add(ingreso);
                    }
                }

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

                    if (emp == 2)
                    {
                        var ingresoMes = ContextBD.IngresoMesPruebas
                            .Where(i => i.recurso == u.id
                                     && i.fecha >= fechaInicio
                                     && i.fecha <= fechaFin)
                            .ToList();

                        totalServiciosIngresoMes = ingresoMes.Sum(i => i.numero);
                        totalPlataIngresoMes = ingresoMes.Sum(i => i.valorServicio ?? 0);
                        totalBarberiaIngresoMes = ingresoMes.Sum(i => i.valorBarberia ?? 0);
                        totalBarberoIngresoMes = ingresoMes.Sum(i => i.valorBarbero ?? 0);
                    }

                    total += totalServiciosIngresoMes;
                    totalPlata = (totalPlata ?? 0) + totalPlataIngresoMes;
                    totalDescuento = (totalDescuento ?? 0) + totalBarberiaIngresoMes;
                    totalEmpleado = (totalEmpleado ?? 0) + totalBarberoIngresoMes;

                    var comisionVentas = (
                            from v in ContextBD.Venta
                            join vd in ContextBD.VentaDetalles on v.id equals vd.venta
                            where v.usuarioVenta == u.id
                               && v.fechaVenta >= fechaInicio
                               && v.fechaVenta <= fechaFin
                            select (decimal?)vd.comision
                        ).Sum() ?? 0;

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
                        totalIngresoBarber = totalEmpleado + obsequios.Sum(s => s.valorBarbero) + noPagas.Sum(s => s.valorBarbero) + comisionVentas,
                        sede = u.sedeNavigation.Nombre,
                        totalBonos = bonos.Count,
                        comisionVentas = comisionVentas
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

        [HttpGet("informacionInventarioVenta")]
        public async Task<IActionResult> InformacionInventarioVenta([FromQuery] DateTime fechaInicio, DateTime fechaFin, int emp, int? usuario = null, bool personal = false)
        {
            string nombreBase = string.Empty;
            string nombreEmpresa = string.Empty;
            string logo = string.Empty;

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

                var lista = (
                    from v in ContextBD.Venta
                    join vd in ContextBD.VentaDetalles on v.id equals vd.venta
                    join vl in ContextBD.VentaDetalleLotes on vd.id equals vl.ventaDetalle
                    join pl in ContextBD.ProductoLotes on vl.productoLote equals pl.id
                    join p in ContextBD.Productos on vd.producto equals p.id
                    join u in ContextBD.Usuarios on v.usuarioVenta equals u.id
                    where v.fechaVenta >= fechaInicio
                       && v.fechaVenta <= fechaFin
                    group new { vl, pl } by new
                    {
                        v.fechaVenta,
                        VentaDetalleId = vd.id,
                        Producto = p.nombre,
                        Vendedor = u.nombre,
                        vd.cantidad,
                        vd.valorVenta,
                        vd.total,
                        vd.descuento,
                        vd.comision
                    }
                    into g
                    select new
                    {
                        FechaVenta = g.Key.fechaVenta,
                        Producto = g.Key.Producto,
                        Vendedor = g.Key.Vendedor,

                        Cantidad = g.Key.cantidad,

                        //Venta = g.Key.cantidad * g.Key.valorVenta,
                        // Si prefieres usar el valor final pagado:
                        Venta = g.Key.total,

                        Descuento = g.Key.descuento,
                        Comision = g.Key.comision,

                        Costo = g.Sum(x => x.vl.cantidad * x.pl.precioCosto)
                    }
                )
                .ToList()
                .GroupBy(x => new
                {
                    x.FechaVenta,
                    x.Producto,
                    x.Vendedor
                })
                .Select(g => new
                {
                    FechaVenta = g.Key.FechaVenta,
                    Producto = g.Key.Producto,
                    Vendedor = g.Key.Vendedor,

                    Cantidad = g.Sum(x => x.Cantidad),
                    Venta = g.Sum(x => x.Venta),
                    Costo = g.Sum(x => x.Costo),
                    Descuento = g.Sum(x => x.Descuento),
                    Comision = g.Sum(x => x.Comision),

                    Ganancia = g.Sum(x => x.Venta)
                             - g.Sum(x => x.Costo)
                             - g.Sum(x => x.Comision)
                             //- g.Sum(x => x.Descuento)
                })
                .OrderBy(x => x.FechaVenta)
                .ThenBy(x => x.Producto)
                .ThenBy(x => x.Vendedor)
                .ToList();

                var venta = lista.Cast<dynamic>().ToList();

                var pdfBytes = await informeService.GenerarReporteVentas(venta, fechaInicio.ToString("dd/MM/yyyy"), fechaFin.ToString("dd/MM/yyyy"), nombreEmpresa, emp, logo);
                return File(pdfBytes, "application/pdf", $"Resumen_Ventas_{DateTime.Now:yyyyMMdd}.pdf");

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

        [HttpGet("inventarioProductos")]
        public async Task<IActionResult> ObtenerInventarioProductos([FromQuery] int emp)
        {
            string nombreBase = string.Empty;
            string nombreEmpresa = string.Empty;
            string logo = string.Empty;

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

                var lista = ContextBD.Productos
                    .Select(p => new ProductoDTO
                    {
                        Id = p.id,
                        Nombre = p.nombre,
                        Codigo = p.codigo,
                        PrecioVenta = p.precioVenta,
                        Porcentaje = p.porcentaje ?? 0,

                        Cantidad = ContextBD.ProductoLotes
                            .Where(l => l.producto == p.id)
                            .Sum(l => (int?)l.cantidadDisponible) ?? 0,

                        preciosCosto = ContextBD.ProductoLotes
                            .Where(l => l.producto == p.id &&
                                        l.estado == "Activo" &&
                                        l.cantidadDisponible > 0)
                            .GroupBy(l => l.precioCosto)
                            .Select(g => new InventarioCostoDTO
                            {
                                precioCosto = g.Key,
                                cantidad = g.Sum(x => x.cantidadDisponible)
                            })
                            .OrderBy(x => x.precioCosto)
                            .ToList()
                    })
                    .OrderBy(p => p.Nombre)
                    .ToList();

                var pdfBytes = await informeService.GenerarReporteInventarioProducto(lista, nombreEmpresa, emp, logo);
                return File(pdfBytes, "application/pdf", $"Resumen_Ventas_{DateTime.Now:yyyyMMdd}.pdf");

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

        [HttpPost("ingresoMesPrueba")]
        public IActionResult INgresoMesPrueba([FromBody] IngresoMesPruebasDTO dto)
        {
            string nombreBase = string.Empty;

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

                var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
                var connectionString = string.Format(connectionTemplate, empresa.conexionBD);

                using var ContextBD = ContextFact.Create(connectionString);

                var ingreso = new IngresoMesPrueba
                {
                    valorServicio = dto.valorServicio,
                    valorBarberia = dto.valorBarberia,
                    valorBarbero = dto.valorBarbero,
                    fecha = dto.fecha,
                    numero = dto.numero,
                    sede = dto.sede,
                    recurso = dto.recurso,
                    fechaCreacion = dto.fechaCreacion,
                    usuarioCreacion = dto.usuarioCreacion
                };

                ContextBD.IngresoMesPruebas.Add(ingreso);

                ContextBD.SaveChanges();

                return Ok(new
                {
                    mensaje = "Registrado guardado exitosamente",
                    exito = true
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

        [HttpPost("EjecutarReporte")]
        public async Task<IActionResult> EjecutarReporte([FromBody] ReporteDTO dto)
        {
            string nombreBase = string.Empty;

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

                var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
                var connectionString = string.Format(connectionTemplate, empresa.conexionBD);

                using var ContextBD = ContextFact.Create(connectionString);

                if (dto == null || dto.Id <= 0)
                {
                    return BadRequest("Debe enviar un IdReporte válido.");
                }

                var reporte = await ContextBD.Reportes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r =>
                        r.id == dto.Id && r.activo == true);

                if (reporte == null)
                {
                    return NotFound("El reporte no existe o se encuentra inactivo.");
                }

                if (string.IsNullOrWhiteSpace(reporte.procedimiento))
                {
                    return BadRequest("El reporte no tiene un procedimiento configurado.");
                }

                if (!Regex.IsMatch(reporte.procedimiento, @"^[a-zA-Z0-9_]+$"))
                {
                    return BadRequest("El procedimiento configurado no es válido.");
                }

                var resultados = new List<Dictionary<string, object>>();

                var connection = ContextBD.Database.GetDbConnection();

                await using (connection)
                {
                    if (connection.State != ConnectionState.Open)
                        await connection.OpenAsync();

                    await using var command = connection.CreateCommand();

                    command.CommandText = reporte.procedimiento;
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandTimeout = 300;

                    await using var reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        var fila = new Dictionary<string, object>();

                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var valor = reader.IsDBNull(i)
                                ? null
                                : reader.GetValue(i);

                            fila[reader.GetName(i)] = valor;
                        }

                        resultados.Add(fila);
                    }
                }

                return Ok(new
                {
                    id = reporte.id,
                    nombre = reporte.nombre,
                    descripcion = reporte.descripcion,
                    columnas = resultados.Count > 0
                            ? resultados[0].Keys.ToList()
                            : new List<string>(),
                    datos = resultados
                });
            }
            catch (Exception ex)
            {
                LogsCatch.GuardarError(ex, configuracion, ContextFact, nombreBase);

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        mensaje = "Error al ejecutar el reporte.",
                        detalle = ex.Message
                    });
            }
        }

        [HttpPost("ExportarReporte")]
        public async Task<IActionResult> ExportarReporte([FromBody] ReporteDTO dto)
        {
            string nombreBase = string.Empty;

            try
            {
                // =========================================================
                // VALIDAR EMPRESA
                // =========================================================

                var empresa = await ContextGene.Empresas
                    .FirstOrDefaultAsync(e =>
                        e.id == dto.empresa &&
                        e.estado == Estados.Activo);

                if (empresa == null)
                {
                    return Ok(new
                    {
                        mensaje = "Empresa no encontrada o inactiva",
                        exito = false
                    });
                }

                var resultadoValida =
                    ValidacionEmpresa.ValidarEmpresa(empresa);

                if (!resultadoValida.Exito)
                {
                    return Ok(new
                    {
                        mensaje = resultadoValida.Mensaje,
                        exito = false
                    });
                }

                nombreBase = empresa.conexionBD;

                // =========================================================
                // CREAR CONTEXTO DE LA BASE DE DATOS DEL CLIENTE
                // =========================================================

                var connectionTemplate =
                    configuracion["ConnectionStrings:ConnectionFactory"];

                var connectionString =
                    string.Format(
                        connectionTemplate,
                        empresa.conexionBD);

                using var ContextBD =
                    ContextFact.Create(connectionString);

                // =========================================================
                // BUSCAR REPORTE
                // =========================================================

                var reporte = await ContextBD.Reportes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r =>
                        r.id == dto.Id &&
                        r.activo == true);

                if (reporte == null)
                {
                    return BadRequest(new
                    {
                        mensaje = "Reporte no encontrado o inactivo.",
                        exito = false
                    });
                }

                // =========================================================
                // VALIDAR PROCEDIMIENTO
                // =========================================================

                if (string.IsNullOrWhiteSpace(reporte.procedimiento))
                {
                    return BadRequest(new
                    {
                        mensaje = "El reporte no tiene un procedimiento configurado.",
                        exito = false
                    });
                }

                if (!Regex.IsMatch(
                        reporte.procedimiento,
                        @"^[a-zA-Z0-9_]+$"))
                {
                    return BadRequest(new
                    {
                        mensaje = "El procedimiento configurado no es válido.",
                        exito = false
                    });
                }

                // =========================================================
                // EJECUTAR PROCEDIMIENTO
                // =========================================================

                var resultados =
                    new List<Dictionary<string, object>>();

                var connection =
                    ContextBD.Database.GetDbConnection();

                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }

                await using var command =
                    connection.CreateCommand();

                command.CommandText =
                    reporte.procedimiento;

                command.CommandType =
                    CommandType.StoredProcedure;

                command.CommandTimeout = 300;

                await using var reader =
                    await command.ExecuteReaderAsync();

                // =========================================================
                // LEER RESULTADOS
                // =========================================================

                do
                {
                    if (reader.FieldCount == 0)
                    {
                        continue;
                    }

                    while (await reader.ReadAsync())
                    {
                        var fila =
                            new Dictionary<string, object>();

                        for (int i = 0;
                             i < reader.FieldCount;
                             i++)
                        {
                            var nombreColumna =
                                reader.GetName(i);

                            var valor =
                                reader.GetValue(i);

                            fila[nombreColumna] =
                                valor == DBNull.Value
                                    ? null
                                    : valor;
                        }

                        resultados.Add(fila);
                    }

                } while (await reader.NextResultAsync());

                // =========================================================
                // CREAR EXCEL
                // =========================================================

                using var workbook =
                    new XLWorkbook();

                var worksheet =
                    workbook.Worksheets.Add("Reporte");

                // =========================================================
                // ENCABEZADOS Y DATOS
                // =========================================================

                if (resultados.Count > 0)
                {
                    var columnas =
                        resultados[0]
                            .Keys
                            .ToList();

                    // -----------------------------------------------------
                    // ENCABEZADOS
                    // -----------------------------------------------------

                    for (int i = 0;
                         i < columnas.Count;
                         i++)
                    {
                        var celda =
                            worksheet.Cell(1, i + 1);

                        celda.Value =
                            columnas[i];

                        celda.Style.Font.Bold = true;
                    }

                    // -----------------------------------------------------
                    // DATOS
                    // -----------------------------------------------------

                    for (int fila = 0;
                         fila < resultados.Count;
                         fila++)
                    {
                        for (int columna = 0;
                             columna < columnas.Count;
                             columna++)
                        {
                            var valor =
                                resultados[fila][columnas[columna]];

                            var celda =
                                worksheet.Cell(
                                    fila + 2,
                                    columna + 1);

                            if (valor == null)
                            {
                                celda.Value = "";
                            }
                            else if (valor is DateTime fecha)
                            {
                                celda.Value = fecha;

                                celda.Style.DateFormat.Format =
                                    "dd/MM/yyyy HH:mm:ss";
                            }
                            else if (valor is decimal ||
                                     valor is double ||
                                     valor is float)
                            {
                                celda.Value =
                                    Convert.ToDouble(valor);
                            }
                            else if (valor is int ||
                                     valor is long ||
                                     valor is short ||
                                     valor is byte)
                            {
                                celda.Value =
                                    Convert.ToInt64(valor);
                            }
                            else if (valor is bool booleano)
                            {
                                celda.Value =
                                    booleano ? "Sí" : "No";
                            }
                            else
                            {
                                celda.Value =
                                    valor.ToString();
                            }
                        }
                    }

                    // -----------------------------------------------------
                    // CREAR TABLA
                    // -----------------------------------------------------

                    var rango =
                        worksheet.Range(
                            1,
                            1,
                            resultados.Count + 1,
                            columnas.Count);

                    rango.CreateTable();

                    // -----------------------------------------------------
                    // AJUSTAR COLUMNAS
                    // -----------------------------------------------------

                    worksheet.Columns()
                        .AdjustToContents();
                }
                else
                {
                    worksheet.Cell(1, 1).Value =
                        "El reporte no contiene información.";

                    worksheet.Cell(1, 1)
                        .Style.Font.Bold = true;
                }

                // =========================================================
                // CONGELAR ENCABEZADO
                // =========================================================

                worksheet.SheetView.FreezeRows(1);

                // =========================================================
                // GENERAR ARCHIVO
                // =========================================================

                using var stream =
                    new MemoryStream();

                workbook.SaveAs(stream);

                stream.Position = 0;

                var nombreArchivo =
                    $"{reporte.nombre}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";

                // =========================================================
                // RETORNAR EXCEL
                // =========================================================

                return File(
                    stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    nombreArchivo);
            }
            catch (Exception ex)
            {
                // =========================================================
                // GUARDAR ERROR
                // =========================================================

                LogsCatch.GuardarError(
                    ex,
                    configuracion,
                    ContextFact,
                    nombreBase);

                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        exito = false,
                        mensaje = "Error al exportar el reporte.",
                        detalle = ex.Message
                    });
            }
        }
    }
}
