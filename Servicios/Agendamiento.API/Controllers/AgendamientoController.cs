using Agendamiento.API.Servicios;
using Compartido;
using Compartido.Constantes;
using Compartido.Data;
using Compartido.Data.Modelos;
using Compartido.DTOs;
using Compartido.Metodos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Globalization;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Model;

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
        public async Task<IActionResult> CrearCitaNormal([FromBody] CitaDTO dto)
        {
            string nombreBase = string.Empty;
            string logo = string.Empty;
            string nombreEmpresa = string.Empty;
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
                    var EnvioEmailCitaUsuario = bool.TryParse(ContextBD.Parametros.FirstOrDefault(p => p.tipo == "EnvioEmailCitaUsuario")?.valor, out bool resulUsuario) && resulUsuario;
                    var EnvioEmailCitaCliente = bool.TryParse(ContextBD.Parametros.FirstOrDefault(p => p.tipo == "EnvioEmailCitaCliente")?.valor, out bool resulCliente) && resulCliente;

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

                        var infoRecurso = GetInfoRecurso(dto.recurso, ContextBD);

                        if (dto.UsuarioCreacion != "Usuario" && EnvioEmailCitaUsuario)
                        {
                            if (!string.IsNullOrEmpty(infoRecurso.Correo))
                            {
                                await emailService.SendEmailAsync(
                                    infoRecurso.Correo,
                                    "Confirmación de cita",
                                    agendaService.mensajeEmailUsuario(nombreCliente, telefonoCliente, infoRecurso.Nombre, dto.FechaIni, logo),
                                    isHtml: true
                                );
                            }
                        }

                        if (!string.IsNullOrEmpty(emailCliente) && EnvioEmailCitaCliente)
                        {
                            await emailService.SendEmailAsync(
                                emailCliente,
                                "Confirmación de cita",
                                agendaService.mensajeEmailCliente(nombreCliente, dto.FechaIni, nombreEmpresa, infoRecurso.NombreSede, logo),
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

        [HttpPost("guardarCita")]
        public async Task<IActionResult> CrearCitaServicio([FromBody] PadreDTO dto)
        {
            string nombreBase = string.Empty;
            string logo = string.Empty;
            string nombreEmpresa = string.Empty;
            int idCliente = 0;
            string emailCliente = "";
            string nombreCliente = "";
            string telefonoCliente = "";
            bool pago = true;
            bool validacion = true;

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
                    var EnvioEmailCitaUsuario = bool.TryParse(ContextBD.Parametros.FirstOrDefault(p => p.tipo == "EnvioEmailCitaUsuario")?.valor, out bool resulUsuario) && resulUsuario;
                    var EnvioEmailCitaCliente = bool.TryParse(ContextBD.Parametros.FirstOrDefault(p => p.tipo == "EnvioEmailCitaCliente")?.valor, out bool resulCliente) && resulCliente;

                    if (dto.validaHorario)
                        validacion = agendaService.ValidarDisponibilidad(ContextBD, dto.Citas);

                    if (validacion)
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
                                cliente = dto.clienteInfo != null ? idCliente : null,
                                valor = detalleCita.Valor,
                                estado = empresa.servicio == true ? Estados.Activo : dto.UsuarioCreacion == "Usuario" ? Estados.Asistio : Estados.Activo,
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

                        var infoRecurso = GetInfoRecurso(8, ContextBD);

                        if (dto.UsuarioCreacion != "Usuario" && EnvioEmailCitaUsuario)
                        {
                            if (!string.IsNullOrEmpty(infoRecurso.Correo))
                            {
                                await emailService.SendEmailAsync(
                                    infoRecurso.Correo,
                                    "Confirmación de cita",
                                    agendaService.mensajeEmailUsuario(nombreCliente, telefonoCliente, infoRecurso.Nombre, dto.FechaIni, logo),
                                    isHtml: true
                                );
                            }
                        }

                        if (!string.IsNullOrEmpty(emailCliente) && EnvioEmailCitaCliente)
                        {
                            await emailService.SendEmailAsync(
                                emailCliente,
                                "Confirmación de cita",
                                agendaService.mensajeEmailCliente(nombreCliente, dto.FechaIni, nombreEmpresa, infoRecurso.NombreSede, logo),
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
                            mensaje = "Los servicios seleccionados superan el tiempo disponible del usuario.",
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

        [HttpPost("almacenarCitaOrdenNo")]
        public async Task<IActionResult> CrearCitaOrdenesNo([FromBody] CitaDTO dto)
        {
            string nombreBase = string.Empty;
            string logo = string.Empty;
            string nombreEmpresa = string.Empty;
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
                    var EnvioEmailCitaUsuario = bool.TryParse(ContextBD.Parametros.FirstOrDefault(p => p.tipo == "EnvioEmailCitaUsuario")?.valor, out bool resulUsuario) && resulUsuario;
                    var EnvioEmailCitaCliente = bool.TryParse(ContextBD.Parametros.FirstOrDefault(p => p.tipo == "EnvioEmailCitaCliente")?.valor, out bool resulCliente) && resulCliente;

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

                        var nuevaOrden = new Ordene
                        {
                            nombre = nombreCliente,
                            estado = Estados.Pendiente,
                            cliente = idCliente,
                            observaciones = null,
                            usuarioCreacion = dto.UsuarioCreacion,
                            fechaCreacion = dto.FechaCreacion
                        };

                        ContextBD.Ordenes.Add(nuevaOrden);
                        ContextBD.SaveChanges();

                        int idOrden = nuevaOrden.id;

                        var nuevo = new Cita
                        {
                            fechaInicio = dto.FechaIni,
                            hora = dto.Hora,
                            fechaFin = dto.FechaIni.AddMinutes(dto.Hora),
                            recurso = dto.recurso,
                            cliente = dto.clienteInfo != null ? idCliente : null,
                            valor = dto.Valor,
                            estado = Estados.Activo,
                            pago = pago,
                            bono = dto.UsuarioCreacion == "Usuario" ? dto.Bono : null,
                            liquidado = false,
                            fechaCreacion = dto.FechaCreacion,
                            usuarioCreacion = dto.UsuarioCreacion,
                            orden = idOrden
                        };

                        ContextBD.Citas.Add(nuevo);
                        ContextBD.SaveChanges();
                        
                        int idCita = nuevo.id;

                        foreach (var detalle in dto.servicios)
                        {
                            var nuevoServicio = new CitaServicio
                            {
                                cita = nuevo.id,
                                servicio = detalle.Servicio
                            };

                            ContextBD.CitaServicios.Add(nuevoServicio);

                            for (int i = 1; i <= detalle.cantidad; i++)
                            {
                                var nuevaSesion = new Sesione
                                {
                                    numero = i,
                                    orden = idOrden,
                                    estado = i == 1 ? Estados.Agendada : Estados.Pendiente,
                                    cita = i == 1 ? idCita : null
                                };

                                ContextBD.Sesiones.Add(nuevaSesion);
                            }

                            ContextBD.SaveChanges();
                        }

                        transaction.Commit();

                        var infoRecurso = GetInfoRecurso(dto.recurso, ContextBD);

                        if (dto.UsuarioCreacion != "Usuario" && EnvioEmailCitaUsuario)
                        {
                            if (!string.IsNullOrEmpty(infoRecurso.Correo))
                            {
                                await emailService.SendEmailAsync(
                                    infoRecurso.Correo,
                                    "Confirmación de cita",
                                    agendaService.mensajeEmailUsuario(nombreCliente, telefonoCliente, infoRecurso.Nombre, dto.FechaIni, logo),
                                    isHtml: true
                                );
                            }
                        }

                        if (!string.IsNullOrEmpty(emailCliente) && EnvioEmailCitaCliente)
                        {
                            await emailService.SendEmailAsync(
                                emailCliente,
                                "Confirmación de cita",
                                agendaService.mensajeEmailCliente(nombreCliente, dto.FechaIni, nombreEmpresa, infoRecurso.NombreSede, logo),
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

        [HttpPost("almacenarCitaOrdenSi")]
        public async Task<IActionResult> CrearCitaOrdenesSi([FromBody] CitaDTO dto)
        {
            string nombreBase = string.Empty;
            string logo = string.Empty;
            string nombreEmpresa = string.Empty;
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
                    //var EnvioEmailCitaUsuario = bool.TryParse(ContextBD.Parametros.FirstOrDefault(p => p.tipo == "EnvioEmailCitaUsuario")?.valor, out bool resulUsuario) && resulUsuario;
                    var EnvioEmailCitaCliente = bool.TryParse(ContextBD.Parametros.FirstOrDefault(p => p.tipo == "EnvioEmailCitaCliente")?.valor, out bool resulCliente) && resulCliente;

                    var validaHora = ContextBD.Citas.Where(c => c.fechaInicio == dto.FechaIni
                                                           && c.recurso == dto.recurso
                                                           && (c.estado == Estados.Activo || c.estado == Estados.Asistio || c.estado == Estados.Inasistencia)
                                                           ).FirstOrDefault();

                    if (validaHora == null)
                    {
                        if (dto.clienteInfo != null)
                        {
                            var user = ContextBD.Clientes
                                .FirstOrDefault(e => e.id == dto.clienteInfo.Id);

                            if (user != null)
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
                            estado = Estados.Activo,
                            pago = pago,
                            bono = dto.UsuarioCreacion == "Usuario" ? dto.Bono : null,
                            liquidado = false,
                            fechaCreacion = dto.FechaCreacion,
                            usuarioCreacion = dto.UsuarioCreacion,
                            orden = dto.orden.Id
                        };

                        ContextBD.Citas.Add(nuevo);
                        ContextBD.SaveChanges();

                        int idCita = nuevo.id;

                        var sesion = ContextBD.Sesiones
                                .FirstOrDefault(e => e.id == dto.Sesion);

                        if (sesion != null)
                        {
                            sesion.cita = idCita;
                            sesion.estado = Estados.Agendada;

                            ContextBD.SaveChanges();
                        }

                        transaction.Commit();

                        var infoRecurso = GetInfoRecurso(dto.recurso, ContextBD);

                        //if (dto.UsuarioCreacion != "Usuario" && EnvioEmailCitaUsuario)
                        //{
                        //    if (!string.IsNullOrEmpty(infoRecurso.Correo))
                        //    {
                        //        await emailService.SendEmailAsync(
                        //            infoRecurso.Correo,
                        //            "Confirmación de cita",
                        //            agendaService.mensajeEmailUsuario(nombreCliente, telefonoCliente, infoRecurso.Nombre, dto.FechaIni, logo),
                        //            isHtml: true
                        //        );
                        //    }
                        //}

                        if (!string.IsNullOrEmpty(emailCliente) && EnvioEmailCitaCliente)
                        {
                            await emailService.SendEmailAsync(
                                emailCliente,
                                "Confirmación de cita",
                                agendaService.mensajeEmailCliente(nombreCliente, dto.FechaIni, nombreEmpresa, infoRecurso.NombreSede, logo),
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

                if (empresa.id == 8)
                    dto.recurso = 8;

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
                    var recursos = empresa.servicio == true
                                    ? dto.profesional.Split(',')
                                             .Select(x => int.Parse(x.Trim()))
                                             .ToList()
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

                var recursos = empresa.servicio == true
                                ? dto.profesional.Split(',')
                                            .Select(x => int.Parse(x.Trim()))
                                            .ToList()
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
            IEnumerable<object> lista;
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

                if (empresa.servicio == true)
                {
                    lista = ContextBD.Padres
                        .Where(p => p.CitaPadres.Any(cp =>
                            cp.citaNavigation.fechaInicio >= dto.FechaIni &&
                            cp.citaNavigation.fechaFin <= dto.FechaFin &&
                            (cp.citaNavigation.estado == Estados.Activo ||
                             cp.citaNavigation.estado == Estados.Asistio)))
                        .Select(p => new
                        {
                            Padre = p.id,

                            Citas = p.CitaPadres
                                .Where(cp =>
                                    cp.citaNavigation.fechaInicio >= dto.FechaIni &&
                                    cp.citaNavigation.fechaFin <= dto.FechaFin &&
                                    (cp.citaNavigation.estado == Estados.Activo ||
                                     cp.citaNavigation.estado == Estados.Asistio))
                                .Select(cp => new
                                {
                                    cp.citaNavigation.id,
                                    cp.citaNavigation.fechaInicio,
                                    cp.citaNavigation.estado,
                                    cp.citaNavigation.recurso,

                                    NombreRecurso = ContextBD.Usuarios
                                            .Where(u => u.id == cp.citaNavigation.recurso)
                                            .Select(u => u.nombre)
                                            .FirstOrDefault(),

                                    Cliente = ContextBD.Clientes
                                            .Where(cl => cl.id == cp.citaNavigation.cliente)
                                            .Select(cl => new
                                            {
                                                NombreCliente = cl.nombre,
                                                TelefonoCliente = cl.telefono
                                            })
                                            .FirstOrDefault(),

                                    Servicios = cp.citaNavigation.CitaServicios
                                        .Select(cs => new
                                        {
                                            Id = cs.servicioNavigation.id,
                                            Nombre = cs.servicioNavigation.nombre,
                                            Valor = cs.servicioNavigation.nombre == "Otro"
                                                ? cp.citaNavigation.valor
                                                : cs.servicioNavigation.valor
                                        }).ToList()
                                }).ToList()
                        })
                        .OrderBy(o => o.Padre)
                        .ToList();
                }
                else
                {
                    lista = ContextBD.Citas
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
                }

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

                if (empresa.servicio == true)
                {
                    var citas = (from cp in ContextBD.CitaPadres
                                 join c in ContextBD.Citas on cp.cita equals c.id
                                 where cp.padre == dto.Id
                                       && c.estado == Estados.Activo
                                 select c
                                ).ToList();

                    foreach (var cita in citas)
                    {
                        cita.fechaCancelacion = dto.FechaCancelacion;
                        cita.usuarioCancelacion = dto.UsuarioCancelacion;
                        cita.estado = Estados.Cancelada;
                    }
                }
                else
                {
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

                    if (empresa.ordenes == true)
                    {
                        var sesion = ContextBD.Sesiones
                                    .FirstOrDefault(s => s.cita == citaId.id);

                        if (sesion != null)
                        {
                            sesion.cita = null;
                            sesion.estado = Estados.Pendiente;
                        }
                    }

                }

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

                if (empresa.servicio == true)
                {
                    var citas = (from cp in ContextBD.CitaPadres
                                 join c in ContextBD.Citas on cp.cita equals c.id
                                 where cp.padre == dto.Id
                                       && c.estado == Estados.Activo
                                 select c
                                ).ToList();

                    foreach (var cita in citas)
                    {
                        if (dto.Asistencia)
                        {
                            var servicios = (
                            from cs in ContextBD.CitaServicios
                            join s in ContextBD.Servicios on cs.servicio equals s.id
                            join sr in ContextBD.ServiciosRecursos on s.id equals sr.servicio
                            join sp in ContextBD.ServiciosParametros on s.id equals sp.servicio
                            join u in ContextBD.Usuarios on sr.recurso equals u.id
                            join se in ContextBD.Sedes on u.sede equals se.id
                            where cs.cita == cita.id
                            select new
                            {
                                ServicioValor = s.valor == 0 ? (cita.valor ?? 0) : (s.valor ?? 0),
                                Porcentaje = (sp.porcentaje ?? 0),
                                Sede = se.Nombre
                            }
                            ).ToList();

                            foreach (var servicio in servicios)
                            {
                                decimal valorTrabajador = servicio.ServicioValor * (servicio.Porcentaje / 100m);

                                decimal valorEmpresa = servicio.ServicioValor - valorTrabajador;

                                var liquidacion = new LiquidacionCitum
                                {
                                    cita = cita.id,
                                    valorServicio = servicio.ServicioValor,
                                    valorBarbero = valorTrabajador,
                                    valorBarberia = valorEmpresa,
                                    sede = servicio.Sede,
                                    fechaCita = cita.fechaInicio,
                                    bono = cita.bono,
                                    fechaCreacion = dto.FechaCreacion,
                                    usuarioCreacion = dto.UsuarioCreacion,
                                    tipo = "Normal",
                                    numero = 1
                                };

                                ContextBD.LiquidacionCita.Add(liquidacion);
                            }

                            cita.liquidado = true;
                            cita.estado = Estados.Asistio;
                        }
                        else
                            cita.estado = Estados.Inasistencia;
                    }
                    ContextBD.SaveChanges();
                }
                else
                {
                    if (empresa.ordenes == true)
                    {
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

                            var sesion = ContextBD.Sesiones.FirstOrDefault(s => s.cita == citaId.id);

                            if (sesion != null)
                            {
                                sesion.estado = Estados.Asistio;

                                var sesionesOrden = ContextBD.Sesiones
                                    .Where(s => s.orden == sesion.orden)
                                    .ToList();

                                bool todasFinalizadas = sesionesOrden.Any() &&
                                                        sesionesOrden.All(s => s.estado == Estados.Asistio);

                                if (todasFinalizadas)
                                {
                                    var orden = ContextBD.Ordenes
                                        .FirstOrDefault(o => o.id == sesion.orden);

                                    if (orden != null)
                                    {
                                        orden.estado = Estados.Completada;
                                    }
                                }

                                bool yaTieneSesionAsistida = ContextBD.Sesiones
                                                    .Any(s => s.orden == sesion.orden
                                                           && s.estado == Estados.Asistio
                                                           && s.id != sesion.id);

                                if (!yaTieneSesionAsistida)
                                {
                                    var servicios = (from cs in ContextBD.CitaServicios
                                                     join s in ContextBD.Servicios on cs.servicio equals s.id
                                                     join u in ContextBD.Usuarios on citaId.recurso equals u.id
                                                     join se in ContextBD.Sedes on u.sede equals se.id
                                                     where cs.cita == citaId.id
                                                     select new
                                                     {
                                                         ServicioValor = s.valor,
                                                         Porcentaje = (u.porcentaje ?? 0),
                                                         Sede = se.Nombre
                                                     }
                                        ).ToList();

                                    foreach (var servicio in servicios)
                                    {
                                        decimal valorServicio = servicio.ServicioValor ?? 0m;

                                        decimal valorTrabajador = valorServicio * (servicio.Porcentaje / 100m);

                                        decimal valorEmpresa = valorServicio - valorTrabajador;

                                        var liquidacion = new LiquidacionCitum
                                        {
                                            cita = citaId.id,
                                            valorServicio = servicio.ServicioValor,
                                            valorBarbero = valorTrabajador,
                                            valorBarberia = valorEmpresa,
                                            sede = servicio.Sede,
                                            fechaCita = citaId.fechaInicio,
                                            bono = citaId.bono,
                                            fechaCreacion = dto.FechaCreacion,
                                            usuarioCreacion = dto.UsuarioCreacion,
                                            tipo = "Normal",
                                            numero = 1
                                        };

                                        ContextBD.LiquidacionCita.Add(liquidacion);
                                    }
                                }
                            }
                        }
                        else
                        {
                            citaId.estado = Estados.Inasistencia;

                            var sesion = ContextBD.Sesiones
                                .FirstOrDefault(s => s.cita == citaId.id);

                            if (sesion != null)
                            {
                                sesion.cita = null;
                            }
                        }

                        ContextBD.SaveChanges();
                    }
                    else
                    {
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

                            if (empresa.id != 2)
                            {
                                var servicios = (
                                from cs in ContextBD.CitaServicios
                                join s in ContextBD.Servicios on cs.servicio equals s.id
                                join u in ContextBD.Usuarios on citaId.recurso equals u.id
                                join se in ContextBD.Sedes on u.sede equals se.id
                                where cs.cita == citaId.id
                                select new
                                {
                                    ServicioValor = s.valor,
                                    Porcentaje = (u.porcentaje ?? 0),
                                    Sede = se.Nombre
                                }
                                ).ToList();

                                    foreach (var servicio in servicios)
                                    {
                                        decimal valorServicio = servicio.ServicioValor ?? 0m;

                                        decimal valorTrabajador = valorServicio * (servicio.Porcentaje / 100m);

                                        decimal valorEmpresa = valorServicio - valorTrabajador;

                                        var liquidacion = new LiquidacionCitum
                                        {
                                            cita = citaId.id,
                                            valorServicio = servicio.ServicioValor,
                                            valorBarbero = valorTrabajador,
                                            valorBarberia = valorEmpresa,
                                            sede = servicio.Sede,
                                            fechaCita = citaId.fechaInicio,
                                            bono = citaId.bono,
                                            fechaCreacion = dto.FechaCreacion,
                                            usuarioCreacion = dto.UsuarioCreacion,
                                            tipo = "Normal",
                                            numero = 1
                                        };

                                        ContextBD.LiquidacionCita.Add(liquidacion);
                                    }
                            }
                        }
                        else
                            citaId.estado = Estados.Inasistencia;

                        ContextBD.SaveChanges();
                    }
                }

                return Ok(new
                {
                    mensaje = "Cita confirmada exitosamente",
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

        [HttpPut("editarCita")]
        public async Task<IActionResult> EditarCita([FromBody] PadreDTO dto)
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
            bool cliente = true;
            bool validacion = true;

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
                    var cita = ContextBD.Padres.Where(p => p.id == dto.Id).FirstOrDefault();

                    if (cita != null)
                    {
                        if (dto.clienteInfo != null)
                        {
                            var user = ContextBD.Clientes
                                .FirstOrDefault(e => e.telefono == dto.clienteInfo.Telefono);

                            if (user == null)
                            {
                                cliente = false;
                            }
                            else
                            {
                                idCliente = user.id;
                                nombreCliente = user.nombre;
                                emailCliente = user.correo;
                                telefonoCliente = user.telefono;
                            }
                        }

                        if (cliente)
                        {

                            if (dto.validaHorario)
                                validacion = agendaService.ValidarDisponibilidad(ContextBD, dto.Citas);

                            if (validacion)
                            {
                                cita.fechaCita = dto.FechaIni;

                                var idsCitas = ContextBD.CitaPadres
                                    .Where(cp => cp.padre == cita.id)
                                    .Select(cp => cp.cita)
                                    .ToList();

                                var servicios = ContextBD.CitaServicios
                                    .Where(cs => idsCitas.Contains(cs.cita));

                                ContextBD.CitaServicios.RemoveRange(servicios);

                                var relaciones = ContextBD.CitaPadres
                                    .Where(cp => cp.padre == cita.id);

                                ContextBD.CitaPadres.RemoveRange(relaciones);

                                var citasEliminar = ContextBD.Citas
                                    .Where(c => idsCitas.Contains(c.id));

                                ContextBD.Citas.RemoveRange(citasEliminar);

                                ContextBD.SaveChanges();

                                foreach (var detalleCita in dto.Citas)
                                {
                                    var nuevaCita = new Cita
                                    {
                                        fechaInicio = detalleCita.FechaIni,
                                        hora = detalleCita.Hora,
                                        fechaFin = detalleCita.FechaIni.AddMinutes(detalleCita.Hora),
                                        recurso = detalleCita.recurso,
                                        cliente = idCliente,
                                        valor = detalleCita.Valor,
                                        estado = empresa.servicio == true ? Estados.Activo : dto.UsuarioCreacion == "Usuario" ? Estados.Asistio : Estados.Activo,
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
                                        ContextBD.CitaServicios.Add(new CitaServicio
                                        {
                                            cita = nuevaCita.id,
                                            servicio = detalleServicio.Servicio
                                        });
                                    }

                                    ContextBD.CitaPadres.Add(new CitaPadre
                                    {
                                        cita = nuevaCita.id,
                                        padre = cita.id
                                    });
                                }

                                ContextBD.SaveChanges();
                                transaction.Commit();

                                //if (dto.UsuarioCreacion != "Usuario")
                                //{
                                //    var infoRecurso = ContextBD.Usuarios
                                //                        .Include(u => u.sedeNavigation)
                                //                        .Where(e => e.estado == Estados.Activo
                                //                            && e.recurso == true
                                //                            && e.id == 8)
                                //                        .FirstOrDefault();

                                //    if (infoRecurso != null)
                                //    {
                                //        nombreSede = infoRecurso.sedeNavigation?.Nombre;

                                //        if (!string.IsNullOrEmpty(infoRecurso.correo))
                                //        {
                                //            await emailService.SendEmailAsync(
                                //                infoRecurso.correo,
                                //                "Confirmación de cita",
                                //                agendaService.mensajeEmailUsuario(nombreCliente, telefonoCliente, infoRecurso.nombre, dto.FechaIni, logo),
                                //                isHtml: true
                                //            );
                                //        }
                                //    }
                                //}

                                return Ok(new
                                {
                                    mensaje = "Cita editada exitosamente",
                                    exito = true
                                });
                            }
                            else
                            {
                                return Ok(new
                                {
                                    mensaje = "Los servicios seleccionados superan el tiempo disponible del usuario.",
                                    exito = false
                                });
                            }
                        }
                        else
                        {
                            return Ok(new
                            {
                                mensaje = "Cliente no encontrado",
                                exito = false
                            });
                        }
                    }
                    else
                    {
                        return Ok(new
                        {
                            mensaje = "Cita no encontrada",
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

        [HttpGet("consultarAgendaCitas")]
        public IActionResult consultarAgendaCitas([FromQuery] CitaDTO dto)
        {
            string nombreBase = string.Empty;
            IEnumerable<object> lista;
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

                lista = ContextBD.Citas
                .Where(e => e.fechaInicio >= dto.FechaIni
                            && e.fechaFin <= dto.FechaFin
                            && (e.estado == Estados.Activo || e.estado == Estados.Asistio))
                .Select(c => new
                {
                    c.id,
                    c.fechaInicio,
                    c.estado,

                    Recurso = ContextBD.Usuarios
                        .Where(u => u.id == c.recurso)
                        .Select(u => new
                        {
                            u.id,
                            u.nombre,
                            SedeId = u.sede,
                            SedeNombre = u.sedeNavigation.Nombre
                        })
                        .FirstOrDefault(),

                    Cliente = ContextBD.Clientes
                        .Where(cl => cl.id == c.cliente)
                        .Select(cl => new
                        {
                            Nombre = cl.nombre,
                            Telefono = cl.telefono
                        })
                        .FirstOrDefault(),

                    Servicios = c.CitaServicios
                        .Select(cs => new
                        {
                            Nombre = cs.servicioNavigation.nombre,
                            Valor = cs.servicioNavigation.nombre == "Otro"
                                ? c.valor
                                : cs.servicioNavigation.valor
                        })
                        .ToList()
                })
                .AsEnumerable()
                .Where(x => x.Recurso != null)
                .GroupBy(x => new
                {
                    x.Recurso.SedeId,
                    x.Recurso.SedeNombre
                })
                .Select(sede => new
                {
                    id = sede.Key.SedeId,
                    nombre = sede.Key.SedeNombre,

                    recursos = sede
                        .GroupBy(r => new
                        {
                            r.Recurso.id,
                            r.Recurso.nombre
                        })
                        .OrderBy(r => r.Key.nombre)
                        .Select(recurso => new
                        {
                            id = recurso.Key.id,
                            nombre = recurso.Key.nombre,

                            citas = recurso
                                .OrderBy(c => c.fechaInicio)
                                .Select(c => new
                                {
                                    id = c.id,
                                    hora = c.fechaInicio.ToString("HH:mm"),
                                    cliente = c.Cliente?.Nombre,
                                    telefono = c.Cliente?.Telefono,

                                    servicio = string.Join(" + ",
                                        c.Servicios
                                            .Select(s => s.Nombre)
                                            .Distinct()),

                                    estado = c.estado.ToString()
                                })
                                .ToList()
                        })
                        .ToList()
                })
                .OrderBy(s => s.nombre)
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

        [HttpPost("eliminarBloqueo")]
        public IActionResult EliminarBloqueo([FromBody] BloqueoDTO dto)
        {
            string nombreBase = string.Empty;

            try
            {
                var empresa = ContextGene.Empresas
                    .FirstOrDefault(e => e.id == dto.empresa && e.estado == Estados.Activo);

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

                var connectionString = string.Format(
                    connectionTemplate,
                    empresa.conexionBD
                );

                using var ContextBD = ContextFact.Create(connectionString);

                var bloqueo = ContextBD.BloqueosHorarios
                              .FirstOrDefault(b => b.id == dto.Id);

                if (bloqueo == null)
                {
                    return Ok(new
                    {
                        mensaje = "El bloqueo no existe o no pertenece al usuario",
                        exito = false
                    });
                }

                ContextBD.BloqueosHorarios.Remove(bloqueo);

                ContextBD.SaveChanges();

                return Ok(new
                {
                    mensaje = "Bloqueo eliminado correctamente",
                    exito = true
                });
            }
            catch (Exception ex)
            {
                LogsCatch.GuardarError(
                    ex,
                    configuracion,
                    ContextFact,
                    nombreBase
                );

                return StatusCode(500, new
                {
                    mensaje = "Ocurrió un error interno",
                    exito = false
                });
            }
        }

        [HttpGet("ConsultarOrdenes")]
        public async Task<IActionResult> GestionarOrdenes(int empresa, int cliente)
        {
            string nombreBase = string.Empty;

            try
            {
                var empresaBD = ContextGene.Empresas
                    .FirstOrDefault(e => e.id == empresa && e.estado == Estados.Activo);

                if (empresaBD == null)
                {
                    return Ok(new
                    {
                        mensaje = "Empresa no encontrada o inactiva",
                        exito = false
                    });
                }

                var resultado = ValidacionEmpresa.ValidarEmpresa(empresaBD);

                if (!resultado.Exito)
                {
                    return Ok(new
                    {
                        mensaje = resultado.Mensaje,
                        exito = false
                    });
                }

                nombreBase = empresaBD.conexionBD;

                var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
                var connectionString = string.Format(connectionTemplate, nombreBase);

                using var ContextBD = ContextFact.Create(connectionString);

                var ordenes = await ContextBD.Ordenes
                    .AsNoTracking()
                    .Where(o => o.cliente == cliente)
                    .Select(o => new OrdenDTO
                    {
                        Id = o.id,
                        Nombre = o.nombre,
                        Estado = o.estado,
                        Observaciones = o.observaciones,
                        UsuarioCreacion = o.usuarioCreacion,
                        FechaCreacion = o.fechaCreacion,
                        NombreServicio = (from c in ContextBD.Citas 
                                          join cs in ContextBD.CitaServicios on c.id equals cs.cita 
                                          join servicio in ContextBD.Servicios on cs.servicio equals servicio.id 
                                          where c.orden == o.id select servicio.nombre).FirstOrDefault(),
                        idServicio = (from c in ContextBD.Citas
                                          join cs in ContextBD.CitaServicios on c.id equals cs.cita
                                          join servicio in ContextBD.Servicios on cs.servicio equals servicio.id
                                          where c.orden == o.id
                                          select servicio.id).FirstOrDefault(),
                        sesiones = ContextBD.Sesiones
                            .Where(s => s.orden == o.id)
                            .Select(s => new SesionesDTO
                            {
                                Id = s.id,
                                Numero = s.numero,
                                Orden = s.orden,
                                Estado = s.estado,

                                FechaCita = s.cita != null
                                    ? ContextBD.Citas
                                        .Where(c => c.id == s.cita)
                                        .Select(c => c.fechaInicio)
                                        .FirstOrDefault()
                                    : null
                            })
                            .ToList()
                    })
                    .ToListAsync();

                return Ok(new
                {
                    mensaje = "Órdenes consultadas exitosamente",
                    exito = true,
                    data = ordenes
                });
            }
            catch (Exception ex)
            {
                LogsCatch.GuardarError(
                    ex,
                    configuracion,
                    ContextFact,
                    nombreBase
                );

                return StatusCode(500, new
                {
                    mensaje = "Ocurrió un error interno",
                    exito = false
                });
            }
        }

        private UsuarioDTO GetInfoRecurso(int recurso, ModeloContext ContextBD)
        {
            var infoRecurso = ContextBD.Usuarios
                                .Include(u => u.sedeNavigation)
                                .Where(e => e.estado == Estados.Activo
                                    && e.recurso == true
                                    && e.id == recurso)
                                .FirstOrDefault();

            if (infoRecurso != null)
            {
                return new UsuarioDTO
                {
                    Nombre = infoRecurso.nombre,
                    NombreSede = infoRecurso.sedeNavigation?.Nombre,
                    Correo = infoRecurso.correo
                };
            }
            else
            {
                var Recurso = ContextBD.Recursos
                                .Include(u => u.sedeNavigation)
                                .Where(e => e.estado == Estados.Activo
                                    && e.id == recurso)
                                .FirstOrDefault();
                if (Recurso != null)
                {
                    return new UsuarioDTO
                    {
                        NombreSede = Recurso.sedeNavigation?.Nombre
                    };
                }
            }

            return null;
        }
    }
}
