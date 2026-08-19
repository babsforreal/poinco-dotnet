namespace Api.Models;

public class Punch : IHasTimestamps
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string CompanyId { get; set; }
    public required string EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public required PunchType Type { get; set; }
    public DateTimeOffset PunchedAt { get; set; }
    public DateTimeOffset? RawPunchedAt { get; set; }
    public bool IsEdited { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}