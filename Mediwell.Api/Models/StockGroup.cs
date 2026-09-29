namespace Mediwell.Api.Models;

public class StockGroup
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public long ReceiptId { get; set; }
    public string Series { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public long TotalUnits { get; set; }      // jami dona (210)
    public long BoxCount { get; set; }         // butun karobka soni (5)
    public long OpenUnitCount { get; set; }    // ochilgan/ichki dona (10)
    public List<StockItem> Details { get; set; } = new();  // ichidagi 
    public long CashUnits { get; set; }      // naqd dona
    public long OfficialUnits { get; set; }  // rasmiy dona (Asl Belgida)
}