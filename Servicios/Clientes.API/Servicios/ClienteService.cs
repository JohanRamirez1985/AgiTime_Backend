using Clientes.API.Data;
using Clientes.API.Data.Modelos;
using Compartido.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;

namespace Clientes.API.Servicios
{
    public class ClientesService
    {
        private readonly FactoryContext ContextFact;
        private readonly IConfiguration configuracion;

        public ClientesService(FactoryContext fac, IConfiguration config)
        {
            ContextFact = fac;
            configuracion = config;
        }

        public object ProcesarUsuario(Cliente usuario, Empresa empresa, string nombreBase)
        {
            var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
            var connectionString = string.Format(connectionTemplate, nombreBase);

            var ContextBD = ContextFact.Create(connectionString);

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
                Documento = usuario.documento,
                Estado = usuario.estado,
                FechaCreacion = usuario.fechaCreacion,

                NombreGenero = usuario.generoNavigation?.nombre,
                NombreTipoDocumento = usuario.tipoDocumentoNavigation?.nombre,
                NombreRol = "Cliente",
            };

            var funcionalidades = ContextBD.FuncionalidadesSets
                    .Include(f => f.funcionalidadNavigation)
                    .Include(f => f.rolNavigation)
                    .Where(f => f.rolNavigation.nombre == "Cliente")
                    .ToList();

            var sedes = ContextBD.Sedes.OrderBy(s => s.Nombre);

            var emp = new { 
                Id = empresa.id,
                Nombre = empresa.nombre,
                Nit = empresa.nit,
                Logo = empresa.logo,
                Conexion = empresa.conexionBD,
                Estados = empresa.estado,
                Sedes = sedes
            };

            var datos = new
            {
                Usuario = user,
                Funcionaidades = funcionalidades,
                Empresa = emp
            };

            return datos;

        }

        //public object ProcesarFuncionabilidad(Cliente usuario, Empresa empresa, string nombreBase)
        //{
        //    var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
        //    var connectionString = string.Format(connectionTemplate, nombreBase);

        //    using var ContextBD = ContextFact.Create(connectionString);

        //    var funcionalidades = ContextBD.FuncionalidadesSets
        //            .Include(f => f.funcionalidadNavigation)
        //            .Include(f => f.rolNavigation)
        //            .Where(f => f.rolNavigation.nombre == "Cliente")
        //            .ToList();

        //    return funcionalidades;
        //}
    }
}
