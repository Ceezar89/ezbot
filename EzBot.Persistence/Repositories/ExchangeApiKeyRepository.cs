using EzBot.Models.SQL;
using Microsoft.EntityFrameworkCore;
using EzBot.Common.Interfaces;

namespace EzBot.Persistence.Repositories;

public class ExchangeApiKeyRepository(EzBotDbContext dbContext, IEncryptionService encryptionService) : IExchangeApiKeyRepository
{
    private readonly EzBotDbContext _dbContext = dbContext;
    private readonly IEncryptionService _encryptionService = encryptionService;

    public async Task AddAsync(ExchangeApiKey apiKey)
    {
        apiKey.ApiKey = _encryptionService.Encrypt(apiKey.ApiKey);
        apiKey.ApiSecret = _encryptionService.Encrypt(apiKey.ApiSecret);
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
            apiKey.ApiKey = _encryptionService.Decrypt(apiKey.ApiKey);
            apiKey.ApiSecret = _encryptionService.Decrypt(apiKey.ApiSecret);
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
            key.ApiKey = _encryptionService.Decrypt(key.ApiKey);
            key.ApiSecret = _encryptionService.Decrypt(key.ApiSecret);
        }

        return apiKeys;
    }

    // Example of how to speedup read queries by 3x using precompiled queries (only 10-20% slower than Dapper)
    public async Task<ExchangeApiKey?> GetByIdAsyncFast(Guid id)
    {
        return await GetByIdAsync_PreCompiled(_dbContext, id);
    }

    // precomiled query example
    public static readonly Func<EzBotDbContext, Guid, Task<ExchangeApiKey?>> GetByIdAsync_PreCompiled =
        EF.CompileAsyncQuery((EzBotDbContext dbContext, Guid id) =>
            dbContext.ExchangeApiKeys
                .AsNoTracking()
                .Include(apiKey => apiKey.User)
                .FirstOrDefault(apiKey => apiKey.Id == id));

}