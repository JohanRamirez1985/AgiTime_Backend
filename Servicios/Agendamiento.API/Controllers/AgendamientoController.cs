using Agendamiento.API.Servicios;
using Compartido;
using Compartido.Constantes;
using Compartido.Data;
using Compartido.Data.Modelos;
using Compartido.DTOs;
using Compartido.Metodos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq;

namespace Agendamiento.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AgendamientoController : ControllerBase
    {
        private readonly GeneralContext ContextGene;
        private readonly FactoryContext ContextFact;
        private readonly IConfiguration configuracion;
        private readonly AgendamientoService agendaService;
        private readonly IEmailService emailService;

        public AgendamientoController(GeneralContext gen, FactoryContext fac, IConfiguration config, IEmailService email, AgendamientoService ageSer)
        {
            ContextGene = gen;
            ContextFact = fac;
            configuracion = config;
            agendaService = ageSer;
            emailService = email;
        }

        [HttpPost("registrarCita")]
        public async Task<IActionResult> CrearCita([FromBody] CitaDTO dto)
        {
            string nombreBase = string.Empty;
            string logo = string.Empty;
            string nombreEmpresa = string.Empty;
            string nombreSede = string.Empty;
            int idCliente = 0;
            string emailCliente = "";
            string nombreCliente = "";
            int newHora = 0;
            bool pago = true;

            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == dto.empresa && e.estado == Estados.Activo);
                var resultado = ValidacionEmpresa.ValidarEmpresa(empresa);

                if (!resultado.Exito)
                {
                    return Ok(new
                    {
                        mensaje = resultado.Mensaje,
                        exito = false
                    });
                }

                nombreBase = empresa.conexionBD;
                nombreEmpresa = empresa.nombre;
                logo = "https://agitime.com.co/assets/img/" + empresa.logo;

                var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
                var connectionString = string.Format(connectionTemplate, nombreBase);

                using var ContextBD = ContextFact.Create(connectionString);
                using var transaction = ContextBD.Database.BeginTransaction();

                try
                {
                    var validaHora = ContextBD.Citas.Where(c => c.fechaInicio == dto.FechaIni 
                                                           && c.recurso == dto.recurso
                                                           && (c.estado == Estados.Activo || c.estado == Estados.Asistio || c.estado == Estados.Inasistencia)
                                                           ).FirstOrDefault();

                    if (validaHora == null)
                    {
                        if (dto.clienteInfo != null)
                        {
                            var user = ContextBD.Clientes
                                .FirstOrDefault(e => e.telefono == dto.clienteInfo.Telefono);

                            if (user == null)
                            {
                                var nueva = new Cliente
                                {
                                    nombre = dto.clienteInfo.Nombre,
                                    correo = dto.clienteInfo.Correo,
                                    telefono = dto.clienteInfo.Telefono,
                                    fechaNacimiento = dto.clienteInfo.FechaNacimiento,
                                    estado = Estados.Activo,
                                    fechaCreacion = dto.FechaCreacion
                                };

                                ContextBD.Clientes.Add(nueva);
                                ContextBD.SaveChanges();

                                idCliente = nueva.id;
                                nombreCliente = nueva.nombre;
                                emailCliente = nueva.correo;
                            }
                            else
                            {
                                idCliente = user.id;
                                nombreCliente = user.nombre;
                                emailCliente = user.correo;
                            }
                        }

                        if (dto.Hora <= 30)
                        {
                            newHora = 30;
                        }
                        else if (dto.Hora >= 45 && dto.Hora <= 60)
                        {
                            newHora = 60;
                        }
                        else if (dto.Hora > 60 && dto.Hora <= 90)
                        {
                            newHora = 90;
                        }
                        else if (dto.Hora > 90 && dto.Hora <= 120)
                        {
                            newHora = 120;
                        }
                        else if (dto.Hora > 120 && dto.Hora <= 150)
                        {
                            newHora = 150;
                        }
                        else if (dto.Hora > 150 && dto.Hora <= 180)
                        {
                            newHora = 180;
                        }
                        else
                        {
                            newHora = dto.Hora;
                        }

                        bool diaCompletoBloqueado = false;
                        var diaSemana = (int)dto.FechaIni.DayOfWeek;
                        if (diaSemana == 0) diaSemana = 7;

                        var fechaSolo = dto.FechaIni.Date;

                        var bloqueos = ContextBD.BloqueosHorarios
                                        .AsEnumerable()
                                        .Where(b => b.usuario == dto.recurso &&
                                            (
                                                (b.tipoBloqueo == 1 &&
                                                 b.diasSemana != null &&
                                                 b.diasSemana.Split(',').Contains(diaSemana.ToString()))
                                            )
                                        )
                                        .ToList();

                        foreach (var bloqueo in bloqueos)
                        {
                            var inicio = bloqueo.horaInicio;
                            var fin = bloqueo.horaFin;

                            // Día completo bloqueado
                            if (inicio == TimeSpan.Zero && fin >= TimeSpan.FromHours(23.5))
                            {
                                diaCompletoBloqueado = true;
                                break;
                            }
                        }

                        if (diaCompletoBloqueado)
                        {
                            pago = false;
                        }

                        var nuevo = new Cita
                        {
                            fechaInicio = dto.FechaIni,
                            hora = newHora,
                            fechaFin = dto.FechaIni.AddMinutes(newHora),
                            recurso = dto.recurso,
                            cliente = dto.clienteInfo != null ? idCliente : null,
                            valor = dto.Valor,
                            estado = dto.UsuarioCreacion == "Usuario" ? Estados.Asistio : Estados.Activo,
                            pago = pago,
                            fechaCreacion = dto.FechaCreacion,
                            usuarioCreacion = dto.UsuarioCreacion
                        };

                        ContextBD.Citas.Add(nuevo);
                        ContextBD.SaveChanges();

                        foreach (var detalle in dto.servicios)
                        {
                            var nuevoServicio = new CitaServicio
                            {
                                cita = nuevo.id,
                                servicio = detalle.Servicio
                            };

                            ContextBD.CitaServicios.Add(nuevoServicio);
                        }

                        ContextBD.SaveChanges();

                        transaction.Commit();

                        var infoRecurso = ContextBD.Usuarios
                                            .Include(u => u.sedeNavigation)
                                            .Where(e => e.estado == Estados.Activo
                                                && e.recurso == true
                                                && e.id == dto.recurso)
                                            .FirstOrDefault();

                        if (infoRecurso != null)
                        {
                            nombreSede = infoRecurso.sedeNavigation?.Nombre;

                            if (!string.IsNullOrEmpty(infoRecurso.correo))
                            {
                                await emailService.SendEmailAsync(
                                    infoRecurso.correo,
                                    "Confirmación de cita",
                                    agendaService.mensajeEmailUsuario(nombreCliente, infoRecurso.nombre, dto.FechaIni, logo),
                                    isHtml: true
                                );
                            }
                        }
                        else
                        {
                            var Recurso = ContextBD.Recursos
                                            .Include(u => u.sedeNavigation)
                                            .Where(e => e.estado == Estados.Activo)
                                            .FirstOrDefault();
                            if (Recurso != null)
                            {
                                nombreSede = infoRecurso.sedeNavigation?.Nombre;
                            }
                        }

                        if (!string.IsNullOrEmpty(emailCliente))
                        {
                            await emailService.SendEmailAsync(
                                emailCliente,
                                "Confirmación de cita",
                                agendaService.mensajeEmailCliente(nombreCliente, dto.FechaIni, nombreEmpresa, nombreSede, logo),
                                isHtml: true
                            );
                        }

                        return Ok(new
                        {
                            mensaje = "Cita registrada exitosamente",
                            exito = true
                        });
                    }
                    else
                    {
                        return Ok(new
                        {
                            mensaje = "Ya hay una cita agendada en ese horario.",
                            exito = false
                        });
                    }
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    LogsCatch.GuardarError(ex, configuracion, ContextFact, nombreBase);

                    return StatusCode(500, new
                    {
                        mensaje = "Ocurrió un error interno",
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

        [HttpPost("registrarBloqueo")]
        public IActionResult CrearBloqueo([FromBody] BloqueoDTO dto)
        {
            string nombreBase = string.Empty;
            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == dto.empresa && e.estado == Estados.Activo);
                var resultado = ValidacionEmpresa.ValidarEmpresa(empresa);

                if (!resultado.Exito)
                {
                    return Ok(new
                    {
                        mensaje = resultado.Mensaje,
                        exito = false
                    });
                }

                nombreBase = empresa.conexionBD;

                var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
                var connectionString = string.Format(connectionTemplate, empresa.conexionBD);

                using var ContextBD = ContextFact.Create(connectionString);

                var nuevo = new BloqueosHorario
                {
                    tipoBloqueo = dto.TipoBloqueo,
                    usuario = dto.Usuario,
                    fechaInicio = dto.FechaInicio,
                    fechaFin = dto.FechaFin,
                    horaInicio = TimeSpan.Parse(dto.HoraInicio),
                    horaFin = TimeSpan.Parse(dto.HoraFin),
                    motivo = dto.Motivo,
                    diasSemana = dto.DiasSemana,
                    fechaCreacion = dto.FechaCreacion,
                    estado = Estados.Activo
                };

                ContextBD.BloqueosHorarios.Add(nuevo);
                ContextBD.SaveChanges();

                return Ok(new
                {
                    mensaje = "Bloqueo registrado exitosamente",
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

        [HttpGet("consultarBloqueosHoras")]
        public IActionResult ObtenerBloqueosHoras([FromQuery] BloqueoDTO dto)
        {
            string nombreBase = string.Empty;
            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == dto.empresa && e.estado == Estados.Activo);
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

                var diaSemana = (int)dto.FechaInicio.Value.DayOfWeek;
                if (diaSemana == 0) diaSemana = 7;

                var fechaSolo = dto.FechaInicio.Value.Date;

                var bloqueos = ContextBD.BloqueosHorarios
                    .AsEnumerable()
                    .Where(b => b.usuario == dto.Usuario &&
                        (
                            (b.tipoBloqueo == 1 &&
                             b.diasSemana != null &&
                             b.diasSemana.Split(',').Contains(diaSemana.ToString()))

                            //||

                            //(b.tipoBloqueo == 2 &&
                            // b.fechaInicio.HasValue && b.fechaFin.HasValue &&
                            // fechaSolo >= b.fechaInicio.Value.Date &&
                            // fechaSolo <= b.fechaFin.Value.Date &&
                            // b.diasSemana != null &&
                            // b.diasSemana.Split(',').Contains(diaSemana.ToString()))

                            ||

                            (b.tipoBloqueo == 2 &&
                             b.fechaInicio.HasValue &&
                             fechaSolo >= b.fechaInicio.Value.Date)
                        )
                    )
                    .ToList();

                var resultado = new ResultadoBloqueo();

                foreach (var bloqueo in bloqueos)
                {
                    var inicio = bloqueo.horaInicio;
                    var fin = bloqueo.horaFin;

                    if (inicio == TimeSpan.Zero && fin >= TimeSpan.FromHours(23.5))
                    {
                        resultado.DiaCompletoBloqueado = true;
                        resultado.HorasBloqueadas.Clear();
                        return Ok(new
                        {
                            exito = true,
                            datos = resultado
                        });
                    }

                    var hora = inicio;
                    while (hora < fin)
                    {
                        resultado.HorasBloqueadas.Add(DateTime.Today.Add(hora).ToString("hh:mm tt", new CultureInfo("en-US")));
                        hora = hora.Add(TimeSpan.FromMinutes(30));
                    }
                }

                return Ok(new
                {
                    exito = true,
                    datos = resultado
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

        [HttpGet("consultarBloqueos")]
        public IActionResult ObtenerBloqueos([FromQuery] BloqueoDTO dto)
        {
            string nombreBase = string.Empty;
            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == dto.empresa && e.estado == Estados.Activo);
                var resultado = ValidacionEmpresa.ValidarEmpresa(empresa);

                if (!resultado.Exito)
                {
                    return Ok(new
                    {
                        mensaje = resultado.Mensaje,
                        exito = false
                    });
                }

                nombreBase = empresa.conexionBD;

                var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
                var connectionString = string.Format(connectionTemplate, empresa.conexionBD);

                using var ContextBD = ContextFact.Create(connectionString);

                var diaSemana = (int)dto.FechaInicio.Value.DayOfWeek;
                if (diaSemana == 0) diaSemana = 7;

                var fechaSolo = dto.FechaInicio.Value.Date;

                var bloqueos = ContextBD.BloqueosHorarios
                    .AsEnumerable()
                    .Where(b => b.usuario == dto.Usuario &&
                        (
                            (b.tipoBloqueo == 1)

                            ||

                            (b.tipoBloqueo == 2 &&
                             b.fechaInicio.HasValue &&
                             fechaSolo == b.fechaInicio.Value.Date)
                        )
                    )
                    .ToList();

                return Ok(new
                {
                    exito = true,
                    datos = bloqueos
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

        [HttpGet("consultarAgendaRecurso")]
        public IActionResult ObtenerAgendaRecurso([FromQuery] CitaDTO dto)
        {
            string nombreBase = string.Empty;
            var horasOcupadas = new HashSet<string>();
            bool diaCompletoBloqueado = false;

            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == dto.empresa && e.estado == Estados.Activo);
                var resultado = ValidacionEmpresa.ValidarEmpresa(empresa);

                if (!resultado.Exito)
                {
                    return Ok(new
                    {
                        mensaje = resultado.Mensaje,
                        exito = false
                    });
                }

                nombreBase = empresa.conexionBD;

                var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
                var connectionString = string.Format(connectionTemplate, empresa.conexionBD);

                using var ContextBD = ContextFact.Create(connectionString);

                var diaSemana = (int)dto.FechaIni.DayOfWeek;
                if (diaSemana == 0) diaSemana = 7;

                var fechaSolo = dto.FechaIni.Date;

                var bloqueos = ContextBD.BloqueosHorarios
                    .AsEnumerable()
                    .Where(b => b.usuario == dto.recurso &&
                        (
                            (b.tipoBloqueo == 1 &&
                             b.diasSemana != null &&
                             b.diasSemana.Split(',').Contains(diaSemana.ToString()))
                            ||
                            (b.tipoBloqueo == 2 &&
                             b.fechaInicio.HasValue &&
                             fechaSolo == b.fechaInicio.Value.Date)
                        )
                    )
                    .ToList();

                foreach (var bloqueo in bloqueos)
                {
                    var inicio = bloqueo.horaInicio;
                    var fin = bloqueo.horaFin;

                    // Día completo bloqueado
                    if (inicio == TimeSpan.Zero && fin >= TimeSpan.FromHours(23.5))
                    {
                        diaCompletoBloqueado = true;
                        horasOcupadas.Clear();
                        break;
                    }

                    var hora = inicio;
                    while (hora < fin)
                    {
                        horasOcupadas.Add(DateTime.Today.Add(hora).ToString("HH:mm"));
                        hora = hora.Add(TimeSpan.FromMinutes(30));
                    }
                }

                if (!diaCompletoBloqueado)
                {
                    var listaCitas = ContextBD.Citas
                                .Where(e => e.fechaInicio >= dto.FechaIni
                                         && e.fechaFin <= dto.FechaFin
                                         && e.recurso == dto.recurso
                                         && (e.estado == Estados.Activo || e.estado == Estados.Asistio || e.estado == Estados.Inasistencia))
                                .ToList();

                    foreach (var item in listaCitas)
                    {
                        int cantidad = item.hora / 30;
                        for (int i = 0; i < cantidad; i++)
                        {
                            DateTime horaActual = item.fechaInicio.AddMinutes(i * 30);
                            horasOcupadas.Add(horaActual.ToString("HH:mm"));
                        }
                    }
                }

                if (horasOcupadas.Count == 0 && !diaCompletoBloqueado)
                {
                    return Ok(new
                    {
                        mensaje = "No hay citas registradas",
                        exito = false
                    });
                }

                var listaOrdenada = horasOcupadas
                                    .Select(h => TimeSpan.Parse(h))
                                    .OrderBy(t => t)
                                    .Select(t => DateTime.Today.Add(t).ToString("hh:mm tt", new CultureInfo("en-US")))
                                    .ToList();

                return Ok(new
                {
                    exito = true,
                    diaCompletoBloqueado,
                    horasOcupadas = listaOrdenada
                });

                //var lista = ContextBD.Citas
                //                     .Where(e => e.fechaInicio >= dto.FechaIni
                //                            && e.fechaFin <= dto.FechaFin
                //                            && e.recurso == dto.recurso
                //                            && (e.estado == Estados.Activo || e.estado == Estados.Asistio || e.estado == Estados.Inasistencia))
                //                     .OrderBy(o => o.fechaInicio)
                //                     .ToList();

                //if (lista == null || lista.Count == 0)
                //{
                //    return Ok(new
                //    {
                //        mensaje = "No hay citas registradas",
                //        exito = false
                //    });
                //}
                //else
                //{
                //    foreach (var item in lista)
                //    {
                //        int cantidad = item.hora / 30;
                //        for (int i = 0; i < cantidad; i++)
                //        {
                //            DateTime horaActual = item.fechaInicio.AddMinutes(i * 30);
                //            horasGeneradas.Add(horaActual.ToString("hh:mm tt", new CultureInfo("en-US")));
                //        }
                //    }
                //}

                //return Ok(new
                //{
                //    exito = true,
                //    datos = horasGeneradas
                //});
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

        [HttpGet("consultarMiAgenda")]
        public IActionResult ObtenerMiAgenda([FromQuery] CitaDTO dto)

        {
            string nombreBase = string.Empty;
            var horasOcupadas = new HashSet<string>();
            bool diaCompletoBloqueado = false;

            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == dto.empresa && e.estado == Estados.Activo);
                var resultado = ValidacionEmpresa.ValidarEmpresa(empresa);

                if (!resultado.Exito)
                {
                    return Ok(new
                    {
                        mensaje = resultado.Mensaje,
                        exito = false
                    });
                }

                nombreBase = empresa.conexionBD;

                var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
                var connectionString = string.Format(connectionTemplate, empresa.conexionBD);

                using var ContextBD = ContextFact.Create(connectionString);

                var listaCitas = ContextBD.Citas
                            .Where(e => e.fechaInicio >= dto.FechaIni
                                     && e.fechaFin <= dto.FechaFin
                                     && e.recurso == dto.recurso
                                     && (e.estado == Estados.Activo || e.estado == Estados.Asistio))
                            .ToList();

                foreach (var item in listaCitas)
                {
                    int cantidad = item.hora / 30;
                    for (int i = 0; i < cantidad; i++)
                    {
                        DateTime horaActual = item.fechaInicio.AddMinutes(i * 30);
                        horasOcupadas.Add(horaActual.ToString("HH:mm"));
                    }
                }
                
                if (horasOcupadas.Count == 0)
                {
                    return Ok(new
                    {
                        mensaje = "No hay citas registradas",
                        exito = false
                    });
                }

                var listaOrdenada = horasOcupadas
                                    .Select(h => TimeSpan.Parse(h))
                                    .OrderBy(t => t)
                                    .Select(t => DateTime.Today.Add(t).ToString("hh:mm tt", new CultureInfo("en-US")))
                                    .ToList();

                return Ok(new
                {
                    exito = true,
                    diaCompletoBloqueado,
                    horasOcupadas = listaOrdenada
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

        [HttpGet("consultarCitasCliente")]
        public IActionResult ObtenerCitasCliente([FromQuery] CitaDTO dto)
        {
            string nombreBase = string.Empty;
            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == dto.empresa && e.estado == Estados.Activo);
                var resultado = ValidacionEmpresa.ValidarEmpresa(empresa);

                if (!resultado.Exito)
                {
                    return Ok(new
                    {
                        mensaje = resultado.Mensaje,
                        exito = false
                    });
                }

                nombreBase = empresa.conexionBD;

                var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
                var connectionString = string.Format(connectionTemplate, empresa.conexionBD);

                using var ContextBD = ContextFact.Create(connectionString);

                var user = ContextBD.Clientes
                            .FirstOrDefault(e => e.telefono == dto.clienteInfo.Telefono);

                if (user == null)
                {
                    return Ok(new
                    {
                        mensaje = "Con el telefono ingresado no hay citas agendadas",
                        exito = false
                    });
                }

                var citasRaw = ContextBD.Citas
                    .Where(e => e.fechaInicio >= dto.FechaIni
                                && e.cliente == user.id
                                && (e.estado == Estados.Activo || e.estado == Estados.Asistio || e.estado == Estados.Inasistencia))
                    .Select(c => new
                    {
                        c.id,
                        c.fechaInicio,
                        c.estado,
                        c.recurso,
                        Servicios = c.CitaServicios.Select(cs => new
                        {
                            Id = cs.servicioNavigation.id,
                            Nombre = cs.servicioNavigation.nombre,
                            Valor = cs.servicioNavigation.valor
                        }).ToList()
                    }).ToList();

                var recursoIds = citasRaw.Select(c => c.recurso).Distinct().ToList();

                var usuariosRecursos = ContextBD.Usuarios
                                        .Where(u => recursoIds.Contains(u.id) && u.recurso)
                                        .Select(u => new {
                                            u.id,
                                            u.nombre,
                                            nombreSede = u.sedeNavigation.Nombre
                                        })
                                        .ToList();

                List<int> usuarioIds = usuariosRecursos.Select(u => u.id).ToList();
                var recursos = new List<(int id, string nombre, string nombreSede)>();

                if (!usuariosRecursos.Any())
                {
                    recursos = ContextBD.Recursos
                                .Where(r => recursoIds.Contains(r.id))
                                .Select(r => new ValueTuple<int, string, string>(
                                    r.id,
                                    r.nombre,
                                    r.sedeNavigation.Nombre
                                ))
                                .ToList();
                }

                var recursoNombreDict = usuariosRecursos
                                        .Select(u => new
                                        {
                                            u.id,
                                            u.nombre,
                                            u.nombreSede
                                        })
                                        .Concat(
                                            recursos.Select(r => new
                                            {
                                                id = r.id,
                                                nombre = r.nombre,
                                                nombreSede = "Sin sede"
                                            })
                                        )
                                        .ToDictionary(x => x.id, x => new { x.nombre, x.nombreSede });

                var lista = citasRaw
                            .Select(c => new
                            {
                                c.id,
                                c.fechaInicio,
                                c.estado,
                                nombreCliente = user.nombre,
                                telefonoCliente = user.telefono,
                                servicios = c.Servicios,
                                recursoNombre = recursoNombreDict.ContainsKey(c.recurso)
                                    ? recursoNombreDict[c.recurso].nombre
                                    : "Desconocido",
                                sede = recursoNombreDict.ContainsKey(c.recurso)
                                    ? recursoNombreDict[c.recurso].nombreSede
                                    : "Desconocida"
                            })
                            .ToList();

                //.AsEnumerable()
                //.Select(c => new
                //{
                //    c.id,
                //    c.fechaInicio,
                //    c.estado,
                //    nombreCliente = user.nombre,
                //    telefonoCliente = user.telefono,
                //    servicios = c.Servicios
                //})
                //.ToList();


                if (lista == null || lista.Count == 0)
                {
                    return Ok(new
                    {
                        mensaje = "No hay citas registradas",
                        exito = false
                    });
                }

                return Ok(new
                {
                    exito = true,
                    datos = lista
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

        [HttpGet("consultarCitasRecurso")]
        public IActionResult ObtenerCitasRecurso([FromQuery] CitaDTO dto)
        {
            string nombreBase = string.Empty;
            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == dto.empresa && e.estado == Estados.Activo);
                var resultado = ValidacionEmpresa.ValidarEmpresa(empresa);

                if (!resultado.Exito)
                {
                    return Ok(new
                    {
                        mensaje = resultado.Mensaje,
                        exito = false
                    });
                }

                nombreBase = empresa.conexionBD;

                var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
                var connectionString = string.Format(connectionTemplate, empresa.conexionBD);

                using var ContextBD = ContextFact.Create(connectionString);

                var lista = ContextBD.Citas
                    .Where(e => e.fechaInicio >= dto.FechaIni
                                && e.fechaFin <= dto.FechaFin
                                && e.recurso == dto.recurso
                                && (e.estado == Estados.Activo || e.estado == Estados.Asistio || e.estado == Estados.Inasistencia))
                    .Select(c => new
                    {
                        c.id,
                        c.fechaInicio,
                        c.estado,
                        Cliente = ContextBD.Clientes
                            .Where(cl => cl.id == c.cliente)
                            .Select(cl => new
                            {
                                NombreCliente = cl.nombre,
                                TelefonoCliente = cl.telefono
                            })
                            .FirstOrDefault(),
                        Servicios = c.CitaServicios.Select(cs => new
                        {
                            Id = cs.servicioNavigation.id,
                            Nombre = cs.servicioNavigation.nombre,
                            Valor = cs.servicioNavigation.valor
                        }).ToList()
                    })
                    .AsEnumerable()
                    .Select(c => new
                    {
                        c.id,
                        c.fechaInicio,
                        c.estado,
                        nombreCliente = c.Cliente?.NombreCliente,
                        telefonoCliente = c.Cliente?.TelefonoCliente,
                        servicios = c.Servicios
                    })
                    .OrderBy(o => o.fechaInicio)
                    .ToList();

                if (lista == null || lista.Count == 0)
                {
                    return Ok(new
                    {
                        mensaje = "No hay citas registradas",
                        exito = false
                    });
                }

                return Ok(new
                {
                    exito = true,
                    datos = lista
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

        [HttpPost("cancelarCita")]
        public IActionResult CancelarCita([FromBody] CitaDTO dto)
        {
            string nombreBase = string.Empty;
            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == dto.empresa && e.estado == Estados.Activo);
                var resultado = ValidacionEmpresa.ValidarEmpresa(empresa);

                if (!resultado.Exito)
                {
                    return Ok(new
                    {
                        mensaje = resultado.Mensaje,
                        exito = false
                    });
                }

                nombreBase = empresa.conexionBD;

                var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
                var connectionString = string.Format(connectionTemplate, nombreBase);

                using var ContextBD = ContextFact.Create(connectionString);

                var citaId = ContextBD.Citas.FirstOrDefault(p => p.id == dto.Id);

                if (citaId == null)
                {
                    return Ok(new
                    {
                        mensaje = "Cita no encontrada",
                        exito = false
                    });
                }

                citaId.fechaCancelacion = dto.FechaCancelacion;
                citaId.usuarioCancelacion = dto.UsuarioCancelacion;
                citaId.estado = Estados.Cancelada;

                ContextBD.SaveChanges();

                return Ok(new
                {
                    mensaje = "Cita cancelada exitosamente",
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

        [HttpPost("asistioCita")]
        public IActionResult AsistioCita([FromBody] CitaDTO dto)
        {
            string nombreBase = string.Empty;
            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == dto.empresa && e.estado == Estados.Activo);
                var resultado = ValidacionEmpresa.ValidarEmpresa(empresa);

                if (!resultado.Exito)
                {
                    return Ok(new
                    {
                        mensaje = resultado.Mensaje,
                        exito = false
                    });
                }

                nombreBase = empresa.conexionBD;

                var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
                var connectionString = string.Format(connectionTemplate, nombreBase);

                using var ContextBD = ContextFact.Create(connectionString);

                var citaId = ContextBD.Citas.FirstOrDefault(p => p.id == dto.Id);

                if (citaId == null)
                {
                    return Ok(new
                    {
                        mensaje = "Cita no encontrada",
                        exito = false
                    });
                }
                if (dto.Asistencia)
                    citaId.estado = Estados.Asistio;
                else
                    citaId.estado = Estados.Inasistencia;


                ContextBD.SaveChanges();

                return Ok(new
                {
                    mensaje = "Cita validada exitosamente",
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

        [HttpPost("cambiarEstadoBloqueo")]
        public IActionResult CambiarEstadoBloqueo([FromBody] BloqueoDTO dto)
        {
            string nombreBase = string.Empty;
            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == dto.empresa && e.estado == Estados.Activo);
                var resultado = ValidacionEmpresa.ValidarEmpresa(empresa);

                if (!resultado.Exito)
                {
                    return Ok(new
                    {
                        mensaje = resultado.Mensaje,
                        exito = false
                    });
                }

                nombreBase = empresa.conexionBD;

                var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
                var connectionString = string.Format(connectionTemplate, nombreBase);

                using var ContextBD = ContextFact.Create(connectionString);

                var bloqueoId = ContextBD.BloqueosHorarios.FirstOrDefault(p => p.id == dto.Id);

                if (bloqueoId == null)
                {
                    return Ok(new
                    {
                        mensaje = "Bloqueo no encontrado",
                        exito = false
                    });
                }

                if (dto.Activar)
                    bloqueoId.estado = Estados.Activo;
                else
                    bloqueoId.estado = Estados.Inactivo;

                ContextBD.SaveChanges();

                return Ok(new
                {
                    mensaje = "Bloqueo actualizado exitosamente",
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
    }
}
