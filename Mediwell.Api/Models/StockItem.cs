namespace Mediwell.Api.Models;

public class StockItem
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public long ReceiptId { get; set; }
    public string Series { get; set; } = string.Empty;
    // v_stock.expiry_date — DATE ustuni. Npgsql uni DateOnly qilib qaytaradi,
    // DateTime qilib qo'ysak Dapper o'gira olmaydi (DateOnly : IConvertible emas).
    public DateOnly ExpiryDate { get; set; }
    public string ArrivalStatus { get; set; } = string.Empty;
    public long BoxCount { get; set; }
    public long UnitCount { get; set; }
}