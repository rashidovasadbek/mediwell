namespace Mediwell.Api.Models;

public class Product
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public int NominalCapacity { get; set; }
    public string? Gtin { get; set; }
    public bool Active { get; set; }
    public DateTime CreatedAt { get; set; }
}