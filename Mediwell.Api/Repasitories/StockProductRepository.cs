using Dapper;
using Mediwell.Api.Data;
using Mediwell.Api.Models;
using Npgsql;

namespace Mediwell.Api.Repasitories;

// Sklad hisobi: guruhlar va guruh dorilari
public class StockProductRepository
{
    private readonly DbConnectionFactory _db;

    public StockProductRepository(DbConnectionFactory db)
    {
        _db = db;
    }

    // ---------- Guruhlar ----------

    public async Task<IEnumerable<ProductGroup>> GetGroupsAsync()
    {
        using var conn = _db.Create();
        return await conn.QueryAsync<ProductGroup>(
            "SELECT * FROM product_group ORDER BY sort_order, id");
    }

    public async Task<long> CreateGroupAsync(SaveProductGroupRequest req)
    {
        using var conn = _db.Create();
        try
        {
            return await conn.ExecuteScalarAsync<long>(@"
                INSERT INTO product_group (code, name, sort_order, active)
                VALUES (@Code, @Name, @SortOrder, @Active)
                RETURNING id",
                new { Code = req.Code.Trim(), Name = req.Name.Trim(), req.SortOrder, req.Active });
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DuplicateKeyException($"Bu kodli guruh allaqachon bor: {req.Code.Trim()}", ex);
        }
    }

    public async Task<bool> UpdateGroupAsync(long id, SaveProductGroupRequest req)
    {
        using var conn = _db.Create();
        try
        {
            var n = await conn.ExecuteAsync(@"
                UPDATE product_group
                   SET code = @Code, name = @Name, sort_order = @SortOrder, active = @Active
                 WHERE id = @id",
                new { id, Code = req.Code.Trim(), Name = req.Name.Trim(), req.SortOrder, req.Active });
            return n > 0;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DuplicateKeyException($"Bu kodli guruh allaqachon bor: {req.Code.Trim()}", ex);
        }
    }

    // ---------- Dorilar ----------

    public async Task<IEnumerable<StockProduct>> GetProductsAsync(long? groupId, bool includeInactive)
    {
        var where = new List<string>();
        if (groupId is not null) where.Add("p.group_id = @groupId");
        if (!includeInactive) where.Add("p.active");

        var sql = @"
            SELECT p.*,
                   EXISTS (SELECT 1 FROM stock_movement m WHERE m.product_id = p.id) AS has_movements
              FROM stock_product p"
            + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "")
            + " ORDER BY p.group_id, p.sort_order, p.name";

        using var conn = _db.Create();
        return await conn.QueryAsync<StockProduct>(sql, new { groupId });
    }

    public async Task<long> CreateProductAsync(SaveStockProductRequest req)
    {
        using var conn = _db.Create();

        var groupExists = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM product_group WHERE id = @GroupId)", new { req.GroupId });
        if (!groupExists)
            throw new InvalidOperationException($"Guruh topilmadi: id={req.GroupId}");

        try
        {
            return await conn.ExecuteScalarAsync<long>(@"
                INSERT INTO stock_product (group_id, name, perech_price, sort_order, active)
                VALUES (@GroupId, @Name, @PerechPrice, @SortOrder, @Active)
                RETURNING id",
                new { req.GroupId, Name = req.Name.Trim(), req.PerechPrice, req.SortOrder, req.Active });
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DuplicateKeyException($"Bu guruhda shu nomli dori allaqachon bor: {req.Name.Trim()}", ex);
        }
    }

    public async Task<bool> UpdateProductAsync(long id, SaveStockProductRequest req)
    {
        using var conn = _db.Create();
        try
        {
            var n = await conn.ExecuteAsync(@"
                UPDATE stock_product
                   SET name = @Name, perech_price = @PerechPrice, sort_order = @SortOrder, active = @Active
                 WHERE id = @id",
                new { id, Name = req.Name.Trim(), req.PerechPrice, req.SortOrder, req.Active });
            return n > 0;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new DuplicateKeyException($"Bu guruhda shu nomli dori allaqachon bor: {req.Name.Trim()}", ex);
        }
    }

    // Harakati bo'lsa o'chirmaydi — faolsizlantiradi. null = topilmadi.
    // (O'chirilgan harakatlar ham FK orqali bog'langan, shuning uchun ular ham hisobga olinadi.)
    public async Task<string?> DeleteProductAsync(long id)
    {
        using var conn = _db.Create();

        var exists = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM stock_product WHERE id = @id)", new { id });
        if (!exists) return null;

        var hasMovements = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM stock_movement WHERE product_id = @id)", new { id });

        if (hasMovements)
        {
            await conn.ExecuteAsync("UPDATE stock_product SET active = false WHERE id = @id", new { id });
            return "deactivated";
        }

        try
        {
            await conn.ExecuteAsync("DELETE FROM stock_product WHERE id = @id", new { id });
            return "deleted";
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            // Shu orada harakat qo'shilib qolgan bo'lsa
            await conn.ExecuteAsync("UPDATE stock_product SET active = false WHERE id = @id", new { id });
            return "deactivated";
        }
    }
}
