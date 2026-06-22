using Compartido;
using Compartido.Constantes;
using Compartido.Data;
using Compartido.Data.Modelos;
using Compartido.DTOs;
using Compartido.Metodos;
using Microsoft.AspNetCore.Mvc;

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
                                lote.estado = "AGOTADO";

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
                            .Sum(l => (int?)l.cantidadDisponible) ?? 0
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
    }
}
