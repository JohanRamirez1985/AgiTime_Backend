using Microsoft.EntityFrameworkCore;
using Clientes.API.Models;

namespace Clientes.API.Data
{
    public class ClientesContext : DbContext
    {
        public ClientesContext(DbContextOptions<ClientesContext> options) : base(options) { }

        public virtual DbSet<Ciudad> Ciudad { get; set; } = null!;
        public virtual DbSet<Cliente> Clientes { get; set; } = null!;
        public virtual DbSet<Empresa> Empresas { get; set; } = null!;
        public virtual DbSet<EmpresaCliente> EmpresaClientes { get; set; } = null!;
        public virtual DbSet<Sede> Sedes { get; set; } = null!;
        public virtual DbSet<TipoDocumento> TipoDocumentos { get; set; } = null!;

    }
}
