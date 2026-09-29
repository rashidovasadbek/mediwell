using Dapper;
using Mediwell.Api.Data;
using Mediwell.Api.Models;

namespace Mediwell.Api.Repasitories;

public class StockRepository
{
    private readonly DbConnectionFactory _db;

    public StockRepository(DbConnectionFactory db)
    {
        _db = db;
    }

   // Qoldiq — preparat/seriya bo'yicha jamlangan (bosganda ochiladi)
        public async Task<IEnumerable<StockGroup>> GetGroupedStockAsync()
        {
            var flat = (await GetStockAsync()).ToList();

            var groups = flat
                .GroupBy(s => new { s.ProductId, s.ReceiptId, s.Series, s.ExpiryDate, s.ProductName })
                .Select(g => new StockGroup
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.ProductName,
                    ReceiptId = g.Key.ReceiptId,
                    Series = g.Key.Series,
                    ExpiryDate = g.Key.ExpiryDate,
                    TotalUnits = g.Sum(x => x.UnitCount),
                    CashUnits = g.Where(x => x.OfficialStatus == "cash").Sum(x => x.UnitCount),
                    OfficialUnits = g.Where(x => x.OfficialStatus == "official").Sum(x => x.UnitCount),
                    BoxCount = g.Sum(x => x.BoxCount),
                    OpenUnitCount = g.Where(x => x.ArrivalStatus == "unit").Sum(x => x.UnitCount),
                    Details = g.ToList()
                })
                .OrderBy(x => x.ProductName)
                .ThenBy(x => x.Series)
                .ToList();

            return groups;
        }
        
        // Qoldiq: agregat (butun karobka) + ichki kodlar (alohida dona) — C# da qo'shiladi
        public async Task<IEnumerable<StockItem>> GetStockAsync()
        {
            using var conn = _db.Create();

            // 1. Agregatlardan qoldiq — naqd/rasmiy ajratib
            const string aggSql = @"
                SELECT
                    r.product_id      AS ProductId,
                    p.name            AS ProductName,
                    r.id              AS ReceiptId,
                    r.series          AS Series,
                    r.expiry_date     AS ExpiryDate,
                    a.arrival_status  AS ArrivalStatus,
                    a.official_status AS OfficialStatus,
                    COUNT(*)          AS BoxCount,
                    SUM(a.actual_count) AS UnitCount
                FROM aggregate a
                JOIN receipt r ON r.id = a.receipt_id
                JOIN product p ON p.id = r.product_id
                WHERE a.physical_status = 'in_stock'
                GROUP BY r.product_id, p.name, r.id, r.series, r.expiry_date, a.arrival_status, a.official_status";

            var aggRows = (await conn.QueryAsync<StockItem>(aggSql)).ToList();

            // 2. Ichki kodlardan qoldiq — naqd/rasmiy ajratib
            const string unitSql = @"
                SELECT
                    r.product_id      AS ProductId,
                    p.name            AS ProductName,
                    r.id              AS ReceiptId,
                    r.series          AS Series,
                    r.expiry_date     AS ExpiryDate,
                    u.official_status AS OfficialStatus,
                    COUNT(*)          AS UnitCount
                FROM unit_code u
                JOIN receipt r ON r.id = u.receipt_id
                JOIN product p ON p.id = r.product_id
                WHERE u.physical_status = 'in_stock'
                GROUP BY r.product_id, p.name, r.id, r.series, r.expiry_date, u.official_status";

            var unitRows = (await conn.QueryAsync<StockItem>(unitSql)).ToList();

            foreach (var u in unitRows)
            {
                u.ArrivalStatus = "unit";
                u.BoxCount = 0;
            }

            return aggRows.Concat(unitRows)
                .OrderBy(s => s.ProductName)
                .ThenBy(s => s.Series)
                .ThenBy(s => s.OfficialStatus)
                .ThenBy(s => s.ArrivalStatus)
                .ToList();
        }
}