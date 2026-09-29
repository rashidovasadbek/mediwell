namespace Mediwell.Api.Models;

// Markirovka kod ro'yxati uchun (agregat va ichki kodlar bir xil ko'rinishda).
// Dinamik qator qaytarsak, Postgres alias'larni kichik harfga aylantiradi va
// JSON kalitlari (productname, actualcount) frontend kutgan camelCase'ga mos kelmaydi.
public class CodeItem
{
    public string Code { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Series { get; set; } = string.Empty;
    public int ActualCount { get; set; }
    public string OfficialStatus { get; set; } = "cash";
    // "agregat" yoki "ichki"
    public string CodeType { get; set; } = string.Empty;
}
