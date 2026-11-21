using Agendamiento.API.Servicios;
using Compartido;
using Compartido.Constantes;
using Compartido.Data;
using Compartido.Data.Modelos;
using Compartido.DTOs;
using Compartido.Metodos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.Internal;
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
            bool pago = true;
            string fidelidadMensaje = string.Empty;
            DateTime fechaNacimientoCliente = new DateTime();
            bool esCumpleanos = false;
            bool esDiaDescuento = false;
            decimal valorFinal = dto.Valor ?? 0m;
            bool descuento = false;
            decimal porcentajeDescuentoAplicar = 0m;
            string mensajeDescuento = string.Empty;
            string tipoDescuento = string.Empty;
            bool cualDescuento = false;

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
                    var fidelidad = bool.TryParse(ContextBD.Parametros.FirstOrDefault(p => p.tipo == "ManejaFidelidad")?.valor, out bool resulFidelidad) && resulFidelidad;
                    var promoCumple = bool.TryParse(ContextBD.Parametros.FirstOrDefault(p => p.tipo == "DescuentoCumpleanos")?.valor, out bool resulPromoCumple) && resulPromoCumple;
                    var promoDiaSemana = bool.TryParse(ContextBD.Parametros.FirstOrDefault(p => p.tipo == "PromocionDiaSemana")?.valor, out bool resulPromoSemana) && resulPromoSemana;
                    int promoDia = agendaService.GetInt(ContextBD.Parametros.FirstOrDefault(p => p.tipo == "DiaSemanaPromocion"));
                    decimal porcentajeCumple = agendaService.GetDecimal(ContextBD.Parametros.FirstOrDefault(p => p.tipo == "PorcentajeDescuentoCumpleanos"));
                    decimal porcentajeDiaSemana = agendaService.GetDecimal(ContextBD.Parametros.FirstOrDefault(p => p.tipo == "PorcentajeDiaSemana"));

                    if (dto.seleccionoCualquiera)
                    {
                        // =====================================================
                        // 1. ROTACIÓN AUTOMÁTICA DE RECURSOS
                        // =====================================================

                        DateTime ahora = DateTime.Now;
                        DateTime fechaRotacion = DateTime.Today;


                        var zonaColombia = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "SA Pacific Standard Time" : "America/Bogota");

                        ahora = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zonaColombia);

                        fechaRotacion = ahora.Date;

                        // Siempre comenzar en el siguiente intervalo de 15 minutos.
                        int minutosActuales = ahora.Hour * 60 + ahora.Minute;

                        int siguienteIntervalo = ((minutosActuales / 15) + 1) * 15;

                        DateTime horaMinima = ahora.Date.AddMinutes(siguienteIntervalo);

                        // -------------------------------------------------
                        // 1.1. Bloquear la rotación para esta sede y fecha.
                        // Evita que dos solicitudes automáticas consulten
                        // simultáneamente el mismo último recurso.
                        // -------------------------------------------------

                        string recursoBloqueo = $"RotacionAutomatica_Sede_{dto.sede}_{fechaRotacion:yyyyMMdd}";

                        await ContextBD.Database.ExecuteSqlInterpolatedAsync($@"
                            DECLARE @resultado INT;

                            EXEC @resultado = sp_getapplock
                                @Resource = {recursoBloqueo},
                                @LockMode = 'Exclusive',
                                @LockOwner = 'Transaction',
                                @LockTimeout = 15000;

                            IF @resultado < 0
                                THROW 50001,
                                    'No fue posible obtener el bloqueo de rotación.',
                                    1;
                        ");

                        // -------------------------------------------------
                        // 1.2. Consultar los recursos activos de la sede.
                        // -------------------------------------------------

                        var recursos = ContextBD.Usuarios
                            .Where(u =>
                                u.recurso == true &&
                                u.id != 3 &&
                                u.id != 4 &&
                                u.estado == Estados.Activo &&
                                u.sede == dto.sede)
                            .Select(u => u.id)
                            .OrderBy(id => id)
                            .ToList();

                        if (!recursos.Any())
                        {
                            await transaction.RollbackAsync();

                            return Ok(new
                            {
                                mensaje = "No hay recursos disponibles en la sede seleccionada.",
                                exito = false
                            });
                        }

                        // -------------------------------------------------
                        // 1.3. Consultar el último recurso asignado
                        // automáticamente para la sede en el día actual.
                        // -------------------------------------------------

                        var rotacionActual = ContextBD.RotacionRecursoAutomaticas.FirstOrDefault(r => r.sede == dto.sede && r.fecha == fechaRotacion);

                        // Si ya hubo una asignación automática hoy,
                        // comenzar por el recurso siguiente al último usado.
                        if (rotacionActual != null)
                        {
                            int indiceUltimoRecurso = recursos.IndexOf(rotacionActual.ultimoRecurso);

                            if (indiceUltimoRecurso >= 0)
                            {
                                recursos = recursos.Skip(indiceUltimoRecurso + 1).Concat(recursos.Take(indiceUltimoRecurso + 1)).ToList();
                            }
                        }

                        // -------------------------------------------------
                        // 1.4. Consultar los horarios activos de la agenda.
                        // -------------------------------------------------

                        var horarios = ContextBD.Agenda
                            .Where(a => a.estado)
                            .Select(a => a.valor)
                            .Distinct()
                            .OrderBy(h => h)
                            .ToList();

                        if (!horarios.Any())
                        {
                            await transaction.RollbackAsync();

                            return Ok(new
                            {
                                mensaje = "No hay horarios disponibles en la agenda.",
                                exito = false
                            });
                        }

                        // -------------------------------------------------
                        // 1.5. Buscar la primera combinación disponible:
                        // horario más cercano y recurso según la rotación.
                        // -------------------------------------------------

                        bool encontrado = false;

                        for (int dia = 0; dia < 30 && !encontrado; dia++)
                        {
                            DateTime fecha = horaMinima.Date.AddDays(dia);

                            foreach (var horario in horarios)
                            {
                                DateTime inicio = fecha.Add(horario);

                                // No asignar una hora anterior o igual
                                // al siguiente intervalo permitido.
                                if (inicio < horaMinima)
                                    continue;

                                DateTime fin = inicio.AddMinutes(dto.Hora);

                                foreach (var recurso in recursos)
                                {
                                    bool ocupado = ContextBD.Citas.Any(c =>
                                        c.recurso == recurso &&
                                        (
                                            c.estado == Estados.Activo ||
                                            c.estado == Estados.Asistio
                                        ) &&
                                        c.fechaInicio < fin &&
                                        c.fechaFin > inicio);

                                    if (ocupado)
                                        continue;

                                    // Se encontró disponibilidad para
                                    // toda la duración de la cita.
                                    dto.FechaIni = inicio;
                                    dto.recurso = recurso;

                                    encontrado = true;
                                    break;
                                }

                                if (encontrado)
                                    break;
                            }
                        }

                        if (!encontrado)
                        {
                            await transaction.RollbackAsync();

                            return Ok(new
                            {
                                mensaje = "No se encontraron recursos con disponibilidad para toda la duración de la cita durante los próximos 30 días.",
                                exito = false
                            });
                        }


                        // =====================================================
                        // 2. VALIDAR DISPONIBILIDAD PARA LA CITA
                        // =====================================================

                        DateTime fechaInicio = dto.FechaIni;
                        DateTime fechaFin = fechaInicio.AddMinutes(dto.Hora);

                        bool citaOcupada = ContextBD.Citas.Any(c =>
                            c.recurso == dto.recurso &&
                            (
                                c.estado == Estados.Activo ||
                                c.estado == Estados.Asistio
                            ) &&
                            c.fechaInicio < fechaFin &&
                            c.fechaFin > fechaInicio);

                        if (citaOcupada)
                        {
                            await transaction.RollbackAsync();

                            return Ok(new
                            {
                                mensaje = "Ya hay una cita agendada en ese horario.",
                                exito = false
                            });
                        }

                        // =====================================================
                        // 3. CONSULTAR O CREAR EL CLIENTE
                        // =====================================================

                        if (dto.clienteInfo != null)
                        {
                            var user = ContextBD.Clientes
                                .FirstOrDefault(e =>
                                    e.telefono == dto.clienteInfo.Telefono);

                            if (user == null)
                            {
                                var nueva = new Cliente
                                {
                                    nombre = dto.clienteInfo.Nombre,
                                    correo = string.IsNullOrWhiteSpace(dto.clienteInfo.Correo) ? "na@na.com" : dto.clienteInfo.Correo.Trim(),
                                    telefono = dto.clienteInfo.Telefono,
                                    fechaNacimiento = dto.clienteInfo.FechaNacimiento,
                                    estado = Estados.Activo,
                                    fechaCreacion = dto.FechaCreacion,
                                    alias = dto.alias,
                                };

                                ContextBD.Clientes.Add(nueva);

                                await ContextBD.SaveChangesAsync();

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

                        // =====================================================
                        // 4. VALIDAR BLOQUEO DE DÍA COMPLETO
                        // =====================================================

                        bool diaCompletoBloqueado = false;

                        int diaSemana = (int)dto.FechaIni.DayOfWeek;

                        if (diaSemana == 0)
                            diaSemana = 7;

                        var bloqueos = ContextBD.BloqueosHorarios
                            .AsEnumerable()
                            .Where(b =>
                                b.usuario == dto.recurso &&
                                b.tipoBloqueo == 3 &&
                                b.estado == Estados.Activo &&
                                b.diasSemana != null &&
                                diaSemana == 7 &&
                                b.diasSemana
                                    .Split(',')
                                    .Select(x => x.Trim())
                                    .Contains("7"))
                            .ToList();

                        foreach (var bloqueo in bloqueos)
                        {
                            var inicioBloqueo = bloqueo.horaInicio;
                            var finBloqueo = bloqueo.horaFin;

                            if (inicioBloqueo == TimeSpan.Zero &&
                                finBloqueo >= TimeSpan.FromHours(23.5))
                            {
                                diaCompletoBloqueado = true;
                                break;
                            }
                        }

                        if (diaCompletoBloqueado)
                        {
                            pago = false;
                        }

                        // =====================================================
                        // 5. CREAR LA CITA
                        // =====================================================

                        var nuevo = new Cita
                        {
                            fechaInicio = dto.FechaIni,
                            hora = dto.Hora,
                            fechaFin = dto.FechaIni.AddMinutes(dto.Hora),
                            recurso = dto.recurso,
                            cliente = dto.clienteInfo != null
                                ? idCliente
                                : null,

                            valor = dto.Valor,

                            estado = dto.UsuarioCreacion == "Usuario"
                                ? Estados.Asistio
                                : Estados.Activo,

                            pago = pago,

                            bono = dto.UsuarioCreacion == "Usuario"
                                ? dto.Bono
                                : null,

                            liquidado = false,
                            fechaCreacion = dto.FechaCreacion,
                            usuarioCreacion = dto.UsuarioCreacion,
                            alias = dto.alias
                        };

                        ContextBD.Citas.Add(nuevo);

                        await ContextBD.SaveChangesAsync();

                        // =====================================================
                        // 6. GUARDAR LOS SERVICIOS DE LA CITA
                        // =====================================================

                        if (dto.servicios != null)
                        {
                            foreach (var detalle in dto.servicios)
                            {
                                var nuevoServicio = new CitaServicio
                                {
                                    cita = nuevo.id,
                                    servicio = detalle.Servicio
                                };

                                ContextBD.CitaServicios.Add(nuevoServicio);
                            }

                            await ContextBD.SaveChangesAsync();
                        }

                        // =====================================================
                        // 7. ACTUALIZAR LA ROTACIÓN
                        // Solo para citas automáticas.
                        // Se guarda dentro de la misma transacción de la cita.
                        // =====================================================

                        if (dto.seleccionoCualquiera)
                        {
                            var rotacion = ContextBD.RotacionRecursoAutomaticas
                                .FirstOrDefault(r =>
                                    r.sede == dto.sede &&
                                    r.fecha == fechaRotacion);

                            if (rotacion == null)
                            {
                                rotacion = new RotacionRecursoAutomatica
                                {
                                    sede = dto.sede,
                                    fecha = fechaRotacion,
                                    ultimoRecurso = dto.recurso,
                                    fechaActualizacion = ahora
                                };

                                ContextBD.RotacionRecursoAutomaticas.Add(rotacion);
                            }
                            else
                            {
                                rotacion.ultimoRecurso = dto.recurso;
                                rotacion.fechaActualizacion = ahora;
                            }

                            await ContextBD.SaveChangesAsync();
                        }

                        // Confirmar tanto la cita como la actualización
                        // de la rotación.
                        await transaction.CommitAsync();

                        // =====================================================
                        // 8. ENVIAR CORREOS DE CONFIRMACIÓN
                        // =====================================================

                        var infoRecurso = agendaService.GetInfoRecurso(dto.recurso, ContextBD);

                        if (dto.UsuarioCreacion != "Usuario" && EnvioEmailCitaUsuario && !string.IsNullOrEmpty(infoRecurso.Correo))
                        {
                            await emailService.SendEmailAsync(
                                infoRecurso.Correo,
                                "Confirmación de cita",
                                agendaService.mensajeEmailUsuario(
                                    nombreCliente,
                                    telefonoCliente,
                                    infoRecurso.Nombre,
                                    dto.FechaIni,
                                    logo),
                                isHtml: true
                            );
                        }

                        if (!string.IsNullOrEmpty(emailCliente) && EnvioEmailCitaCliente)
                        {
                            await emailService.SendEmailAsync(
                                emailCliente,
                                "Confirmación de cita",
                                agendaService.mensajeEmailCliente(
                                    nombreCliente,
                                    dto.FechaIni,
                                    nombreEmpresa,
                                    infoRecurso.NombreSede,
                                    logo),
                                isHtml: true
                            );
                        }

                        // =====================================================
                        // 9. VALIDAR MENSAJE DE FIDELIDAD
                        // =====================================================

                        fidelidadMensaje = agendaService.ObtenerMensajeFidelidad(idCliente, new[] { nuevo.id }, empresa, ContextBD);

                        return Ok(new
                        {
                            mensaje = "Cita registrada exitosamente",
                            exito = true,
                            fidelidadMensaje = fidelidadMensaje
                        });
                    }
                    else
                    {
                        var validaHora = ContextBD.Citas.Where(c => c.fechaInicio == dto.FechaIni
                                                               && c.recurso == dto.recurso
                                                               && (c.estado == Estados.Activo || c.estado == Estados.Asistio)
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
                                        correo = string.IsNullOrWhiteSpace(dto.clienteInfo.Correo) ? "na@na.com" : dto.clienteInfo.Correo.Trim(),
                                        telefono = dto.clienteInfo.Telefono,
                                        fechaNacimiento = dto.clienteInfo.FechaNacimiento,
                                        estado = Estados.Activo,
                                        fechaCreacion = dto.FechaCreacion,
                                        alias = dto.alias
                                    };

                                    ContextBD.Clientes.Add(nueva);
                                    ContextBD.SaveChanges();

                                    idCliente = nueva.id;
                                    nombreCliente = nueva.nombre;
                                    emailCliente = nueva.correo;
                                    telefonoCliente = nueva.telefono;
                                    fechaNacimientoCliente = (DateTime)nueva.fechaNacimiento;
                                }
                                else
                                {
                                    idCliente = user.id;
                                    nombreCliente = user.nombre;
                                    emailCliente = user.correo;
                                    telefonoCliente = user.telefono;
                                    fechaNacimientoCliente = (DateTime)user.fechaNacimiento;
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

                            if (dto.UsuarioCreacion == "Cliente")
                            {
                                if (promoDiaSemana)
                                {
                                    esDiaDescuento = (int)dto.FechaIni.DayOfWeek == promoDia;

                                    if (esDiaDescuento && porcentajeDiaSemana > porcentajeDescuentoAplicar)
                                    {
                                        porcentajeDescuentoAplicar = porcentajeDiaSemana;
                                        tipoDescuento = promoDia switch
                                        {
                                            0 => "Domingo dia de promoción",
                                            1 => "Lunes dia de promoción",
                                            2 => "Martes dia de promoción",
                                            3 => "Miércoles dia de promoción",
                                            4 => "Jueves dia de promoción",
                                            5 => "Viernes dia de promoción",
                                            6 => "Sábado dia de promoción",
                                            _ => "Día no válido"
                                        }; ;
                                        cualDescuento = true;
                                        mensajeDescuento = $"¡Tienes un descuento especial del {porcentajeDiaSemana:0.##}% por la promoción del día!";
                                        //valorFinal = Math.Round(valorFinal * (1 - porcentajeDiaSemana / 100m), 2);
                                        //descuento = true;
                                    }
                                }

                                if (promoCumple && fechaNacimientoCliente != DateTime.MinValue)
                                {
                                    var nacimiento = fechaNacimientoCliente;
                                    var fechaCita = dto.FechaIni.Date;

                                    // Validar cumpleaños y día anterior.
                                    var cumpleanos = new DateTime(fechaCita.Year, nacimiento.Month, Math.Min(nacimiento.Day, DateTime.DaysInMonth(fechaCita.Year, nacimiento.Month)));

                                    esCumpleanos = fechaCita == cumpleanos || fechaCita == cumpleanos.AddDays(-1);

                                    if (esCumpleanos && porcentajeCumple > porcentajeDescuentoAplicar)
                                    {
                                        porcentajeDescuentoAplicar = porcentajeCumple;
                                        tipoDescuento = "Cumpleanos";
                                        cualDescuento = true;
                                        mensajeDescuento = $"¡Feliz cumpleaños! Tienes un descuento especial del {porcentajeCumple:0.##}%.";
                                        //valorFinal = Math.Round(valorFinal * (1 - porcentajeCumple / 100m),2);
                                        //descuento = true;
                                    }
                                }
                            }

                            // Parámetro de fidelidad que aplica cuando la fidelidad es el mayor descuento
                            FidelidadParametro? fidelidadGanadora = null;

                            if (fidelidad)
                            {
                                var fidelidadCliente = ContextBD.Fidelidads.FirstOrDefault(p => p.cliente == idCliente);

                                if (fidelidadCliente != null)
                                {
                                    var siguienteParametro = ContextBD.FidelidadParametros
                                                            .Where(p =>
                                                                p.estado &&
                                                                p.cantidad > fidelidadCliente.cantidad)
                                                            .OrderBy(p => p.cantidad)
                                                            .FirstOrDefault();

                                    if (siguienteParametro != null && siguienteParametro.cantidad == fidelidadCliente.cantidad + 1 && siguienteParametro.porcentaje > porcentajeDescuentoAplicar)
                                    {
                                        cualDescuento = false;
                                        tipoDescuento = string.Empty;
                                        mensajeDescuento = $"¡Felicitaciones! llegaste a tu {siguienteParametro.cantidad} visita. " + $"Tienes un beneficio especial {siguienteParametro.porcentaje:0}% de descuento.";
                                        fidelidadGanadora = siguienteParametro;
                                    }
                                }
                            }

                            if (porcentajeDescuentoAplicar > 0 && cualDescuento)
                            {
                                valorFinal = Math.Round(valorFinal * (1 - porcentajeDescuentoAplicar / 100m),2);
                                descuento = true;
                            }

                            var nuevo = new Cita
                            {
                                fechaInicio = dto.FechaIni,
                                hora = dto.Hora,
                                fechaFin = dto.FechaIni.AddMinutes(dto.Hora),
                                recurso = dto.recurso,
                                cliente = dto.clienteInfo != null ? idCliente : null,
                                valor = valorFinal,
                                estado = dto.UsuarioCreacion == "Usuario" ? Estados.Asistio : Estados.Activo,
                                pago = pago,
                                bono = dto.UsuarioCreacion == "Usuario" ? dto.Bono : null,
                                liquidado = false,
                                fechaCreacion = dto.FechaCreacion,
                                usuarioCreacion = dto.UsuarioCreacion,
                                alias = dto.alias,
                                descuento = descuento,
                                tipoDescuento = tipoDescuento
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

                            // Mensaje del mayor descuento, con el mismo formato de consultarCitasRecurso
                            if (descuento)
                            {
                                // Ganó cumpleaños o día de promoción
                                fidelidadMensaje = $"{nombreCliente} se le aplico un descuento por motivo de {tipoDescuento} ";
                            }
                            else if (fidelidadGanadora != null)
                            {
                                // Ganó la fidelidad (solo sale si la cita quedó activa)
                                fidelidadMensaje = agendaService.ObtenerMensajeFidelidad(idCliente, new[] { nuevo.id }, empresa, ContextBD);
                            }

                            var infoRecurso = agendaService.GetInfoRecurso(dto.recurso, ContextBD);

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
                                exito = true,
                                fidelidadMensaje = fidelidadMensaje,
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
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();

                    LogsCatch.GuardarError(
                        ex,
                        configuracion,
                        ContextFact,
                        nombreBase);

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
            int rec = 0;
            string fidelidadMensaje = string.Empty;

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
                    RespuestaDTO validacion = new RespuestaDTO
                    {
                        Disponible = true,
                        EnvioMensaje = false
                    };

                    var EnvioEmailCitaUsuario = bool.TryParse(ContextBD.Parametros.FirstOrDefault(p => p.tipo == "EnvioEmailCitaUsuario")?.valor, out bool resulUsuario) && resulUsuario;
                    var EnvioEmailCitaCliente = bool.TryParse(ContextBD.Parametros.FirstOrDefault(p => p.tipo == "EnvioEmailCitaCliente")?.valor, out bool resulCliente) && resulCliente;

                    if (dto.validaHorario)
                    {
                        if (empresa.servicio == true)
                            validacion = agendaService.ValidarDisponibilidad(ContextBD, dto.Citas);

                        if (empresa.recursoMultiple == true)
                            validacion = agendaService.ValidarDisponibilidadMultiple(ContextBD, dto.Citas);
                    }

                    if (validacion.Disponible)
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
                                    correo = string.IsNullOrWhiteSpace(dto.clienteInfo.Correo) ? "na@na.com" : dto.clienteInfo.Correo.Trim(),
                                    telefono = dto.clienteInfo.Telefono,
                                    fechaNacimiento = dto.clienteInfo.FechaNacimiento,
                                    estado = Estados.Activo,
                                    fechaCreacion = dto.FechaCreacion,
                                    alias = dto.alias
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

                        int padreNuevo = 0;

                        var nuevoPadre = new Padre
                        {
                            fechaCita = dto.FechaIni
                        };

                        ContextBD.Padres.Add(nuevoPadre);
                        ContextBD.SaveChanges();

                        padreNuevo = nuevoPadre.id;

                        var citasCreadas = new List<int>();

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
                                estado = (empresa.servicio == true || empresa.recursoMultiple == true) ? Estados.Activo : dto.UsuarioCreacion == "Usuario" ? Estados.Asistio : Estados.Activo,
                                pago = pago,
                                bono = dto.UsuarioCreacion == "Usuario" ? detalleCita.Bono : null,
                                liquidado = false,
                                fechaCreacion = dto.FechaCreacion,
                                usuarioCreacion = dto.UsuarioCreacion,
                                alias = dto.alias,
                                comboPromo = dto.promociones
                            };

                            ContextBD.Citas.Add(nuevaCita);
                            ContextBD.SaveChanges();

                            citasCreadas.Add(nuevaCita.id);

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
                                padre = padreNuevo,
                            };

                            ContextBD.CitaPadres.Add(citaPadre);
                        }
                        ContextBD.SaveChanges();
                        transaction.Commit();

                        if (empresa.servicio == true)
                            rec = 8;
                        else
                        {
                            if (empresa.recursoMultiple == true)
                                rec = 7;
                        }

                        var infoRecurso = agendaService.GetInfoRecurso(rec, ContextBD);

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

                        fidelidadMensaje = agendaService.ObtenerMensajeFidelidad(idCliente, citasCreadas, empresa, ContextBD);

                        return Ok(new
                        {
                            mensaje = "Cita registrada exitosamente",
                            envioMensaje = validacion.EnvioMensaje,
                            fidelidadMensaje = fidelidadMensaje,
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
                                    correo = string.IsNullOrWhiteSpace(dto.clienteInfo.Correo) ? "na@na.com" : dto.clienteInfo.Correo.Trim(),
                                    telefono = dto.clienteInfo.Telefono,
                                    fechaNacimiento = dto.clienteInfo.FechaNacimiento,
                                    estado = Estados.Activo,
                                    fechaCreacion = dto.FechaCreacion,
                                    alias = dto.alias
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
                            orden = idOrden,
                            alias = dto.alias
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

                        var infoRecurso = agendaService.GetInfoRecurso(dto.recurso, ContextBD);

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

                        var fidelidadMensaje = agendaService.ObtenerMensajeFidelidad(idCliente, new[] { idCita }, empresa, ContextBD);

                        return Ok(new
                        {
                            mensaje = "Cita registrada exitosamente",
                            exito = true,
                            fidelidadMensaje = fidelidadMensaje
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
                            orden = dto.orden.Id,
                            alias = dto.alias
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

                        var infoRecurso = agendaService.GetInfoRecurso(dto.recurso, ContextBD);

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

                if (tiempo == null || !int.TryParse(tiempo.valor, out int minutosAgenda) || minutosAgenda <= 0)
                {
                    return Ok(new
                    {
                        mensaje = "No está configurado el parámetro Tiempo_Agenda",
                        exito = false
                    });
                }

                // Empresas de servicio / recurso múltiple envían el recurso en "profesional" (uno o varios separados por coma).
                // Si no llega, se usa "recurso" para no fallar con un 500.
                var recursos = (empresa.servicio == true || empresa.recursoMultiple == true) && !string.IsNullOrWhiteSpace(dto.profesional)
                                ? dto.profesional.Split(',')
                                            .Select(x => int.TryParse(x.Trim(), out var id) ? id : 0)
                                            .Where(id => id > 0)
                                            .ToList()
                                : new List<int>();

                // Recurso múltiple usa un solo profesional, como antes; servicio admite varios
                if (empresa.servicio != true && empresa.recursoMultiple == true)
                {
                    recursos = recursos.Take(1).ToList();
                }

                if (!recursos.Any() && dto.recurso > 0)
                {
                    recursos.Add(dto.recurso);
                }

                var listaCitas = ContextBD.Citas
                    .Where(e => e.fechaInicio >= dto.FechaIni
                             && e.fechaFin <= dto.FechaFin
                             && recursos.Contains(e.recurso)
                             && (e.estado == Estados.Activo || e.estado == Estados.Asistio))
                    .ToList();

                foreach (var item in listaCitas)
                {
                    int cantidad = item.hora / minutosAgenda;

                    for (int i = 0; i < cantidad; i++)
                    {
                        DateTime horaActual = item.fechaInicio.AddMinutes(i * minutosAgenda);
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
        public IActionResult ObtenerCitasRecurso(
            [FromQuery(Name = "empresa")] int empresaId,
            [FromQuery] DateTime FechaIni,
            [FromQuery] DateTime FechaFin,
            [FromQuery] string? recurso)
        {
            // recurso es opcional: si no llega, llega vacío o "null" se consultan las citas de todos los recursos.
            // Se recibe como texto porque como int un "recurso=null" haría que [ApiController] responda 400.
            int? recursoId = int.TryParse(recurso, out var recursoNum) && recursoNum > 0 ? recursoNum : null;
            var dto = new CitaDTO { empresa = empresaId, FechaIni = FechaIni, FechaFin = FechaFin };

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
                    var padres = ContextBD.Padres
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
                                    cp.citaNavigation.cliente,
                                    cp.citaNavigation.bono,
                                    cp.citaNavigation.fidelidad,

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

                                    FidelidadCantidad = ContextBD.Fidelidads
                                        .Where(f => f.cliente == cp.citaNavigation.cliente)
                                        .Select(f => (int?)f.cantidad)
                                        .FirstOrDefault(),

                                    Servicios = cp.citaNavigation.CitaServicios
                                        .Select(cs => new
                                        {
                                            Id = cs.servicioNavigation.id,
                                            Nombre = cs.servicioNavigation.nombre,
                                            Valor =
                                                cp.citaNavigation.bono == true
                                                || cp.citaNavigation.fidelidad == true
                                                || cp.citaNavigation.descuento == true
                                                || cp.citaNavigation.comboPromo == true
                                                || cs.servicioNavigation.valor == 0
                                            ? (cp.citaNavigation.valor ?? 0)
                                            : (cs.servicioNavigation.valor ?? 0)
                                        })
                                        .ToList()
                                })
                                .ToList()
                        })
                        .ToList();

                    lista = padres
                        .Select(p => new
                        {
                            p.Padre,

                            Citas = p.Citas
                                .Select(c =>
                                {
                                    string fidelidadMensaje = string.Empty;

                                    if (c.estado == Estados.Activo && c.FidelidadCantidad.HasValue)
                                    {
                                        // La cita actual todavía no está contabilizada
                                        var visitaActual = c.FidelidadCantidad.Value + 1;

                                        var fidelidadParametro = ContextBD.FidelidadParametros
                                            .FirstOrDefault(fp =>
                                                fp.cantidad == visitaActual &&
                                                fp.estado);

                                        if (fidelidadParametro != null)
                                        {
                                            var valorServicios = c.Servicios.Sum(s => s.Valor);

                                            var valorFinal = valorServicios -
                                                             (valorServicios *
                                                              fidelidadParametro.porcentaje / 100);

                                            fidelidadMensaje =
                                                $"Es la {visitaActual} visita del cliente " +
                                                $"{c.Cliente.NombreCliente} y tiene un " +
                                                $"{fidelidadParametro.porcentaje:0}% de descuento " +
                                                $"para un total de " +
                                                $"{valorFinal.ToString("C0", new CultureInfo("es-CO"))}";
                                        }
                                    }

                                    return new
                                    {
                                        c.id,
                                        c.fechaInicio,
                                        c.estado,
                                        c.recurso,
                                        c.NombreRecurso,
                                        c.Cliente,
                                        c.Servicios,
                                        fidelidad = fidelidadMensaje
                                    };
                                })
                                .ToList()
                        })
                        .OrderBy(o => o.Padre)
                        .ToList();
                    //lista = ContextBD.Padres
                    //    .Where(p => p.CitaPadres.Any(cp =>
                    //        cp.citaNavigation.fechaInicio >= dto.FechaIni &&
                    //        cp.citaNavigation.fechaFin <= dto.FechaFin &&
                    //        (cp.citaNavigation.estado == Estados.Activo ||
                    //         cp.citaNavigation.estado == Estados.Asistio)))
                    //    .Select(p => new
                    //    {
                    //        Padre = p.id,

                    //        Citas = p.CitaPadres
                    //            .Where(cp =>
                    //                cp.citaNavigation.fechaInicio >= dto.FechaIni &&
                    //                cp.citaNavigation.fechaFin <= dto.FechaFin &&
                    //                (cp.citaNavigation.estado == Estados.Activo ||
                    //                 cp.citaNavigation.estado == Estados.Asistio))
                    //            .Select(cp => new
                    //            {
                    //                cp.citaNavigation.id,
                    //                cp.citaNavigation.fechaInicio,
                    //                cp.citaNavigation.estado,
                    //                cp.citaNavigation.recurso,

                    //                NombreRecurso = ContextBD.Usuarios
                    //                        .Where(u => u.id == cp.citaNavigation.recurso)
                    //                        .Select(u => u.nombre)
                    //                        .FirstOrDefault(),

                    //                Cliente = ContextBD.Clientes
                    //                        .Where(cl => cl.id == cp.citaNavigation.cliente)
                    //                        .Select(cl => new
                    //                        {
                    //                            NombreCliente = cl.nombre,
                    //                            TelefonoCliente = cl.telefono
                    //                        })
                    //                        .FirstOrDefault(),

                    //                Servicios = cp.citaNavigation.CitaServicios
                    //                    .Select(cs => new
                    //                    {
                    //                        Id = cs.servicioNavigation.id,
                    //                        Nombre = cs.servicioNavigation.nombre,
                    //                        Valor = cs.servicioNavigation.nombre == "Otro" || cp.citaNavigation.bono == true || cp.citaNavigation.fidelidad == true || cs.servicioNavigation.valor == 0
                    //                                ? (cp.citaNavigation.valor ?? 0) : (cs.servicioNavigation.valor ?? 0)
                    //                    }).ToList()
                    //            }).ToList()
                    //    })
                    //    .OrderBy(o => o.Padre)
                    //    .ToList();
                }
                else
                {
                    if (empresa.recursoMultiple == true)
                    {
                        var citas = ContextBD.Citas
                            .Where(e => e.fechaInicio >= dto.FechaIni
                                        && e.fechaFin <= dto.FechaFin
                                        && (recursoId == null || e.recurso == recursoId)
                                        && (e.estado == Estados.Activo || e.estado == Estados.Asistio))
                            .Select(c => new
                            {
                                c.id,
                                c.fechaInicio,
                                c.estado,
                                c.fidelidad,
                                c.cliente,
                                c.recurso,

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
                                    Valor = c.bono == true
                                            || c.fidelidad == true
                                            || c.descuento == true
                                            || c.comboPromo == true
                                            || cs.servicioNavigation.valor == 0
                                            ? (c.valor ?? 0)
                                            : (cs.servicioNavigation.valor ?? 0)
                                }).ToList()
                            })
                            .ToList();

                        var nombresRecursos = agendaService.ObtenerNombresRecursos(citas.Select(c => c.recurso), ContextBD);

                        lista = citas
                            .Select(c =>
                            {
                                var fidelidadCantidad = ContextBD.Fidelidads
                                    .Where(f => f.cliente == c.cliente)
                                    .Select(f => (int?)f.cantidad)
                                    .FirstOrDefault();

                                string fidelidadMensaje = string.Empty;

                                if (c.estado == Estados.Activo && fidelidadCantidad.HasValue)
                                {
                                    var visitaActual = fidelidadCantidad.Value + 1;

                                    var fidelidadParametro = ContextBD.FidelidadParametros
                                        .FirstOrDefault(p =>
                                            p.cantidad == visitaActual &&
                                            p.estado);

                                    if (fidelidadParametro != null)
                                    {
                                        var valorServicios = c.Servicios.Sum(s => s.Valor);

                                        // Fecha de la cita
                                        var fechaCita = c.fechaInicio.Date;

                                        // Consultar recursos diferentes del mismo cliente y día
                                        var cantidadRecursos = ContextBD.Citas
                                            .Where(cita =>
                                                cita.cliente == c.cliente &&
                                                cita.fechaInicio.Date == fechaCita &&
                                                (cita.estado == Estados.Activo ||
                                                 cita.estado == Estados.Asistio))
                                            .Select(cita => cita.recurso)
                                            .Distinct()
                                            .Count();

                                        // Evitar división por cero
                                        cantidadRecursos = Math.Max(cantidadRecursos, 1);

                                        // Dividir el porcentaje entre los recursos diferentes
                                        var porcentajeFidelidad =
                                            fidelidadParametro.porcentaje / cantidadRecursos;

                                        var valorFinal = valorServicios -
                                                         (valorServicios *
                                                          porcentajeFidelidad / 100);

                                        fidelidadMensaje =
                                            $"Es la {visitaActual} visita del cliente " +
                                            $"{c.Cliente?.NombreCliente} y tiene un " +
                                            $"descuento del {porcentajeFidelidad:0.##}% " +
                                            $"para un total de " +
                                            $"{valorFinal.ToString("C0", new CultureInfo("es-CO"))}";
                                    }
                                }

                                return new
                                {
                                    c.id,
                                    c.fechaInicio,
                                    c.estado,
                                    c.recurso,
                                    nombreRecurso = nombresRecursos.GetValueOrDefault(c.recurso),
                                    nombreCliente = c.Cliente?.NombreCliente,
                                    telefonoCliente = c.Cliente?.TelefonoCliente,
                                    servicios = c.Servicios,
                                    fidelidad = fidelidadMensaje
                                };
                            })
                            .OrderBy(o => o.fechaInicio)
                            .ToList();
                    }
                    else
                    {
                        var citas = ContextBD.Citas
                            .Where(e => e.fechaInicio >= dto.FechaIni
                                        && e.fechaFin <= dto.FechaFin
                                        && (recursoId == null || e.recurso == recursoId)
                                        && (e.estado == Estados.Activo || e.estado == Estados.Asistio))
                            .Select(c => new
                            {
                                c.id,
                                c.fechaInicio,
                                c.estado,
                                c.fidelidad,
                                c.cliente,
                                c.recurso,
                                c.descuento,
                                c.tipoDescuento,
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
                                    Valor = c.bono == true
                                            || c.fidelidad == true
                                            || c.descuento == true
                                            || c.comboPromo == true
                                            || cs.servicioNavigation.valor == 0
                                            ? (c.valor ?? 0)
                                            : (cs.servicioNavigation.valor ?? 0)
                                }).ToList()
                            })
                            .ToList();

                        var nombresRecursos = agendaService.ObtenerNombresRecursos(citas.Select(c => c.recurso), ContextBD);

                        lista = citas
                            .Select(c =>
                            {
                                var fidelidadCantidad = ContextBD.Fidelidads
                                    .Where(f => f.cliente == c.cliente)
                                    .Select(f => (int?)f.cantidad)
                                    .FirstOrDefault();

                                string fidelidadMensaje = string.Empty;

                                if (c.descuento == true)
                                {
                                    fidelidadMensaje =
                                            $"{c.Cliente?.NombreCliente} se le aplico un descuento por motivo de {c.tipoDescuento} ";
                                }
                                else
                                {
                                    if (c.estado == Estados.Activo && fidelidadCantidad.HasValue)
                                    {


                                        var visitaActual = fidelidadCantidad.Value + 1;

                                        var fidelidadParametro = ContextBD.FidelidadParametros
                                            .FirstOrDefault(p =>
                                                p.cantidad == visitaActual &&
                                                p.estado);

                                        if (fidelidadParametro != null)
                                        {
                                            var valorServicios = c.Servicios.Sum(s => s.Valor);

                                            var valorFinal = valorServicios -
                                                             (valorServicios *
                                                              fidelidadParametro.porcentaje / 100);

                                            fidelidadMensaje =
                                                $"Es la {visitaActual} visita del cliente " +
                                                $"{c.Cliente?.NombreCliente} y tiene un " +
                                                $"descuento del {fidelidadParametro.porcentaje:0}% " +
                                                $"para un total de " +
                                                $"{valorFinal.ToString("C0", new CultureInfo("es-CO"))}";
                                        }

                                    }
                                }

                                return new
                                {
                                    c.id,
                                    c.fechaInicio,
                                    c.estado,
                                    c.recurso,
                                    nombreRecurso = nombresRecursos.GetValueOrDefault(c.recurso),
                                    nombreCliente = c.Cliente?.NombreCliente,
                                    telefonoCliente = c.Cliente?.TelefonoCliente,
                                    servicios = c.Servicios,
                                    fidelidad = fidelidadMensaje
                                };
                            })
                            .OrderBy(o => o.fechaInicio)
                            .ToList();
                    }
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

                bool fidelidad = ContextBD.Parametros.Where(p => p.estado && p.tipo == "ManejaFidelidad").Select(p => p.valor).FirstOrDefault()?.ToLower() == "true";

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
                                ServicioValor = cita.bono == true || cita.fidelidad == true || cita.descuento == true || cita.comboPromo == true || s.valor == 0 ? (cita.valor ?? 0) : (s.valor ?? 0),
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
                            citaId.metodoPago = dto.metodoPago;
                            if (dto.Valor.HasValue && dto.Valor > 0)
                            {
                                citaId.valor = dto.Valor;
                            }

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
                                                         ServicioValor = citaId.bono == true || citaId.fidelidad == true || citaId.descuento == true || citaId.comboPromo == true || (s.valor ?? 0) == 0 ? (citaId.valor ?? 0) : (s.valor ?? 0),
                                                         Porcentaje = (u.porcentaje ?? 0),
                                                         Sede = se.Nombre
                                                     }
                                        ).ToList();

                                    foreach (var servicio in servicios)
                                    {
                                        decimal valorServicio = servicio.ServicioValor;

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

                        if (empresa.recursoMultiple == true)
                        {
                            decimal porcFidelidad = 0m;

                            var citaId = ContextBD.Citas
                                .Include(c => c.CitaServicios)
                                .ThenInclude(cs => cs.servicioNavigation)
                                .FirstOrDefault(p => p.id == dto.Id);

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
                                // Guardar el estado anterior antes de actualizarlo.
                                bool yaEstabaAsistida = citaId.estado == Estados.Asistio;
                                var fechaDia = citaId.fechaInicio.Date;
                                var fechaDiaSiguiente = fechaDia.AddDays(1);

                                // 1. Actualizar la cita actual.
                                citaId.estado = Estados.Asistio;
                                citaId.bono = dto.Bono;
                                citaId.metodoPago = dto.metodoPago;

                                if (dto.Valor.HasValue && dto.Valor.Value > 0)
                                {
                                    citaId.valor = dto.Valor;
                                }

                                // 2. Procesar fidelidad si está habilitada.
                                if (fidelidad && citaId.cliente.HasValue)
                                {
                                    int idCliente = citaId.cliente.Value;

                                    // Obtener todas las citas del cliente del día.
                                    // Incluye las citas Activo y Asistio.
                                    var citasClienteDia = ContextBD.Citas
                                        .Where(c =>
                                            c.cliente == idCliente &&
                                            c.fechaInicio >= fechaDia &&
                                            c.fechaInicio < fechaDiaSiguiente &&
                                            (c.estado == Estados.Activo ||
                                             c.estado == Estados.Asistio))
                                        .ToList();

                                    // 3. Obtener las citas relacionadas con CitaPadres.
                                    var idsCitasDia = citasClienteDia
                                        .Select(c => c.id)
                                        .Distinct()
                                        .ToList();

                                    var citasConPadre = (
                                        from cp in ContextBD.CitaPadres
                                        join c in ContextBD.Citas
                                            on cp.cita equals c.id
                                        where idsCitasDia.Contains(c.id) &&
                                              (c.estado == Estados.Activo ||
                                               c.estado == Estados.Asistio)
                                        select c
                                    )
                                    .Distinct()
                                    .ToList();

                                    int cantidadCitasDescuento = citasConPadre.Count;

                                    // 4. Consultar el contador actual de fidelidad.
                                    var fidelidadCliente = ContextBD.Fidelidads.FirstOrDefault(p => p.cliente == idCliente);

                                    int visitasActuales = fidelidadCliente?.cantidad ?? 0;

                                    // El nivel corresponde a la siguiente visita.
                                    int visitaActual = visitasActuales + 1;

                                    var nivel = ContextBD.FidelidadParametros.FirstOrDefault(p => p.cantidad == visitaActual && p.estado);

                                    // 5. Aplicar el descuento SOLAMENTE a la cita actual.
                                    if (nivel != null && cantidadCitasDescuento > 0 && citaId.fidelidad != true)
                                    {
                                        // Dividir el porcentaje entre las citas del día.
                                        decimal porcentajePorCita = nivel.porcentaje / cantidadCitasDescuento;
                                        porcFidelidad = porcentajePorCita;

                                        if (citaId.valor.HasValue && citaId.valor.Value > 0)
                                        {
                                            decimal valorOriginal = citaId.valor.Value;

                                            decimal descuento = valorOriginal * porcentajePorCita / 100m;

                                            citaId.valor = valorOriginal - descuento;

                                            citaId.fidelidad = true;
                                        }
                                    }

                                    // 6. Comprobar si todas las citas del cliente
                                    // de ese día ya están asistidas.
                                    bool todasAsistieron = citasClienteDia.Any() && citasClienteDia.All(c => c.estado == Estados.Asistio);

                                    // Incrementar únicamente cuando la cita actual
                                    // acaba de cambiar a Asistio y todas ya asistieron.
                                    if (todasAsistieron && !yaEstabaAsistida)
                                    {
                                        if (fidelidadCliente == null)
                                        {
                                            fidelidadCliente = new Fidelidad
                                            {
                                                cliente = idCliente,
                                                cantidad = 1
                                            };

                                            ContextBD.Fidelidads.Add(fidelidadCliente);
                                        }
                                        else
                                        {
                                            // Obtener la cantidad máxima configurada en fidelidad.
                                            int cantidadMaxima = ContextBD.FidelidadParametros
                                                .Where(p => p.estado)
                                                .Select(p => (int?)p.cantidad)
                                                .Max() ?? 0;

                                            if (cantidadMaxima > 0 &&
                                                fidelidadCliente.cantidad >= cantidadMaxima - 1)
                                            {
                                                // Al completar la última visita, reiniciar a cero.
                                                fidelidadCliente.cantidad = 0;
                                            }
                                            else
                                            {
                                                fidelidadCliente.cantidad++;
                                            }
                                        }
                                    }
                                }

                                // 7. Liquidar los servicios de la cita actual.
                                //var servicios = (
                                //    from cs in ContextBD.CitaServicios
                                //    join s in ContextBD.Servicios
                                //        on cs.servicio equals s.id
                                //    join u in ContextBD.Usuarios
                                //        on citaId.recurso equals u.id
                                //    join se in ContextBD.Sedes
                                //        on u.sede equals se.id
                                //    join sp in ContextBD.ServiciosParametros
                                //        on s.id equals sp.servicio into spGroup
                                //    from sp in spGroup.DefaultIfEmpty()
                                //    where cs.cita == citaId.id
                                //    select new
                                //    {
                                //        ServicioValor = (citaId.valor ?? 0) > 0 ? (citaId.valor ?? 0) : (s.valor ?? 0),
                                //        //citaId.bono == true ||
                                //        //citaId.fidelidad == true ||
                                //        //s.valor == 0
                                //        //    ? (citaId.valor ?? 0)
                                //        //    : (s.valor ?? 0),

                                //        Porcentaje =
                                //            (u.porcentaje ?? 0) != 0
                                //                ? (u.porcentaje ?? 0)
                                //                : (sp != null
                                //                    ? (sp.porcentaje ?? 0)
                                //                    : 0),

                                //        Sede = se.Nombre
                                //    }
                                //).ToList();
                                var usuario = ContextBD.Usuarios
                                    .Where(u => u.id == citaId.recurso)
                                    .Select(u => new
                                    {
                                        Porcentaje = u.porcentaje ?? 0,
                                        Sede = ContextBD.Sedes
                                            .Where(se => se.id == u.sede)
                                            .Select(se => se.Nombre)
                                            .FirstOrDefault()
                                    })
                                    .FirstOrDefault();

                                var servicios = new List<ServicioLiquidacionDTO>();

                                if (usuario != null && usuario.Porcentaje != 0)
                                {
                                    // CASO 1: El usuario tiene un porcentaje propio.
                                    // Solo se devuelve un registro, aunque tenga varios servicios.

                                    servicios.Add(new ServicioLiquidacionDTO
                                    {
                                        ServicioValor = citaId.valor ?? 0,
                                        Porcentaje = usuario.Porcentaje,
                                        Sede = usuario.Sede
                                    });
                                }
                                else
                                {
                                    // CASO 2: El porcentaje depende de ServiciosParametros.
                                    // Se conserva un registro por cada servicio y su porcentaje.

                                    servicios = (
                                            from cs in ContextBD.CitaServicios
                                            join s in ContextBD.Servicios
                                                on cs.servicio equals s.id
                                            join u in ContextBD.Usuarios
                                                on citaId.recurso equals u.id
                                            join se in ContextBD.Sedes
                                                on u.sede equals se.id
                                            join sp in ContextBD.ServiciosParametros
                                                on s.id equals sp.servicio into spGroup
                                            from sp in spGroup.DefaultIfEmpty()
                                            where cs.cita == citaId.id
                                            orderby s.valor descending, s.id
                                            select new ServicioLiquidacionDTO
                                            {
                                                ServicioValor = s.valor ?? 0,
                                                Porcentaje = sp != null
                                                    ? (sp.porcentaje ?? 0)
                                                    : 0,
                                                Sede = se.Nombre
                                            }
                                        )
                                        .ToList();

                                        if (citaId.fidelidad == true && servicios.Any())
                                        {
                                            servicios[0].ServicioValor -=
                                                servicios[0].ServicioValor * porcFidelidad / 100m;
                                        }
                                }

                                foreach (var servicio in servicios)
                                {
                                    decimal valorServicio = servicio.ServicioValor;

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
                            else
                            {
                                citaId.estado = Estados.Inasistencia;
                            }

                            ContextBD.SaveChanges();
                        }
                        else
                        {
                            var citaId = ContextBD.Citas
                            .Include(c => c.CitaServicios)
                            .ThenInclude(cs => cs.servicioNavigation)
                            .FirstOrDefault(p => p.id == dto.Id);

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
                                citaId.metodoPago = dto.metodoPago;
                                if (dto.Valor.HasValue && dto.Valor.Value > 0)
                                {
                                    citaId.valor = dto.Valor;
                                }
                                if (fidelidad)
                                {
                                    bool validaCliente = true;

                                    if (empresa.id == 2)
                                    {
                                        if (citaId.usuarioCreacion == "Usuario" || !citaId.CitaServicios.Any(cs => cs.servicioNavigation.esPago == true))
                                        {
                                            validaCliente = false;
                                        }
                                    }

                                    if (validaCliente)
                                    {
                                        var fidelidadCliente = ContextBD.Fidelidads.FirstOrDefault(p => p.cliente == citaId.cliente);

                                        if (fidelidadCliente == null)
                                        {
                                            fidelidadCliente = new Fidelidad
                                            {
                                                cliente = citaId.cliente.Value,
                                                cantidad = 1
                                            };

                                            ContextBD.Fidelidads.Add(fidelidadCliente);
                                        }
                                        else
                                        {
                                            fidelidadCliente.cantidad++;
                                        }

                                        var niveles = ContextBD.FidelidadParametros
                                            .Where(p => p.estado)
                                            .OrderBy(p => p.cantidad)
                                            .ToList();

                                        var nivel = niveles
                                            .FirstOrDefault(p => p.cantidad == fidelidadCliente.cantidad);

                                        if (nivel != null)
                                        {
                                            if (citaId.valor.HasValue && citaId.valor > 0)
                                            {
                                                if (citaId.descuento != true)
                                                {
                                                    citaId.valor = citaId.valor.Value - (citaId.valor.Value * nivel.porcentaje / 100);
                                                    citaId.fidelidad = true;
                                                }
                                            }

                                            var cantidadMaxima = niveles
                                                .Select(p => p.cantidad)
                                                .LastOrDefault();

                                            if (fidelidadCliente.cantidad == cantidadMaxima)
                                            {
                                                fidelidadCliente.cantidad = 0;
                                            }
                                        }
                                    }
                                }

                                if (empresa.id != 2)
                                {
                                    //var servicios = (
                                    //from cs in ContextBD.CitaServicios
                                    //join s in ContextBD.Servicios on cs.servicio equals s.id
                                    //join u in ContextBD.Usuarios on citaId.recurso equals u.id
                                    //join se in ContextBD.Sedes on u.sede equals se.id
                                    //join sp in ContextBD.ServiciosParametros on s.id equals sp.servicio into spGroup
                                    //from sp in spGroup.DefaultIfEmpty()
                                    //where cs.cita == citaId.id
                                    //select new
                                    //{
                                    //    ServicioValor = (citaId.valor ?? 0) > 0 ? (citaId.valor ?? 0) : (s.valor ?? 0), //citaId.bono == true || citaId.fidelidad == true || s.valor == 0 ? (citaId.valor ?? 0) : (s.valor ?? 0),
                                    //    Porcentaje = (u.porcentaje ?? 0) != 0 ? (u.porcentaje ?? 0) : (sp != null ? (sp.porcentaje ?? 0) : 0),
                                    //    Sede = se.Nombre
                                    //}
                                    //).ToList();
                                    var usuario = ContextBD.Usuarios
                                    .Where(u => u.id == citaId.recurso)
                                    .Select(u => new
                                    {
                                        Porcentaje = u.porcentaje ?? 0,
                                        Sede = ContextBD.Sedes
                                            .Where(se => se.id == u.sede)
                                            .Select(se => se.Nombre)
                                            .FirstOrDefault()
                                    })
                                    .FirstOrDefault();

                                    var servicios = new List<dynamic>();

                                    if (usuario != null && usuario.Porcentaje != 0)
                                    {
                                        // CASO 1: El usuario tiene un porcentaje propio.
                                        // Solo se devuelve un registro, aunque tenga varios servicios.

                                        servicios.Add(new
                                        {
                                            ServicioValor = citaId.valor ?? 0,
                                            Porcentaje = usuario.Porcentaje,
                                            Sede = usuario.Sede
                                        });
                                    }
                                    else
                                    {
                                        // CASO 2: El porcentaje depende de ServiciosParametros.
                                        // Se conserva un registro por cada servicio y su porcentaje.

                                        servicios = (
                                            from cs in ContextBD.CitaServicios
                                            join s in ContextBD.Servicios
                                                on cs.servicio equals s.id
                                            join u in ContextBD.Usuarios
                                                on citaId.recurso equals u.id
                                            join se in ContextBD.Sedes
                                                on u.sede equals se.id
                                            join sp in ContextBD.ServiciosParametros
                                                on s.id equals sp.servicio into spGroup
                                            from sp in spGroup.DefaultIfEmpty()
                                            where cs.cita == citaId.id
                                            select new
                                            {
                                                ServicioValor = (s.valor ?? 0),

                                                Porcentaje = sp != null
                                                    ? (sp.porcentaje ?? 0)
                                                    : 0,

                                                Sede = se.Nombre
                                            }
                                        )
                                        .ToList<dynamic>();
                                    }

                                    foreach (var servicio in servicios)
                                    {
                                        decimal valorServicio = servicio.ServicioValor;

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
            int padre = 0;

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
                    RespuestaDTO validacion = new RespuestaDTO
                    {
                        Disponible = true,
                        EnvioMensaje = false
                    };

                    Padre cita = null;

                    if (empresa.servicio == true)
                        cita = ContextBD.Padres.FirstOrDefault(p => p.id == dto.Id);
                    else
                    {
                        if (empresa.recursoMultiple == true)
                        {
                            var citaIndividual = ContextBD.Citas.FirstOrDefault(c => c.id == dto.Id);

                            if (citaIndividual != null)
                            {
                                var citaPadre = ContextBD.CitaPadres.FirstOrDefault(cp => cp.cita == citaIndividual.id);

                                if (citaPadre != null)
                                {
                                    cita = ContextBD.Padres.FirstOrDefault(p => p.id == citaPadre.padre);
                                }
                            }
                        }
                    }

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
                            if (empresa.servicio == true)
                            {
                                if (dto.validaHorario)
                                    validacion = agendaService.ValidarDisponibilidad(ContextBD, dto.Citas);

                                if (!validacion.Disponible)
                                {
                                    return Ok(new
                                    {
                                        mensaje = "Los servicios seleccionados superan el tiempo disponible del usuario.",
                                        exito = false
                                    });
                                }

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
                            }
                            else
                            {
                                if (empresa.recursoMultiple == true)
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
                                        .Where(cp => idsCitas.Contains(cp.cita));

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
                                            estado = empresa.recursoMultiple == true ? Estados.Activo : dto.UsuarioCreacion == "Usuario" ? Estados.Asistio : Estados.Activo,
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
                                }
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

                var citas = ContextBD.Citas
                .Where(e => e.fechaInicio >= dto.FechaIni
                            && e.fechaFin <= dto.FechaFin
                            && (e.estado == Estados.Activo || e.estado == Estados.Asistio))
                .Select(c => new
                {
                    c.id,
                    c.fechaInicio,
                    c.estado,
                    c.cliente,
                    c.descuento,
                    c.tipoDescuento,

                    // Valores con la misma regla de consultarCitasRecurso, usados para el mensaje de fidelidad
                    ValoresFidelidad = c.CitaServicios
                        .Select(cs =>
                            c.bono == true
                            || c.fidelidad == true
                            || c.descuento == true
                            || c.comboPromo == true
                            || cs.servicioNavigation.valor == 0
                            ? (c.valor ?? 0)
                            : (cs.servicioNavigation.valor ?? 0))
                        .ToList(),

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
                            Valor =  cs.servicioNavigation.nombre == "Otro" ? c.valor : cs.servicioNavigation.valor
                        })
                        .ToList()
                })
                .ToList();

                // Fidelidad: se precarga en una sola consulta en vez de una por cita
                var clientesIds = citas
                    .Where(c => c.cliente.HasValue)
                    .Select(c => c.cliente.Value)
                    .Distinct()
                    .ToList();

                var fidelidadPorCliente = ContextBD.Fidelidads
                    .Where(f => clientesIds.Contains(f.cliente))
                    .AsEnumerable()
                    .GroupBy(f => f.cliente)
                    .ToDictionary(g => g.Key, g => g.First().cantidad);

                var parametrosFidelidad = ContextBD.FidelidadParametros
                    .Where(p => p.estado)
                    .AsEnumerable()
                    .GroupBy(p => p.cantidad)
                    .ToDictionary(g => g.Key, g => g.First());

                string ObtenerMensajeFidelidad(int citaCliente, string citaEstado, bool? citaDescuento,
                                               string citaTipoDescuento, DateTime citaFecha,
                                               string nombreCliente, decimal valorServicios)
                {
                    // Igual que consultarCitasRecurso: el descuento manual solo aplica a empresas sin servicio ni recurso múltiple
                    if (empresa.servicio != true && empresa.recursoMultiple != true && citaDescuento == true)
                    {
                        return $"{nombreCliente} se le aplico un descuento por motivo de {citaTipoDescuento} ";
                    }

                    if (citaEstado != Estados.Activo || !fidelidadPorCliente.TryGetValue(citaCliente, out var cantidad))
                    {
                        return string.Empty;
                    }

                    // La cita actual todavía no está contabilizada
                    var visitaActual = cantidad + 1;

                    if (!parametrosFidelidad.TryGetValue(visitaActual, out var fidelidadParametro))
                    {
                        return string.Empty;
                    }

                    if (empresa.servicio == true)
                    {
                        var valorFinalServicio = valorServicios - (valorServicios * fidelidadParametro.porcentaje / 100);

                        return $"Es la {visitaActual} visita del cliente " +
                               $"{nombreCliente} y tiene un " +
                               $"{fidelidadParametro.porcentaje:0}% de descuento " +
                               $"para un total de " +
                               $"{valorFinalServicio.ToString("C0", new CultureInfo("es-CO"))}";
                    }

                    if (empresa.recursoMultiple == true)
                    {
                        var fechaCita = citaFecha.Date;

                        // Consultar recursos diferentes del mismo cliente y día
                        var cantidadRecursos = ContextBD.Citas
                            .Where(cita =>
                                cita.cliente == citaCliente &&
                                cita.fechaInicio.Date == fechaCita &&
                                (cita.estado == Estados.Activo ||
                                 cita.estado == Estados.Asistio))
                            .Select(cita => cita.recurso)
                            .Distinct()
                            .Count();

                        // Evitar división por cero
                        cantidadRecursos = Math.Max(cantidadRecursos, 1);

                        // Dividir el porcentaje entre los recursos diferentes
                        var porcentajeFidelidad = fidelidadParametro.porcentaje / cantidadRecursos;

                        var valorFinalMultiple = valorServicios - (valorServicios * porcentajeFidelidad / 100);

                        return $"Es la {visitaActual} visita del cliente " +
                               $"{nombreCliente} y tiene un " +
                               $"descuento del {porcentajeFidelidad:0.##}% " +
                               $"para un total de " +
                               $"{valorFinalMultiple.ToString("C0", new CultureInfo("es-CO"))}";
                    }

                    var valorFinal = valorServicios - (valorServicios * fidelidadParametro.porcentaje / 100);

                    return $"Es la {visitaActual} visita del cliente " +
                           $"{nombreCliente} y tiene un " +
                           $"descuento del {fidelidadParametro.porcentaje:0}% " +
                           $"para un total de " +
                           $"{valorFinal.ToString("C0", new CultureInfo("es-CO"))}";
                }

                lista = citas
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

                                    estado = c.estado.ToString(),

                                    fidelidad = c.cliente.HasValue
                                        ? ObtenerMensajeFidelidad(c.cliente.Value, c.estado, c.descuento,
                                                                  c.tipoDescuento, c.fechaInicio,
                                                                  c.Cliente?.Nombre, c.ValoresFidelidad.Sum())
                                        : string.Empty
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
                                          where c.orden == o.id
                                          select servicio.nombre).FirstOrDefault(),
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
    }
}
