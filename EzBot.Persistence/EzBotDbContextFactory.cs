using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EzBot.Persistence;
public class EzBotDbContextFactory : IDesignTimeDbContextFactory<EzBotDbContext>
{
    public EzBotDbContext CreateDbContext(string[] args)
    {
        // Create the options builder
        var builder = new DbContextOptionsBuilder<EzBotDbContext>();

        // Reuse the same logic from the helper
        string connectionString = DbContextConnectionHelper.GetSqliteConnectionString();

        builder.UseSqlite(connectionString);

        // Create and return the context
        return new EzBotDbContext(builder.Options);
    }
}
