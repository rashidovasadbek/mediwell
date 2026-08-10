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
}