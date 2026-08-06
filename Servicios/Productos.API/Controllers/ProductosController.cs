using Compartido;
using Compartido.Constantes;
using Compartido.Data;
using Compartido.Data.Modelos;
using Compartido.DTOs;
using Compartido.Metodos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Productos.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductosController : ControllerBase
    {
        private readonly GeneralContext ContextGene;
        private readonly FactoryContext ContextFact;
        private readonly IConfiguration configuracion;
        //private readonly ClientesService ClienteService;

        public ProductosController(GeneralContext gen, FactoryContext fac, IConfiguration config/*, ClientesService cliser*/)
        {
            ContextGene = gen;
            ContextFact = fac;
            configuracion = config;
            //ClienteService = cliser;
        }

        [HttpPost("registrarProducto")]
        public IActionResult CrearProducto([FromBody] ProductoDTO dto)
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
                using var transaction = ContextBD.Database.BeginTransaction();

                try
                {
                    var prod = ContextBD.Productos.Where(s => s.nombre == dto.Nombre).FirstOrDefault();

                    if (prod == null)
                    {
                        var nuevoProd = new Producto
                        {
                            nombre = dto.Nombre,
                            codigo = dto.Codigo,
                            precioVenta = dto.PrecioVenta,
                            porcentaje = dto.Porcentaje,
                            estado = Estados.Activo
                        };

                        ContextBD.Productos.Add(nuevoProd);
                        ContextBD.SaveChanges();

                        var nuevoDeta = new ProductoLote
                        {
                            producto = nuevoProd.id,
                            cantidadInicial = dto.Cantidad,
                            cantidadDisponible = dto.Cantidad,
                            precioCosto = dto.PrecioCosto,
                            usuarioCreacion = dto.UsuarioCreacion,
                            fechaCreacion = dto.FechaCreacion,
                            estado = Estados.Activo
                        };
                        ContextBD.ProductoLotes.Add(nuevoDeta);
                        ContextBD.SaveChanges();

                        transaction.Commit();

                        return Ok(new
                        {
                            mensaje = "Producto registrado exitosamente",
                            exito = true
                        });
                    }
                    else
                    {
                        return Ok(new
                        {
                            mensaje = "El producto " + dto.Nombre + " ya existe.",
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

        [HttpPost("actualizarProducto")]
        public IActionResult ActulizarProducto([FromBody] ProductoDTO dto)
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

                var prod = ContextBD.Productos.Where(s => s.id == dto.Id).FirstOrDefault();

                if (prod == null)
                {
                    return Ok(new
                    {
                        mensaje = "Producto " + dto.Nombre + " no encontrado.",
                        exito = false
                    });
                }
                else
                {
                    var nuevoDeta = new ProductoLote
                    {
                        producto = dto.Id,
                        cantidadInicial = dto.Cantidad,
                        cantidadDisponible = dto.Cantidad,
                        precioCosto = dto.PrecioCosto,
                        usuarioCreacion = dto.UsuarioCreacion,
                        fechaCreacion = dto.FechaCreacion,
                        estado = Estados.Activo
                    };
                    ContextBD.ProductoLotes.Add(nuevoDeta);
                    ContextBD.SaveChanges();

                    return Ok(new
                    {
                        mensaje = "Se cargo el inventario al producto exitosamente",
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

        [HttpPost("editarProducto")]
        public IActionResult EditarProducto([FromBody] ProductoDTO dto)
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

                var prod = ContextBD.Productos.Where(s => s.id == dto.Id).FirstOrDefault();

                if (prod == null)
                {
                    return Ok(new
                    {
                        mensaje = "Producto " + dto.Nombre + " no encontrado.",
                        exito = false
                    });
                }
                else
                {
                    prod.nombre = dto.Nombre;
                    prod.precioVenta = dto.PrecioVenta;
                    prod.porcentaje = dto.Porcentaje;
                    ContextBD.SaveChanges();

                    return Ok(new
                    {
                        mensaje = "Producto editado exitosamente",
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

        [HttpPost("ventaProducto")]
        public IActionResult VentaProducto([FromBody] VentaDTO dto)
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
                using var transaction = ContextBD.Database.BeginTransaction();

                try
                {
                    var venta = new Ventum
                    {
                        factura = dto.factura,
                        fechaVenta = dto.fechaVenta,
                        usuarioVenta = dto.usuarioVenta
                    };

                    ContextBD.Venta.Add(venta);
                    ContextBD.SaveChanges();

                    var detallesCreados = new List<VentaDetalle>();

                    foreach (var detalle in dto.Detalles)
                    {
                        var nuevoDetalle = new VentaDetalle
                        {
                            venta = venta.id,
                            producto = detalle.Producto,
                            cantidad = detalle.Cantidad,
                            valorVenta = detalle.PrecioVenta,
                            total = detalle.Total,
                            comision = detalle.Porcentaje,
                            descuento = detalle.Descuento
                        };

                        ContextBD.VentaDetalles.Add(nuevoDetalle);
                        detallesCreados.Add(nuevoDetalle);
                    }

                    ContextBD.SaveChanges();

                    foreach (var detalle in detallesCreados)
                    {
                        var cantidadSolicitada = detalle.cantidad;

                        var lotes = ContextBD.ProductoLotes
                            .Where(x => x.producto == detalle.producto && x.cantidadDisponible > 0)
                            .OrderBy(x => x.fechaIngreso)
                            .ToList();

                        if (!lotes.Any())
                            throw new Exception("No hay inventario disponible.");

                        foreach (var lote in lotes)
                        {
                            if (cantidadSolicitada <= 0)
                                break;

                            int cantidadUsada = Math.Min(lote.cantidadDisponible, cantidadSolicitada);

                            lote.cantidadDisponible -= cantidadUsada;
                            cantidadSolicitada -= cantidadUsada;

                            if (lote.cantidadDisponible == 0)
                                lote.estado = Estados.Agotado;

                            if (cantidadUsada > 0)
                            {
                                ContextBD.VentaDetalleLotes.Add(new VentaDetalleLote
                                {
                                    ventaDetalle = detalle.id,
                                    productoLote = lote.id,
                                    cantidad = cantidadUsada
                                });
                            }
                        }

                        if (cantidadSolicitada > 0)
                            throw new Exception("Stock insuficiente para completar la venta.");
                    }

                    ContextBD.SaveChanges();
                    transaction.Commit();

                    return Ok(new
                    {
                        mensaje = "Venta registrada exitosamente",
                        exito = true
                    });
                    //var nuevo = new Ventum
                    //{
                    //    factura = dto.factura,
                    //    fechaVenta = dto.fechaVenta,
                    //    usuarioVenta = dto.usuarioVenta
                    //};

                    //ContextBD.Venta.Add(nuevo);

                    //foreach (var detalle in dto.Detalles)
                    //{
                    //    var nuevoDetalle = new VentaDetalle
                    //    {
                    //        venta = nuevo.id,
                    //        producto = detalle.Producto,
                    //        cantidad = detalle.Cantidad,
                    //        valor = detalle.Valor,
                    //        total = detalle.Total
                    //    };

                    //    ContextBD.VentaDetalles.Add(nuevoDetalle);

                    //    var cantidadSolicitada = detalle.Cantidad;

                    //    var lotes = ContextBD.ProductoLotes
                    //        .Where(x => x.producto == detalle.Producto && x.cantidadDisponible > 0)
                    //        .OrderBy(x => x.fechaIngreso)
                    //        .ToList();

                    //    if (!lotes.Any())
                    //        throw new Exception("No hay inventario disponible para este producto.");

                    //    foreach (var lote in lotes)
                    //    {
                    //        if (cantidadSolicitada <= 0)
                    //            break;

                    //        if (lote.cantidadDisponible >= cantidadSolicitada)
                    //        {
                    //            lote.cantidadDisponible -= cantidadSolicitada;
                    //            cantidadSolicitada = 0;
                    //        }
                    //        else
                    //        {
                    //            cantidadSolicitada -= lote.cantidadDisponible;
                    //            lote.cantidadDisponible = 0;
                    //        }

                    //        if (lote.cantidadDisponible == 0)
                    //        {
                    //            lote.estado = "AGOTADO";
                    //        }
                    //    }

                    //    if (cantidadSolicitada > 0)
                    //        throw new Exception("Stock insuficiente para completar la venta.");
                    //}

                    //ContextBD.SaveChanges();
                    //transaction.Commit();

                    //return Ok(new
                    //{
                    //    mensaje = "Venta registrada exitosamente",
                    //    exito = true
                    //});
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

        [HttpGet("productos")]
        public IActionResult ObtenerProductos([FromQuery] int emp)
        {
            string nombreBase = string.Empty;
            try
            {
                var empresa = ContextGene.Empresas.FirstOrDefault(e => e.id == emp && e.estado == Estados.Activo);
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

                var lista = ContextBD.Productos
                    .Select(p => new
                    {
                        p.id,
                        p.nombre,
                        p.codigo,
                        p.precioVenta,
                        p.porcentaje,

                        stock = ContextBD.ProductoLotes
                            .Where(l => l.producto == p.id)
                            .Sum(l => (int?)l.cantidadDisponible) ?? 0,

                        precioCosto = ContextBD.ProductoLotes
                            .Where(l => l.producto == p.id && l.estado == "Activo")
                            .OrderBy(l => l.fechaIngreso)
                            .Select(l => l.precioCosto)
                            .FirstOrDefault()
                    })
                    .OrderBy(p => p.nombre)
                    .ToList();

                if (!lista.Any())
                {
                    return Ok(new
                    {
                        mensaje = "No hay productos registrados",
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

        [HttpGet("gestionarVenta")]
        public IActionResult GestionarVenta([FromQuery] DateTime fechaInicio, DateTime fechaFin, int emp, int? usuario = null)
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
                       && (!usuario.HasValue || v.usuarioVenta == usuario.Value)
                    group new { vl, pl } by new
                    {
                        vd.id,
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
                        Id = g.Key.id,
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
                    x.Id,
                    x.FechaVenta,
                    x.Producto,
                    x.Vendedor
                })
                .Select(g => new
                {
                    Id = g.Key.Id,
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

                if (!lista.Any())
                {
                    return Ok(new
                    {
                        mensaje = "No hay ventas registradas",
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

        [HttpPost("eliminarVenta")]
        public async Task<IActionResult> EliminarVenta([FromBody] VentaDTO dto)
        {
            string nombreBase = string.Empty;

            try
            {
                var empresa = ContextGene.Empresas
                    .FirstOrDefault(e =>
                        e.id == dto.empresa &&
                        e.estado == Estados.Activo);

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

                var connectionTemplate =
                    configuracion["ConnectionStrings:ConnectionFactory"];

                var connectionString =
                    string.Format(connectionTemplate, empresa.conexionBD);

                using var ContextBD = ContextFact.Create(connectionString);

                using var transaction =
                    await ContextBD.Database.BeginTransactionAsync();

                try
                {
                    // Buscar el detalle que se quiere eliminar
                    var detalle = await ContextBD.VentaDetalles
                        .FirstOrDefaultAsync(x => x.id == dto.Id);

                    if (detalle == null)
                    {
                        await transaction.RollbackAsync();

                        return Ok(new
                        {
                            mensaje = "El detalle de la venta no existe",
                            exito = false
                        });
                    }

                    // Guardamos el ID de la venta
                    var ventaId = detalle.venta;

                    // Contar cuántos detalles tiene actualmente la venta
                    var cantidadDetalles = await ContextBD.VentaDetalles
                        .CountAsync(x => x.venta == ventaId);

                    // Buscar los lotes utilizados por ESTE detalle
                    var detallesLote = await ContextBD.VentaDetalleLotes
                        .Where(x => x.ventaDetalle == detalle.id)
                        .ToListAsync();

                    // Devolver las cantidades al inventario
                    foreach (var detalleLote in detallesLote)
                    {
                        var productoLote = await ContextBD.ProductoLotes
                            .FirstOrDefaultAsync(x =>
                                x.id == detalleLote.productoLote);

                        if (productoLote != null)
                        {
                            // Devolver cantidad al inventario
                            productoLote.cantidadDisponible += detalleLote.cantidad;

                            // Si estaba agotado, vuelve a estar activo
                            if (productoLote.estado == Estados.Agotado)
                            {
                                productoLote.estado = Estados.Activo;
                            }
                        }
                    }

                    // Eliminar los lotes asociados al detalle
                    ContextBD.VentaDetalleLotes
                        .RemoveRange(detallesLote);

                    // Eliminar el detalle
                    ContextBD.VentaDetalles.Remove(detalle);

                    // Si este era el único detalle de la venta,
                    // eliminar también la venta
                    if (cantidadDetalles == 1)
                    {
                        var venta = await ContextBD.Venta
                            .FirstOrDefaultAsync(x => x.id == ventaId);

                        if (venta != null)
                        {
                            ContextBD.Venta.Remove(venta);
                        }
                    }

                    // Guardar cambios
                    await ContextBD.SaveChangesAsync();

                    // Confirmar transacción
                    await transaction.CommitAsync();

                    return Ok(new
                    {
                        mensaje = cantidadDetalles == 1
                            ? "La venta se eliminó exitosamente"
                            : "El producto se eliminó de la venta exitosamente",

                        exito = true
                    });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
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

        [HttpPost("editarVenta")]
        public async Task<IActionResult> EditarVenta([FromBody] VentaDTO dto)
        {
            string nombreBase = string.Empty;

            try
            {
                var empresa = ContextGene.Empresas
                    .FirstOrDefault(e =>
                        e.id == dto.empresa &&
                        e.estado == Estados.Activo);

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

                var connectionTemplate =
                    configuracion["ConnectionStrings:ConnectionFactory"];

                var connectionString =
                    string.Format(connectionTemplate, empresa.conexionBD);

                using var ContextBD = ContextFact.Create(connectionString);

                using var transaction =
                    await ContextBD.Database.BeginTransactionAsync();

                try
                {
                    if (dto.Detalles == null || !dto.Detalles.Any())
                    {
                        await transaction.RollbackAsync();

                        return Ok(new
                        {
                            mensaje = "No se recibió el detalle de la venta",
                            exito = false
                        });
                    }

                    // Como solo se va a editar un producto,
                    // tomamos el detalle enviado.
                    var detalleDTO = dto.Detalles.First();

                    // Buscar el VentaDetalle
                    var detalle = await ContextBD.VentaDetalles
                        .FirstOrDefaultAsync(x => x.id == detalleDTO.Id);

                    if (detalle == null)
                    {
                        await transaction.RollbackAsync();

                        return Ok(new
                        {
                            mensaje = "El detalle de la venta no existe",
                            exito = false
                        });
                    }

                    // Actualizar únicamente los campos permitidos
                    detalle.total = detalleDTO.Total;
                    detalle.descuento = detalleDTO.Descuento;
                    detalle.comision = detalleDTO.Porcentaje;

                    await ContextBD.SaveChangesAsync();

                    await transaction.CommitAsync();

                    return Ok(new
                    {
                        mensaje = "El detalle de la venta se actualizó exitosamente",
                        exito = true
                    });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
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
    }
}
