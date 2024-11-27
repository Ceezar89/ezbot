using System.ComponentModel.DataAnnotations;

namespace EzBot.Models;

public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public required string Username { get; set; }

    // Navigation property
    public ICollection<ExchangeApiKey> ExchangeApiKeys { get; set; } = [];
}