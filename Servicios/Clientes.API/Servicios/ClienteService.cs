using Clientes.API.Data;
using Clientes.API.Data.Modelos;
using Compartido.DTOs;
using Microsoft.EntityFrameworkCore;
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

        public UsuarioDTO ProcesarUsuario(Cliente usuario)
        {
            return new UsuarioDTO
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
            };
        }

        public object ProcesarFuncionabilidad(Cliente usuario, string nombreBase)
        {
            var connectionTemplate = configuracion["ConnectionStrings:ConnectionFactory"];
            var connectionString = string.Format(connectionTemplate, nombreBase);

            using var ContextBD = ContextFact.Create(connectionString);

            var funcionalidades = ContextBD.FuncionalidadesSets
                    .Include(f => f.funcionalidadNavigation)
                    .Include(f => f.rolNavigation)
                    .Where(f => f.rolNavigation.nombre == "Cliente")
                    .ToList();

            return funcionalidades;
        }
    }
}
