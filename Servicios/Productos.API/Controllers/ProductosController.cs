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
                    var prod = ContextBD.Productos.Where(s => s.codigo == dto.Codigo).FirstOrDefault();

                    if (prod == null)
                    {
                        var nuevoProd = new Producto
                        {
                            Nombre = dto.Nombre,
                            codigo = dto.Codigo,
                            precio = dto.Precio,
                            stock = dto.Cantidad,
                            estado = Estados.Activo
                        };

                        ContextBD.Productos.Add(nuevoProd);
                        ContextBD.SaveChanges();

                        var nuevoDeta = new DetalleProducto
                        {
                            producto = nuevoProd.id,
                            cantidadIngresada = dto.Cantidad,
                            cantidadStock = 0,
                            cantidadNueva = dto.Cantidad,
                            usuarioCreacion = dto.UsuarioCreacion,
                            fechaCreacion = dto.FechaCreacion
                        };
                        ContextBD.DetalleProductos.Add(nuevoDeta);
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
                            mensaje = "El codigo de producto " + dto.Codigo + " ya existe.",
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
                        mensaje = "Producto " + dto .Nombre + " no encontrado.",
                        exito = false
                    });
                }
                else
                {
                    var nuevoDeta = new DetalleProducto
                    {
                        producto = prod.id,
                        cantidadIngresada = dto.Cantidad,
                        cantidadStock = prod.stock,
                        cantidadNueva = dto.Cantidad + prod.stock,
                        usuarioCreacion = dto.UsuarioCreacion,
                        fechaCreacion = dto.FechaCreacion
                    };
                    ContextBD.DetalleProductos.Add(nuevoDeta);

                    prod.stock = dto.Cantidad + prod.stock;
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
                    prod.Nombre = dto.Nombre;
                    prod.precio = dto.Precio;
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
                    var nuevo = new Ventum
                    {
                        factura = dto.factura,
                        fechaVenta = dto.fechaVenta,
                        usuarioVenta = dto.usuarioVenta
                    };

                    ContextBD.Venta.Add(nuevo);
                    ContextBD.SaveChanges();

                    foreach (var detalle in dto.Detalles)
                    {
                        var nuevoDetalle = new VentaDetalle
                        {
                            venta = nuevo.id,
                            producto = detalle.Producto,
                            cantidad = detalle.Cantidad,
                            valor = detalle.Valor,
                            total = detalle.Total
                        };

                        ContextBD.VentaDetalles.Add(nuevoDetalle);
                    }

                    ContextBD.SaveChanges();
                    transaction.Commit();

                    return Ok(new
                    {
                        mensaje = "Venta registrada exitosamente",
                        exito = true
                    });
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
    }
}
