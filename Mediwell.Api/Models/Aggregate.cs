namespace Mediwell.Api.Models;

public class Aggregate
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public long ReceiptId { get; set; }
    public int ActualCount { get; set; }
    public string ArrivalStatus { get; set; } = "queued";
    public string? ArrivalFirm { get; set; }
    public string PhysicalStatus { get; set; } = "in_stock";
    public string? SalesChannel { get; set; }
    public bool IsOpened { get; set; }
    public DateTime CreatedAt { get; set; }
}