using Microsoft.EntityFrameworkCore;

namespace Usuarios.API.Data
{
    public class FactoryContext
    {
        public UsuariosContext Create(string connectionString)
        {
            var optionsBuilder = new DbContextOptionsBuilder<UsuariosContext>();
            optionsBuilder.UseSqlServer(connectionString);

            return new UsuariosContext(optionsBuilder.Options);
        }
    }
}
