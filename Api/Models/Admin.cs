namespace Api.Models;

public class Admin : IHasTimestamps
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string CompanyId { get; set; }
    public Company? Company { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public string? RefreshTokenHash { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}