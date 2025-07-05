using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Compartido.Contextos
{
    public class BDEmpresaContext : DbContext
    {
        public BDEmpresaContext(DbContextOptions<BDEmpresaContext> options) : base(options) { }

        public DbSet<Log> Logs { get; set; }
    }
}
