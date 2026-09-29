namespace Mediwell.Api.Models;

public class UnitCode
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public long ReceiptId { get; set; }
    public string PhysicalStatus { get; set; } = "in_stock";
    public string? SalesChannel { get; set; }
    public DateTime CreatedAt { get; set; }
}