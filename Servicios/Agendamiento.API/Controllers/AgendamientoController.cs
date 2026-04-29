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
            string telefonoCliente = "";
            //int newHora = 0;
            bool pago = true;

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
                                telefonoCliente = nueva.telefono;
                            }
                            else
                            {
                                idCliente = user.id;
                                nombreCliente = user.nombre;
                                emailCliente = user.correo;
                                telefonoCliente = user.telefono;
                            }
                        }

                        bool diaCompletoBloqueado = false;
                        var diaSemana = (int)dto.FechaIni.DayOfWeek;
                        if (diaSemana == 0) diaSemana = 7;

                        var fechaSolo = dto.FechaIni.Date;

                        var bloqueos = ContextBD.BloqueosHorarios
                                        .AsEnumerable()
                                        .Where(b => b.usuario == dto.recurso &&
                                            b.tipoBloqueo == 3 &&
                                            b.estado == Estados.Activo &&
                                            b.diasSemana != null &&
                                            (
                                                diaSemana == 7 &&
                                                b.diasSemana.Split(',')
                                                            .Select(x => x.Trim())
                                                            .Contains("7")
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
                            hora = dto.Hora,
                            fechaFin = dto.FechaIni.AddMinutes(dto.Hora),
                            recurso = dto.recurso,
                            cliente = dto.clienteInfo != null ? idCliente : null,
                            valor = dto.Valor,
                            estado = dto.UsuarioCreacion == "Usuario" ? Estados.Asistio : Estados.Activo,
                            pago = pago,
                            bono = dto.UsuarioCreacion == "Usuario" ? dto.Bono : null,
                            liquidado = false,
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

                        if (dto.UsuarioCreacion != "Usuario")
                        {
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
                                        agendaService.mensajeEmailUsuario(nombreCliente, telefonoCliente, infoRecurso.nombre, dto.FechaIni, logo),
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

        [HttpPost("guardarCita")]
        public async Task<IActionResult> GuardarCita([FromBody] PadreDTO dto)
        {
            string nombreBase = string.Empty;
            string logo = string.Empty;
            string nombreEmpresa = string.Empty;
            string nombreSede = string.Empty;
            int idCliente = 0;
            string emailCliente = "";
            string nombreCliente = "";
            string telefonoCliente = "";
            bool pago = true;

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
                    //var validaHora = ContextBD.Citas.Where(c => c.fechaInicio == dto.FechaIni
                    //                                       && c.recurso == dto.recurso
                    //                                       && (c.estado == Estados.Activo || c.estado == Estados.Asistio || c.estado == Estados.Inasistencia)
                    //                                       ).FirstOrDefault();

                    var validacion = agendaService.ValidarDisponibilidad(ContextBD, dto.Citas);

                    if (!validacion.Exito)
                    {
                        if (dto.clienteInfo != null)
                        {
                            var user = ContextBD.Clientes
                                .FirstOrDefault(e => e.telefono == dto.clienteInfo.Telefono);

                            if (user == null)
                            {
                                var nuevoCliente = new Cliente
                                {
                                    nombre = dto.clienteInfo.Nombre,
                                    correo = dto.clienteInfo.Correo,
                                    telefono = dto.clienteInfo.Telefono,
                                    fechaNacimiento = dto.clienteInfo.FechaNacimiento,
                                    estado = Estados.Activo,
                                    fechaCreacion = dto.FechaCreacion
                                };

                                ContextBD.Clientes.Add(nuevoCliente);
                                ContextBD.SaveChanges();

                                idCliente = nuevoCliente.id;
                                nombreCliente = nuevoCliente.nombre;
                                emailCliente = nuevoCliente.correo;
                                telefonoCliente = nuevoCliente.telefono;
                            }
                            else
                            {
                                idCliente = user.id;
                                nombreCliente = user.nombre;
                                emailCliente = user.correo;
                                telefonoCliente = user.telefono;
                            }
                        }

                        //bool diaCompletoBloqueado = false;
                        //var diaSemana = (int)dto.FechaIni.DayOfWeek;
                        //if (diaSemana == 0) diaSemana = 7;

                        //var fechaSolo = dto.FechaIni.Date;

                        //var bloqueos = ContextBD.BloqueosHorarios
                        //                .AsEnumerable()
                        //                .Where(b => b.usuario == dto.recurso &&
                        //                    b.tipoBloqueo == 3 &&
                        //                    b.estado == Estados.Activo &&
                        //                    b.diasSemana != null &&
                        //                    (
                        //                        diaSemana == 7 &&
                        //                        b.diasSemana.Split(',')
                        //                                    .Select(x => x.Trim())
                        //                                    .Contains("7")
                        //                    )
                        //                )
                        //                .ToList();

                        //foreach (var bloqueo in bloqueos)
                        //{
                        //    var inicio = bloqueo.horaInicio;
                        //    var fin = bloqueo.horaFin;

                        //    // Día completo bloqueado
                        //    if (inicio == TimeSpan.Zero && fin >= TimeSpan.FromHours(23.5))
                        //    {
                        //        diaCompletoBloqueado = true;
                        //        break;
                        //    }
                        //}

                        //if (diaCompletoBloqueado)
                        //{
                        //    pago = false;
                        //}

                        var nuevoPadre = new Padre
                        {
                            fechaCita = dto.FechaIni
                        };

                        ContextBD.Padres.Add(nuevoPadre);
                        ContextBD.SaveChanges();

                        foreach (var detalleCita in dto.Citas)
                        {
                            var nuevaCita = new Cita
                            {
                                fechaInicio = detalleCita.FechaIni,
                                hora = detalleCita.Hora,
                                fechaFin = detalleCita.FechaIni.AddMinutes(detalleCita.Hora),
                                recurso = detalleCita.recurso,
                                cliente = detalleCita.clienteInfo != null ? idCliente : null,
                                valor = detalleCita.Valor,
                                estado = dto.UsuarioCreacion == "Usuario" ? Estados.Asistio : Estados.Activo,
                                pago = pago,
                                bono = dto.UsuarioCreacion == "Usuario" ? detalleCita.Bono : null,
                                liquidado = false,
                                fechaCreacion = dto.FechaCreacion,
                                usuarioCreacion = dto.UsuarioCreacion
                            };

                            ContextBD.Citas.Add(nuevaCita);
                            ContextBD.SaveChanges();

                            foreach (var detalleServicio in detalleCita.servicios)
                            {
                                var nuevoServicio = new CitaServicio
                                {
                                    cita = nuevaCita.id,
                                    servicio = detalleServicio.Servicio
                                };

                                ContextBD.CitaServicios.Add(nuevoServicio);
                            }

                            var citaPadre = new CitaPadre
                            {
                                cita = nuevaCita.id,
                                padre = nuevoPadre.id,
                            };

                            ContextBD.CitaPadres.Add(citaPadre);
                        }
                        ContextBD.SaveChanges();
                        transaction.Commit();

                        if (dto.UsuarioCreacion != "Usuario")
                        {
                            var infoRecurso = ContextBD.Usuarios
                                                .Include(u => u.sedeNavigation)
                                                .Where(e => e.estado == Estados.Activo
                                                    && e.recurso == true
                                                    && e.id == 8)
                                                .FirstOrDefault();

                            if (infoRecurso != null)
                            {
                                nombreSede = infoRecurso.sedeNavigation?.Nombre;

                                if (!string.IsNullOrEmpty(infoRecurso.correo))
                                {
                                    await emailService.SendEmailAsync(
                                        infoRecurso.correo,
                                        "Confirmación de cita",
                                        agendaService.mensajeEmailUsuario(nombreCliente, telefonoCliente, infoRecurso.nombre, dto.FechaIni, logo),
                                        isHtml: true
                                    );
                                }
                            }
                            //else
                            //{
                            //    var Recurso = ContextBD.Recursos
                            //                    .Include(u => u.sedeNavigation)
                            //                    .Where(e => e.estado == Estados.Activo)
                            //                    .FirstOrDefault();
                            //    if (Recurso != null)
                            //    {
                            //        nombreSede = infoRecurso.sedeNavigation?.Nombre;
                            //    }
                            //}

                            //if (!string.IsNullOrEmpty(emailCliente))
                            //{
                            //    await emailService.SendEmailAsync(
                            //        emailCliente,
                            //        "Confirmación de cita",
                            //        agendaService.mensajeEmailCliente(nombreCliente, dto.FechaIni, nombreEmpresa, nombreSede, logo),
                            //        isHtml: true
                            //    );
                            //}
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
                            mensaje = validacion.Mensaje,
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

                if (empresa == null)
                {
                    return Ok(new
                    {
                        mensaje = "Empresa no encontrada o inactiva",
                        exito = false
                    });
                }

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

                            ||

                            (b.tipoBloqueo == 2 &&
                             b.fechaInicio.HasValue &&
                             fechaSolo >= b.fechaInicio.Value.Date)

                            ||

                            (b.tipoBloqueo == 3 &&
                             b.diasSemana != null &&
                             b.diasSemana.Split(',').Contains(diaSemana.ToString()))

                            ||

                            (b.tipoBloqueo == 4 &&
                             b.fechaInicio.HasValue &&
                             b.fechaFin.HasValue &&
                             fechaSolo >= b.fechaInicio.Value.Date &&
                             fechaSolo <= b.fechaFin.Value.Date)
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

                if (empresa == null)
                {
                    return Ok(new
                    {
                        mensaje = "Empresa no encontrada o inactiva",
                        exito = false
                    });
                }

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

                var par = ContextBD.Parametros.Where(p => p.tipo == "Activarme" && p.estado == true).FirstOrDefault();

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
                             b.motivo != par.valor &&
                             b.fechaInicio.HasValue &&
                             fechaSolo == b.fechaInicio.Value.Date)

                             ||

                            (b.tipoBloqueo == 3)

                             ||

                            (b.tipoBloqueo == 4 &&
                             b.fechaInicio.HasValue &&
                             b.fechaFin.HasValue &&
                             fechaSolo >= b.fechaInicio.Value.Date &&
                             fechaSolo <= b.fechaFin.Value.Date)
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

                if (empresa == null)
                {
                    return Ok(new
                    {
                        mensaje = "Empresa no encontrada o inactiva",
                        exito = false
                    });
                }

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

                var tiempo = ContextBD.Parametros.Where(p => p.tipo == "Tiempo_Agenda" && p.estado == true).FirstOrDefault();

                var diaSemana = (int)dto.FechaIni.DayOfWeek;
                if (diaSemana == 0) diaSemana = 7;

                var fechaSolo = dto.FechaIni.Date;

                var bloqueos = ContextBD.BloqueosHorarios
                    .Where(b => b.usuario == dto.recurso && b.estado == Estados.Activo)
                    .AsEnumerable()
                    .Where(b =>
                        (
                            (b.tipoBloqueo == 1 &&
                             b.diasSemana != null &&
                             b.diasSemana.Split(',').Contains(diaSemana.ToString()))
                            ||
                            (b.tipoBloqueo == 2 &&
                             b.fechaInicio.HasValue &&
                             fechaSolo == b.fechaInicio.Value.Date)
                            ||
                            (b.tipoBloqueo == 3 &&
                             b.diasSemana != null &&
                             b.diasSemana.Split(',').Contains(diaSemana.ToString()))
                            ||
                            (b.tipoBloqueo == 4 &&
                             b.fechaInicio.HasValue &&
                             b.fechaFin.HasValue &&
                             fechaSolo >= b.fechaInicio.Value.Date &&
                             fechaSolo <= b.fechaFin.Value.Date)
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
                        hora = hora.Add(TimeSpan.FromMinutes(int.Parse(tiempo.valor)));
                    }
                }

                if (!diaCompletoBloqueado)
                {
                    var recursos = empresa.servicio
                                ? dto.profesional.Select(x => x.recurso).ToList()
                                : new List<int> { dto.recurso };

                    var listaCitas = ContextBD.Citas
                        .Where(e => e.fechaInicio >= dto.FechaIni
                                 && e.fechaFin <= dto.FechaFin
                                 && recursos.Contains(e.recurso)
                                 && (e.estado == Estados.Activo || e.estado == Estados.Asistio))
                        .ToList();

                    foreach (var item in listaCitas)
                    {
                        int cantidad = item.hora / int.Parse(tiempo.valor);

                        for (int i = 0; i < cantidad; i++)
                        {
                            DateTime horaActual = item.fechaInicio.AddMinutes(i * int.Parse(tiempo.valor));
                            horasOcupadas.Add(horaActual.ToString("HH:mm"));
                        }
                    }

                    //var listaCitas = ContextBD.Citas
                    //            .Where(e => e.fechaInicio >= dto.FechaIni
                    //                     && e.fechaFin <= dto.FechaFin
                    //                     && e.recurso == dto.recurso
                    //                     && (e.estado == Estados.Activo || e.estado == Estados.Asistio))
                    //            .ToList();

                    //foreach (var item in listaCitas)
                    //{
                    //    int cantidad = item.hora / int.Parse(tiempo.valor);
                    //    for (int i = 0; i < cantidad; i++)
                    //    {
                    //        DateTime horaActual = item.fechaInicio.AddMinutes(i * int.Parse(tiempo.valor));
                    //        horasOcupadas.Add(horaActual.ToString("HH:mm"));
                    //    }
                    //}
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

                if (empresa == null)
                {
                    return Ok(new
                    {
                        mensaje = "Empresa no encontrada o inactiva",
                        exito = false
                    });
                }

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

                var tiempo = ContextBD.Parametros.Where(p => p.tipo == "Tiempo_Agenda" && p.estado == true).FirstOrDefault();

                var listaCitas = ContextBD.Citas
                            .Where(e => e.fechaInicio >= dto.FechaIni
                                     && e.fechaFin <= dto.FechaFin
                                     && e.recurso == dto.recurso
                                     && (e.estado == Estados.Activo || e.estado == Estados.Asistio))
                            .ToList();

                foreach (var item in listaCitas)
                {
                    int cantidad = item.hora / int.Parse(tiempo.valor);
                    for (int i = 0; i < cantidad; i++)
                    {
                        DateTime horaActual = item.fechaInicio.AddMinutes(i * int.Parse(tiempo.valor));
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

                if (empresa == null)
                {
                    return Ok(new
                    {
                        mensaje = "Empresa no encontrada o inactiva",
                        exito = false
                    });
                }

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
                                && (e.estado == Estados.Activo || e.estado == Estados.Asistio))
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

                if (empresa == null)
                {
                    return Ok(new
                    {
                        mensaje = "Empresa no encontrada o inactiva",
                        exito = false
                    });
                }

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
                                && (e.estado == Estados.Activo || e.estado == Estados.Asistio))
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
                            Valor = cs.servicioNavigation.nombre == "Otro" ? c.valor : cs.servicioNavigation.valor
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

                if (!lista.Any())
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

                if (empresa == null)
                {
                    return Ok(new
                    {
                        mensaje = "Empresa no encontrada o inactiva",
                        exito = false
                    });

                }
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

                if (empresa == null)
                {
                    return Ok(new
                    {
                        mensaje = "Empresa no encontrada o inactiva",
                        exito = false
                    });
                }

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
                {
                    citaId.estado = Estados.Asistio;
                    citaId.bono = dto.Bono;
                }
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

                if (empresa == null)
                {
                    return Ok(new
                    {
                        mensaje = "Empresa no encontrada o inactiva",
                        exito = false
                    });
                }

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

        [HttpGet("consultarEstadoActivarme")]
        public IActionResult ObtenerEstadoActivarme([FromQuery] int emp, int usuario)
        {
            string nombreBase = string.Empty;
            bool retorno = false;
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

                var par = ContextBD.Parametros.Where(p => p.tipo == "Activarme" && p.estado == true).FirstOrDefault();

                var bloqueos = ContextBD.BloqueosHorarios
                    .AsEnumerable()
                    .Where(b => b.usuario == usuario &&
                        (
                            (b.tipoBloqueo == 2 &&
                             b.motivo == par.valor)
                        )
                    )
                    .ToList();

                if (bloqueos != null && bloqueos.Count > 0)
                {
                    retorno = true;
                }

                return Ok(new
                {
                    exito = true,
                    datos = retorno
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

        [HttpPost("cambiarEstadoActivarme")]
        public IActionResult CambiarEstadoActivarme([FromBody] BloqueoDTO dto)
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

                var par = ContextBD.Parametros.Where(p => p.tipo == "Activarme" && p.estado == true).FirstOrDefault();

                var bloqueoId = ContextBD.BloqueosHorarios.FirstOrDefault(p => p.usuario == dto.Usuario && p.motivo == par.valor);

                if (bloqueoId == null)
                {
                    return Ok(new
                    {
                        mensaje = "Bloqueo no encontrado",
                        exito = false
                    });
                }

                ContextBD.BloqueosHorarios.Remove(bloqueoId);
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

        [HttpGet("consultarAgendaDiaria")]
        public IActionResult ObtenerAgendaDiaria([FromQuery] CitaDTO dto)
        {
            string nombreBase = string.Empty;
            var agendaDiaria = new List<object>();

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

                var tiempo = ContextBD.Parametros.Where(p => p.tipo == "Tiempo_Agenda" && p.estado == true).FirstOrDefault();

                var diaSemana = (int)dto.FechaIni.DayOfWeek;
                if (diaSemana == 0) diaSemana = 7;

                var fechaSolo = dto.FechaIni.Date;

                var par = ContextBD.Parametros.Where(p => p.tipo == "Activarme" && p.estado == true).FirstOrDefault();

                var usuario = ContextBD.Usuarios.Where(u => u.estado == Estados.Activo && u.recurso == true).ToList();

                foreach (Usuario u in usuario) 
                {
                    var listaOrdenada = new List<string>();
                    var horasOcupadas = new HashSet<string>();
                    bool diaCompletoBloqueado = false;

                    var bloqueos = ContextBD.BloqueosHorarios
                    .AsEnumerable()
                    .Where(b => b.usuario == u.id &&
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

                        if ((inicio == TimeSpan.Zero && fin >= TimeSpan.FromHours(23.5)) || bloqueo.motivo == par.valor)
                        {
                            diaCompletoBloqueado = true;
                            horasOcupadas.Clear();
                            break;
                        }

                        var hora = inicio;
                        while (hora < fin)
                        {
                            horasOcupadas.Add(DateTime.Today.Add(hora).ToString("HH:mm"));
                            hora = hora.Add(TimeSpan.FromMinutes(int.Parse(tiempo.valor)));
                        }
                    }

                    if (!diaCompletoBloqueado)
                    {
                        var listaCitas = ContextBD.Citas
                                    .Where(e => e.fechaInicio >= dto.FechaIni
                                             && e.fechaFin <= dto.FechaFin
                                             && e.recurso == u.id
                                             && (e.estado == Estados.Activo || e.estado == Estados.Asistio))
                                    .ToList();

                        foreach (var item in listaCitas)
                        {
                            int cantidad = item.hora / int.Parse(tiempo.valor);
                            for (int i = 0; i < cantidad; i++)
                            {
                                DateTime horaActual = item.fechaInicio.AddMinutes(i * int.Parse(tiempo.valor));
                                horasOcupadas.Add(horaActual.ToString("HH:mm"));
                            }
                        }
                    }

                    if (horasOcupadas.Count >= 0 && !diaCompletoBloqueado)
                    {
                        listaOrdenada = horasOcupadas
                                             .Select(h => TimeSpan.Parse(h))
                                             .OrderBy(t => t)
                                             .Select(t => DateTime.Today.Add(t).ToString("hh:mm tt", new CultureInfo("en-US")))
                                             .ToList();

                        agendaDiaria.Add(new
                        {
                            usuario = u.id,
                            nombreUsuario = u.nombre,
                            horasOcu = listaOrdenada
                        });
                    }
                }

                return Ok(new
                {
                    exito = true,
                    datos = agendaDiaria
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
