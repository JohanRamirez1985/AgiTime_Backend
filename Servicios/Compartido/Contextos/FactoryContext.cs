using Microsoft.EntityFrameworkCore;

namespace Compartido.Contextos
{
    public class FactoryContext
    {
        public BDEmpresaContext Create(string connectionString)
        {
            var optionsBuilder = new DbContextOptionsBuilder<BDEmpresaContext>();
            optionsBuilder.UseSqlServer(connectionString);

            return new BDEmpresaContext(optionsBuilder.Options);
        }
    }
}
