namespace Mediwell.Api.Models;

public class Receipt
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public string Series { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public string? Supplier { get; set; }
    public string? EntryMethod { get; set; }
    public DateOnly ReceivedAt { get; set; }
    public string? Note { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}