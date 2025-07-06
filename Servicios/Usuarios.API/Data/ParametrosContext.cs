using Microsoft.EntityFrameworkCore;
using Usuarios.API.Models;

namespace Usuarios.API.Data
{
    public class ParametrosContext : DbContext
    {
        public ParametrosContext(DbContextOptions<ParametrosContext> options) : base(options) { }

        public virtual DbSet<Ciudad> Ciudad { get; set; } = null!;
        public virtual DbSet<TipoDocumento> TipoDocumentos { get; set; } = null!;
    }
}
