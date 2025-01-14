using EzBot.Models.SQL;
using EzBot.Persistence.Repositories;

namespace EzBot.Services;

public class DbService(ExchangeApiKeyRepository apiKeyRepository)
{
    private readonly ExchangeApiKeyRepository _apiKeyRepository = apiKeyRepository;

    public async Task AddApiKeyAsync(ExchangeApiKey apiKey)
    {
        // Perform any business logic or validation
        await _apiKeyRepository.AddAsync(apiKey);
    }

    public async Task<ExchangeApiKey?> GetApiKeyAsync(Guid id)
    {
        return await _apiKeyRepository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<ExchangeApiKey>> GetUserApiKeysAsync(Guid userId)
    {
        return await _apiKeyRepository.GetAllByUserIdAsync(userId);
    }

    // Additional service methods as needed
}