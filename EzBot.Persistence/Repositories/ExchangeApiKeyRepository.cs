using EzBot.Models;
using Microsoft.EntityFrameworkCore;

namespace EzBot.Persistence.Repositories;

public class ExchangeApiKeyRepository(EzBotDbContext dbContext) : IExchangeApiKeyRepository
{
    private readonly EzBotDbContext _dbContext = dbContext;

    public async Task AddAsync(ExchangeApiKey apiKey)
    {
        apiKey.ApiKey = Common.Encryption.Encrypt(apiKey.ApiKey);
        apiKey.ApiSecret = Common.Encryption.Encrypt(apiKey.ApiSecret);
        await _dbContext.ExchangeApiKeys.AddAsync(apiKey);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<ExchangeApiKey?> GetByIdAsync(Guid id)
    {
        var apiKey = await _dbContext.ExchangeApiKeys
            .AsNoTracking()
            .Include(apiKey => apiKey.User)
            .FirstOrDefaultAsync(apiKey => apiKey.Id == id);

        if (apiKey != null)
        {
            apiKey.ApiKey = Common.Encryption.Decrypt(apiKey.ApiKey);
            apiKey.ApiSecret = Common.Encryption.Decrypt(apiKey.ApiSecret);
        }

        return apiKey;
    }

    public async Task<IEnumerable<ExchangeApiKey>> GetAllByUserIdAsync(Guid userId)
    {
        var apiKeys = await _dbContext.ExchangeApiKeys
            .AsNoTracking()
            .Where(apiKey => apiKey.UserId == userId)
            .ToListAsync();

        foreach (var key in apiKeys)
        {
            key.ApiKey = Common.Encryption.Decrypt(key.ApiKey);
            key.ApiSecret = Common.Encryption.Decrypt(key.ApiSecret);
        }

        return apiKeys;
    }

}