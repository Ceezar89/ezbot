using Microsoft.EntityFrameworkCore;
using EzBot.Models;

namespace EzBot.Persistence;

public class EzBotDbContext(DbContextOptions<EzBotDbContext> options) : DbContext(options)
{
    public required DbSet<User> Users { get; set; }
    public required DbSet<ExchangeApiKey> ExchangeApiKeys { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure relationships
        modelBuilder.Entity<ExchangeApiKey>()
            .HasOne(apiKey => apiKey.User)
            .WithMany(user => user.ExchangeApiKeys)
            .HasForeignKey(apiKey => apiKey.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}