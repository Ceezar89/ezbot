// In EzBot.Persistence/Repositories/IExchangeApiKeyRepository.cs
using EzBot.Models.SQL;

namespace EzBot.Persistence.Repositories;

public interface IExchangeApiKeyRepository
{
    Task AddAsync(ExchangeApiKey apiKey);
    Task<ExchangeApiKey?> GetByIdAsync(Guid id);
    Task<IEnumerable<ExchangeApiKey>> GetAllByUserIdAsync(Guid userId);
}