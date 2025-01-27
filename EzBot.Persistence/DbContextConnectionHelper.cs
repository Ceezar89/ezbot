
namespace EzBot.Persistence;
public static class DbContextConnectionHelper
{
    public static string GetSqliteConnectionString()
    {
        var dbDirectory = Path.Combine(Environment.CurrentDirectory, "..", "Data");
        Directory.CreateDirectory(dbDirectory); // Ensure 'Data' folder is created

        var dbPath = Path.Combine(dbDirectory, "ezbot.db");
        return $"Data Source={dbPath}";
    }
}
