using Microsoft.EntityFrameworkCore;

namespace Clientes.API.Data
{
    public class FactoryContext
    {
        public ModeloContext Create(string connectionString)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ModeloContext>();
            optionsBuilder.UseSqlServer(connectionString);

            return new ModeloContext(optionsBuilder.Options);
        }
    }
}
