using Compartido.DTOs;
using Microsoft.EntityFrameworkCore;
using Compartido.Data;
using Compartido.Data.Modelos;
using Microsoft.AspNetCore.Mvc;

namespace Usuarios.API.Servicios
{
    public class UsuarioService
    {
        private readonly FactoryContext ContextFact;
        private readonly IConfiguration configuracion;

        public UsuarioService(FactoryContext fac, IConfiguration config)
        {
            ContextFact = fac;
            configuracion = config;
        }

        public object ProcesarUsuario(Usuario usuario, Empresa empresa, string nombreBase)
        {
            var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
            var connectionString = string.Format(connectionTemplate, nombreBase);

            var ContextBD = ContextFact.Create(connectionString);

            var primerRol = usuario.idRols.FirstOrDefault();

            var user = new
            {
                Id = usuario.id,
                Nombre = usuario.nombre,
                Correo = usuario.correo,
                Password = usuario.password,
                Telefono = usuario.telefono,
                FechaNacimiento = usuario.fechaNacimiento,
                Genero = usuario.genero,
                TipoDocumento = usuario.tipoDocumento,
                Sede = usuario.sede,
                Documento = usuario.documento,
                Estado = usuario.estado,
                FechaCreacion = usuario.fechaCreacion,

                NombreGenero = usuario.generoNavigation?.nombre,
                NombreTipoDocumento = usuario.tipoDocumentoNavigation?.nombre,
                NombreSede = usuario.sedeNavigation?.Nombre,

                rol = primerRol?.id ?? 0,
                NombreRol = primerRol?.nombre
            };

            var funcionalidades = ContextBD.FuncionalidadesSets
                    .Include(fs => fs.funcionalidadNavigation)
                    .Where(fs => fs.rol == primerRol.id && fs.estado == "Activo")
                    .Select(fs => new
                    {
                        fs.funcionalidadNavigation.codigo
                    })
                    .ToList();

            var sedes = ContextBD.Sedes.OrderBy(s => s.Nombre);

            var emp = new
            {
                Id = empresa.id,
                Nombre = empresa.nombre,
                Nit = empresa.nit,
                Logo = empresa.logo,
                Conexion = empresa.conexionBD,
                Estados = empresa.estado,
                Servicio = empresa.servicio,
                Sedes = sedes,
                ordenes = empresa.ordenes,
                multiple = empresa.recursoMultiple
            };

            var datos = new
            {
                Usuario = user,
                Funcionalidades = funcionalidades,
                Empresa = emp
            };

            return datos;
        }

        //public object ProcesarFuncionabilidad(Usuario usuario, string nombreBase)
        //{
        //    var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
        //    var connectionString = string.Format(connectionTemplate, nombreBase);

        //    using var ContextBD = ContextFact.Create(connectionString);

        //    var primerRol = usuario.idRols.FirstOrDefault();

        //    var funcionalidades = ContextBD.FuncionalidadesSets
        //            .Include(fs => fs.funcionalidadNavigation)
        //            .Where(fs => fs.rol == primerRol.id && fs.estado == "Activo")
        //            .Select(fs => new
        //            {
        //                fs.funcionalidadNavigation.codigo
        //            })
        //            .ToList();

        //    return funcionalidades;
        //}
    }
}
