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

    // Real-time qoldiq (v_stock view'dan)
    public async Task<IEnumerable<StockItem>> GetStockAsync()
    {
        using var conn = _db.Create();
        const string sql = "SELECT * FROM v_stock";
        return await conn.QueryAsync<StockItem>(sql);
    }
}