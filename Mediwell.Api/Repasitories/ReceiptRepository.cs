using Dapper;
using Mediwell.Api.Data;
using Mediwell.Api.Models;
using Npgsql;

namespace Mediwell.Api.Repasitories;

public class ReceiptRepository
{
    private readonly DbConnectionFactory _db;

    public ReceiptRepository(DbConnectionFactory db)
    {
        _db = db;
    }

    // Kirim + agregat kodlarni bitta tranzaksiyada qo'shish
    public async Task<long> CreateAsync(CreateReceiptRequest req)
    {
        await using var conn = (NpgsqlConnection)_db.Create();
        await conn.OpenAsync();
        // Commit chaqirilmasa, Dispose avtomatik ROLLBACK qiladi.
        await using var tx = await conn.BeginTransactionAsync();

        // 1. Preparatning nominal sig'imini olamiz (default actual_count uchun)
        var nominal = await conn.ExecuteScalarAsync<int?>(
            "SELECT nominal_capacity FROM product WHERE id = @ProductId",
            new { req.ProductId }, tx);

        if (nominal is null)
            throw new InvalidOperationException($"Preparat topilmadi: id={req.ProductId}");

        // nominal_capacity = 0 bo'lsa, ActualCount ko'rsatilmagan kodlar jimgina
        // 0 dona bo'lib yozilardi — qoldiq noto'g'ri chiqadi.
        if (nominal.Value <= 0 && req.Codes.Any(c => c.ActualCount is null))
            throw new InvalidOperationException(
                $"Preparatning nominal_capacity qiymati {nominal.Value}. " +
                "ActualCount ko'rsatilmagan kodlar uchun miqdorni aniqlab bo'lmaydi.");

        // Xato qaysi kodda yuz berganini bilish uchun kuzatib boramiz.
        string? currentCode = null;

        try
        {
            // 2. Kirim yozuvini yaratamiz
            const string receiptSql = @"
                INSERT INTO receipt (product_id, series, expiry_date, supplier, entry_method, note)
                VALUES (@ProductId, @Series, @ExpiryDate, @Supplier, @EntryMethod, @Note)
                RETURNING id";

            var receiptId = await conn.ExecuteScalarAsync<long>(receiptSql, new
            {
                req.ProductId,
                req.Series,
                req.ExpiryDate,
                req.Supplier,
                req.EntryMethod,
                req.Note
            }, tx);

            // 3. Har bir agregat kodni qo'shamiz
            const string aggSql = @"
                INSERT INTO aggregate (code, receipt_id, actual_count, arrival_status, arrival_firm)
                VALUES (@Code, @ReceiptId, @ActualCount, @ArrivalStatus, @ArrivalFirm)";

            foreach (var c in req.Codes)
            {
                currentCode = c.Code;

                await conn.ExecuteAsync(aggSql, new
                {
                    c.Code,
                    ReceiptId = receiptId,
                    ActualCount = c.ActualCount ?? nominal.Value,  // bo'sh bo'lsa nominal
                    c.ArrivalStatus,
                    c.ArrivalFirm
                }, tx);
            }

            await tx.CommitAsync();
            return receiptId;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // Jadval nomi schema'dan aniq ma'lum; cheklov nomini Postgres o'zi
            // generatsiya qiladi, shuning uchun unga tayanmaymiz.
            throw new DuplicateKeyException(ex.TableName switch
            {
                "receipt" =>
                    $"Bu preparat uchun '{req.Series}' seriyasi allaqachon mavjud.",
                "aggregate" =>
                    $"Agregat kod allaqachon bazada mavjud: '{currentCode}'.",
                _ => $"Takrorlanuvchi qiymat: {ex.ConstraintName ?? ex.MessageText}"
            }, ex);
        }
    }

    // Hamma kirimlar ro'yxati
    public async Task<IEnumerable<Receipt>> GetAllAsync()
    {
        using var conn = _db.Create();
        const string sql = "SELECT * FROM receipt ORDER BY received_at DESC, id DESC";
        return await conn.QueryAsync<Receipt>(sql);
    }

    // Bitta kirim — 201 Created dagi Location shu yerga ishora qiladi.
    public async Task<Receipt?> GetByIdAsync(long id)
    {
        using var conn = _db.Create();
        const string sql = "SELECT * FROM receipt WHERE id = @id";
        return await conn.QuerySingleOrDefaultAsync<Receipt>(sql, new { id });
    }
}
