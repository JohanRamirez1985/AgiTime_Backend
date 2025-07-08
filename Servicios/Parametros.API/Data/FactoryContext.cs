using Microsoft.EntityFrameworkCore;

namespace Parametros.API.Data
{
    public class FactoryContext
    {
        public ParametrosContext Create(string connectionString)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ParametrosContext>();
            optionsBuilder.UseSqlServer(connectionString);

            return new ParametrosContext(optionsBuilder.Options);
        }
    }
}
