using Microsoft.EntityFrameworkCore;

namespace Clientes.API.Data
{
    public class FactoryContext
    {
        public ClientesContext Create(string connectionString)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ClientesContext>();
            optionsBuilder.UseSqlServer(connectionString);

            return new ClientesContext(optionsBuilder.Options);
        }
    }
}
