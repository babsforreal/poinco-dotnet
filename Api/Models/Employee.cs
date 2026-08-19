namespace Api.Models;

public enum PunchType
{
    In,
    Break,
    Resume,
    Out
}

public class Employee : IHasTimestamps
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string CompanyId { get; set; }
    public Company? Company { get; set; }
    public required string Name { get; set; }
    public string? EmployeeNumber { get; set; }
    public string? CardUid { get; set; }
    public required string Pin { get; set; }
    public string? PhotoUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public List<Punch> Punches { get; set; } = new();
    public PunchType? LastPunchType { get; set; }
    public DateTimeOffset? LastPunchedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; }
}