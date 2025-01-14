using System.ComponentModel.DataAnnotations;

namespace EzBot.Models.SQL;

public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public required string Username { get; set; }

    public ICollection<ExchangeApiKey> ExchangeApiKeys { get; set; } = [];
}