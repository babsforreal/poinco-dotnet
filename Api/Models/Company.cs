namespace Api.Models;

public class Company : IHasTimestamps
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string Timezone { get; set; } = "America/Toronto";
    public int ClockOffsetMinutes { get; set; } = 0;
    public List<Shift> Shifts { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}