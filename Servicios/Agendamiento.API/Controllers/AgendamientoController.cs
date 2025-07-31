using Agendamiento.API.Data;
using Agendamiento.API.Data.Modelos;
using Compartido;
using Compartido.Constantes;
using Compartido.DTOs;
using Compartido.Metodos;
using Microsoft.AspNetCore.Mvc;
//using Agendamiento.API.Servicios;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

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
        public IActionResult CrearCita([FromBody] CitaDTO dto)
        {
            string nombreBase = string.Empty;
            int idCliente = 0;
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

                if (dto.clienteInfo != null)
                {
                    var user = ContextBD.Clientes.Where(e => e.telefono == dto.clienteInfo.Telefono).FirstOrDefault();

                    if (user == null)
                    {
                        var nueva = new Cliente
                        {
                            nombre = dto.clienteInfo.Nombre,
                            correo = dto.clienteInfo.Correo,
                            telefono = dto.clienteInfo.Telefono,
                            estado = Estados.Activo,
                            fechaCreacion = DateTime.Now
                        };

                        ContextBD.Clientes.Add(nueva);

                        idCliente = nueva.id;
                    }
                    else
                    {
                        idCliente = user.id;
                    }
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
                    fechaCreacion = DateTime.Now,
                    usuarioCreacion = dto.UsuarioCreacion
                };

                ContextBD.Citas.Add(nuevo);

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

                return Ok(new
                {
                    mensaje = "Cita registrada exitosamente",
                    exito = true
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

        [HttpPost("registrarBloqueo")]
        public IActionResult CrearBloqueo([FromBody] BloqueoDTO dto)
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

                var nuevo = new BloqueosHorario
                {
                    tipoBloqueo = dto.TipoBloqueo,
                    usuario = dto.Usuario,
                    fechaInicio = dto.FechaInicio,
                    fechaFin = dto.FechaFin,
                    horaInicio = TimeSpan.Parse(dto.HoraInicio),
                    horaFin = TimeSpan.Parse(dto.HoraFin),
                    motivo = dto.Motivo,
                    diasSemana = dto.DiasSemana
                };

                ContextBD.BloqueosHorarios.Add(nuevo);
                ContextBD.SaveChanges();

                return Ok(new
                {
                    mensaje = "Cita registrada exitosamente",
                    exito = true
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

        [HttpGet("consultarBloqueos")]
        public IActionResult ObtenerBloqueos([FromQuery] BloqueoDTO dto)
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

                //var lista = ContextBD.BloqueosHorarios
                //                     .Where(e => e.fechaInicio >= dto.FechaInicio
                //                            && e.fechaFin <= dto.FechaFin
                //                            && e.usuario == dto.Usuario)
                //                     .ToList();

                //if (lista == null || lista.Count == 0)
                //{
                //    return Ok(new
                //    {
                //        mensaje = "No hay bloqueos registrados",
                //        exito = false
                //    });
                //}

                //return Ok(new
                //{
                //    exito = true,
                //    datos = lista
                //});

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
                             b.fechaInicio.HasValue && b.fechaFin.HasValue &&
                             fechaSolo >= b.fechaInicio.Value.Date &&
                             fechaSolo <= b.fechaFin.Value.Date &&
                             b.diasSemana != null &&
                             b.diasSemana.Split(',').Contains(diaSemana.ToString()))

                            ||

                            (b.tipoBloqueo == 3 &&
                             b.fechaInicio.HasValue && b.fechaFin.HasValue &&
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
                        resultado.HorasBloqueadas.Add(hora);
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
                LogsCatch.GuardarError(ex, configuracion, nombreBase);

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
            List<string> horasGeneradas = new List<string>();
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

                var lista = ContextBD.Citas
                                     .Where(e => e.fechaInicio >= dto.FechaIni
                                            && e.fechaFin <= dto.FechaFin
                                            && e.recurso == dto.recurso)
                                     .ToList();

                if (lista == null || lista.Count == 0)
                {
                    return Ok(new
                    {
                        mensaje = "No hay citas registradas",
                        exito = false
                    });
                }
                else
                {
                    foreach (var item in lista)
                    {
                        for (int i = 0; i < item.hora; i++)
                        {
                            DateTime horaActual = item.fechaInicio.AddHours(i);
                            horasGeneradas.Add(horaActual.ToString("hh:mm tt", new CultureInfo("en-US")));
                        }
                    }
                }

                return Ok(new
                {
                    exito = true,
                    datos = horasGeneradas
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

        [HttpGet("consultarAgendaCliente")]
        public IActionResult ObtenerAgendaCliente([FromQuery] CitaDTO dto)
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

                var lista = ContextBD.Citas
                                     .Where(e => e.fechaInicio >= dto.FechaIni
                                            && e.fechaFin <= dto.FechaFin
                                            && e.recurso == dto.Cliente)
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
