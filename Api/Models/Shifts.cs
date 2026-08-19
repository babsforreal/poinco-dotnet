namespace Api.Models;

public class Shift
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string CompanyId { get; set; }
    public Company? Company { get; set; }
    public required string Start { get; set; } // format "HH:mm"
    public required string End { get; set; }
}