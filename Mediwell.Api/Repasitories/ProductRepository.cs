using Dapper;
using Mediwell.Api.Data;
using Mediwell.Api.Models;

namespace Mediwell.Api.Repasitories;

public class ProductRepository
{
    private readonly DbConnectionFactory _db;

    public ProductRepository(DbConnectionFactory db)
    {
        _db = db;
    }
    
    // Hamma preparatlar ro'yxati
    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        using var conn = _db.Create();
        const string sql = "SELECT * FROM product ORDER BY name";
        return await conn.QueryAsync<Product>(sql);
    }

    // Yangi preparat qo'shish, yangi id qaytaradi
    public async Task<long> CreateAsync(Product p)
    {
        using var conn = _db.Create();
        const string sql = @"
            INSERT INTO product (name, manufacturer, nominal_capacity, gtin)
            VALUES (@Name, @Manufacturer, @NominalCapacity, @Gtin)
            RETURNING id";
        return await conn.ExecuteScalarAsync<long>(sql, p);
    }
}