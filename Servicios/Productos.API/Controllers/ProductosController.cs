using Compartido;
using Compartido.Constantes;
using Compartido.DTOs;
using Microsoft.AspNetCore.Mvc;
using Productos.API.Data;
using Productos.API.Data.Modelos;

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

                    var nuevoDeta = new DetalleProducto
                    {
                        producto = nuevoProd.id,
                        cantidadIngresada = dto.Cantidad,
                        cantidadStock = 0,
                        cantidadNueva = dto.Cantidad,
                        usuarioCreacion = dto.UsuarioCreacion,
                        fechaCreacion = DateTime.Now
                    };
                    ContextBD.DetalleProductos.Add(nuevoDeta);
                    ContextBD.SaveChanges();

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
                    //var nuevoDeta = new DetalleProducto
                    //{
                    //    producto = prod.id,
                    //    cantidadIngresada = dto.Cantidad,
                    //    cantidadStock = prod.stock,
                    //    cantidadNueva = dto.Cantidad + prod.stock,
                    //    usuarioCreacion = dto.UsuarioCreacion,
                    //    fechaCreacion = DateTime.Now
                    //};
                    //ContextBD.DetalleProductos.Add(nuevoDeta);

                    //prod.stock = dto.Cantidad + prod.stock;
                    //ContextBD.SaveChanges();
                }    
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

        [HttpPost("actualizarProducto")]
        public IActionResult ActulizarProducto([FromBody] ProductoDTO dto)
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
                        fechaCreacion = DateTime.Now
                    };
                    ContextBD.DetalleProductos.Add(nuevoDeta);

                    prod.stock = dto.Cantidad + prod.stock;
                    ContextBD.SaveChanges();

                    return Ok(new
                    {
                        mensaje = "Producto registrado exitosamente",
                        exito = true
                    });
                }
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

        [HttpPost("ventaProducto")]
        public IActionResult VentaProducto([FromBody] VentaDTO dto)
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

                var nuevo = new Ventum
                {
                    factura = dto.factura,
                    fechaVenta = DateTime.Now,
                    usuarioVenta = dto.usuarioVenta
                };

                ContextBD.Venta.Add(nuevo);

                foreach (var detalle in dto.Detalles)
                {
                    var nuevoDetalle = new VentaDetalle
                    {
                        venta = nuevo.id,
                        producto = detalle.Producto,
                        cantidad = detalle.Cantidad,
                        valor = detalle.Valor,
                        total = detalle.Cantidad * detalle.Valor
                    };

                    ContextBD.VentaDetalles.Add(nuevoDetalle);
                }

                ContextBD.SaveChanges();

                return Ok(new
                {
                    mensaje = "Venta registrada exitosamente",
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
    }
}
