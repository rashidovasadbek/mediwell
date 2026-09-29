using Dapper;
using Mediwell.Api.Data;
using Mediwell.Api.Models;

namespace Mediwell.Api.Repasitories;

public class AggregateRepository
{
    private readonly DbConnectionFactory _db;

    public AggregateRepository(DbConnectionFactory db)
    {
        _db = db;
    }
    
    public async Task<IEnumerable<Aggregate>> GetByReceiptAsync(long receiptId)
    {
        using var conn = _db.Create();
        const string sql = "SELECT * FROM aggregate WHERE receipt_id = @receiptId ORDER BY id";
        return await conn.QueryAsync<Aggregate>(sql, new { receiptId });
    }

    // Kod bo'yicha bitta agregat
    public async Task<Aggregate?> GetByCodeAsync(string code)
    {
        using var conn = _db.Create();
        const string sql = "SELECT * FROM aggregate WHERE code = @code";
        return await conn.QueryFirstOrDefaultAsync<Aggregate>(sql, new { code });
    }

    public async Task<bool> MarkArrivedAsync(long aggregateId, string arrivalFirm, long? userId)
    {
        using var conn = (Npgsql.NpgsqlConnection)_db.Create();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();

        try
        {
            if (userId == 0) userId = null;
            // 1. Hozirgi holatni olamiz (audit uchun)
            var current = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT arrival_status FROM aggregate WHERE id = @aggregateId",
                new { aggregateId }, tx);

            if (current is null)
                return false; // agregat topilmadi

            // 2. Holatni o'zgartiramiz
            await conn.ExecuteAsync(@"
                UPDATE aggregate
                SET arrival_status = 'arrived', arrival_firm = @arrivalFirm
                WHERE id = @aggregateId",
                new { aggregateId, arrivalFirm }, tx);

            // 3. Audit logga yozamiz
            await conn.ExecuteAsync(@"
                INSERT INTO code_event (aggregate_id, event_type, old_value, new_value, reason, performed_by)
                VALUES (@aggregateId, 'arrival_changed', @old, 'arrived', @reason, @userId)",
                new { aggregateId, old = current, reason = $"Prixod qilindi: {arrivalFirm}", userId }, tx);

            await tx.CommitAsync();
            return true;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
    public async Task<bool> CorrectCountAsync(long aggregateId, int newCount, long? userId)
    {
        if (userId == 0) userId = null;

        using var conn = (Npgsql.NpgsqlConnection)_db.Create();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();

        try
        {
            var oldCount = await conn.QueryFirstOrDefaultAsync<int?>(
                "SELECT actual_count FROM aggregate WHERE id = @aggregateId",
                new { aggregateId }, tx);

            if (oldCount is null)
                return false;

            await conn.ExecuteAsync(
                "UPDATE aggregate SET actual_count = @newCount WHERE id = @aggregateId",
                new { aggregateId, newCount }, tx);

            await conn.ExecuteAsync(@"
                INSERT INTO code_event (aggregate_id, event_type, old_value, new_value, reason, performed_by)
                VALUES (@aggregateId, 'count_corrected', @old, @new, @reason, @userId)",
                new {
                    aggregateId,
                    old = oldCount.ToString(),
                    @new = newCount.ToString(),
                    reason = $"Buzilgan karobka tuzatildi: {oldCount} → {newCount}",
                    userId
                }, tx);

            await tx.CommitAsync();
            return true;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
    
    // Mediwellga prixod: skanerlangan kod naqddan rasmiyga o'tadi + audit
        // Kod agregat (20 belgi) yoki ichki (31 belgi) bo'lishi mumkin
    public async Task<(bool ok, string message)> MediwellArriveAsync(string code, long? userId)
        {
            if (userId == 0) userId = null;
            code = code.Trim();

            using var conn = (Npgsql.NpgsqlConnection)_db.Create();
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();

            try
            {
                // Kod agregatmi yoki ichkimi — formatidan aniqlaymiz
                bool isAggregate = code.Length == 20 && code.StartsWith("00");
                bool isUnit = code.Length == 31 && code.StartsWith("01");

                if (!isAggregate && !isUnit)
                    return (false, $"Noto'g'ri kod formati: {code}");

                if (isAggregate)
                {
                    // Agregatni topamiz
                    var agg = await conn.QueryFirstOrDefaultAsync<(long id, string status)>(
                        "SELECT id, official_status FROM aggregate WHERE code = @code",
                        new { code }, tx);

                    if (agg.id == 0)
                        return (false, $"Agregat topilmadi: {code}");

                    if (agg.status == "official")
                        return (false, $"Bu agregat allaqachon Mediwellga prixod qilingan");

                    // naqd → rasmiy
                    await conn.ExecuteAsync(
                        "UPDATE aggregate SET official_status = 'official' WHERE id = @id",
                        new { id = agg.id }, tx);

                    await conn.ExecuteAsync(@"
                        INSERT INTO code_event (aggregate_id, event_type, old_value, new_value, reason, performed_by)
                        VALUES (@aggId, 'status_changed', 'cash', 'official', 'Mediwellga prixod (agregat)', @userId)",
                        new { aggId = agg.id, userId }, tx);
                }
                else // ichki kod
                {
                    var unit = await conn.QueryFirstOrDefaultAsync<(long id, string status)>(
                        "SELECT id, official_status FROM unit_code WHERE code = @code",
                        new { code }, tx);

                    if (unit.id == 0)
                        return (false, $"Ichki kod topilmadi: {code}");

                    if (unit.status == "official")
                        return (false, $"Bu ichki kod allaqachon Mediwellga prixod qilingan");

                    await conn.ExecuteAsync(
                        "UPDATE unit_code SET official_status = 'official' WHERE id = @id",
                        new { id = unit.id }, tx);
                    // ichki kod uchun audit — code_event aggregate_id talab qiladi,
                    // shuning uchun hozircha ichki kod auditini o'tkazib yuboramiz (keyin unit audit qo'shamiz)
                }

                await tx.CommitAsync();
                return (true, isAggregate ? "Agregat Mediwellga prixod qilindi" : "Ichki kod Mediwellga prixod qilindi");
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
    // Markirovka kodlarni holat bo'yicha ko'rish (naqd yoki rasmiy)
    // officialStatus: "cash" (naqd) yoki "official" (rasmiy) yoki null (hammasi)
    public async Task<object> GetCodesByStatusAsync(string? officialStatus)
    {
        using var conn = _db.Create();

        var filter = officialStatus is null ? "" : "AND a.official_status = @officialStatus";
        var unitFilter = officialStatus is null ? "" : "AND u.official_status = @officialStatus";

        // Agregat kodlar
        var aggSql = $@"
                SELECT a.code                AS Code,
                       a.official_status     AS OfficialStatus,
                       a.actual_count        AS ActualCount,
                       p.name                AS ProductName,
                       r.series              AS Series,
                       'agregat'             AS CodeType
                FROM aggregate a
                JOIN receipt r ON r.id = a.receipt_id
                JOIN product p ON p.id = r.product_id
                WHERE a.physical_status = 'in_stock' {filter}
                ORDER BY p.name, r.series, a.code";
        var agg = await conn.QueryAsync<CodeItem>(aggSql, new { officialStatus });

        // Ichki kodlar — har biri 1 dona
        var unitSql = $@"
                SELECT u.code                AS Code,
                       u.official_status     AS OfficialStatus,
                       1                     AS ActualCount,
                       p.name                AS ProductName,
                       r.series              AS Series,
                       'ichki'               AS CodeType
                FROM unit_code u
                JOIN receipt r ON r.id = u.receipt_id
                JOIN product p ON p.id = r.product_id
                WHERE u.physical_status = 'in_stock' {unitFilter}
                ORDER BY p.name, r.series, u.code";
        var unit = await conn.QueryAsync<CodeItem>(unitSql, new { officialStatus });

        return new { agregat = agg, ichki = unit };
    }
    
    
}