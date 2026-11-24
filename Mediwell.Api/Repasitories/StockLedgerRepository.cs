using System.Data;
using System.Globalization;
using Dapper;
using Mediwell.Api.Data;
using Mediwell.Api.Exceptions;
using Mediwell.Api.Models;
using Npgsql;

namespace Mediwell.Api.Repasitories;

// Sklad hisobi: harakatlar jurnali va qoldiq.
// Qoldiq jadvalda saqlanmaydi — har doim stock_movement'dan hisoblanadi.
// Minus tekshiruvi: dori bo'yicha advisory lock → hozirgi (yakuniy) qoldiq + harakat ta'siri ≥ 0.
public class StockLedgerRepository
{
    private const int LockClass = 7101; // pg_advisory_xact_lock(7101, product_id)

    private readonly DbConnectionFactory _db;

    public StockLedgerRepository(DbConnectionFactory db)
    {
        _db = db;
    }

    // ============================================================
    //  Qoldiq
    // ============================================================

    // Aktiv dorilar (0 bo'lsa ham) + qoldig'i bor faolsiz dorilar
    public async Task<List<StockBalanceRow>> GetBalanceAsync(long groupId, DateOnly? asOf)
    {
        var dateFilter = asOf is null ? "" : " AND m.movement_date <= @asOf";
        var sql = $@"
            SELECT * FROM (
                SELECT p.id AS product_id, p.name AS product_name, p.sort_order, p.active, p.perech_price,
                       COALESCE(SUM(CASE WHEN m.to_warehouse = 'nal' THEN m.qty
                                         WHEN m.from_warehouse = 'nal' THEN -m.qty ELSE 0 END), 0) AS nal,
                       COALESCE(SUM(CASE WHEN m.to_warehouse = 'mediwell' THEN m.qty
                                         WHEN m.from_warehouse = 'mediwell' THEN -m.qty ELSE 0 END), 0) AS mediwell
                  FROM stock_product p
                  LEFT JOIN stock_movement m
                         ON m.product_id = p.id AND m.deleted_at IS NULL{dateFilter}
                 WHERE p.group_id = @groupId
                 GROUP BY p.id
            ) t
            WHERE active OR nal <> 0 OR mediwell <> 0
            ORDER BY sort_order, product_name";

        using var conn = _db.Create();
        var rows = (await conn.QueryAsync<StockBalanceRow>(sql, new { groupId, asOf })).ToList();
        foreach (var r in rows) r.Total = r.Nal + r.Mediwell;
        return rows;
    }

    public async Task<ProductGroup?> GetGroupAsync(long groupId)
    {
        using var conn = _db.Create();
        return await conn.QuerySingleOrDefaultAsync<ProductGroup>(
            "SELECT * FROM product_group WHERE id = @groupId", new { groupId });
    }

    // ============================================================
    //  Ro'yxat va avtoto'ldirish
    // ============================================================

    public async Task<PagedResult<StockMovement>> ListAsync(
        long? groupId, long? productId, string? type, DateOnly? from, DateOnly? to,
        string? search, int page, int pageSize)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 1000);

        var where = new List<string> { "m.deleted_at IS NULL" };
        if (groupId is not null) where.Add("m.group_id = @groupId");
        if (productId is not null) where.Add("m.product_id = @productId");
        if (!string.IsNullOrWhiteSpace(type)) where.Add("m.movement_type = @type");
        if (from is not null) where.Add("m.movement_date >= @from");
        if (to is not null) where.Add("m.movement_date <= @to");
        if (!string.IsNullOrWhiteSpace(search))
            where.Add(@"(p.name ILIKE @search OR m.counterparty ILIKE @search OR m.region ILIKE @search
                         OR m.agent ILIKE @search OR m.doc_no ILIKE @search OR m.note ILIKE @search)");

        var whereSql = " WHERE " + string.Join(" AND ", where);
        const string fromSql = @"
              FROM stock_movement m
              JOIN stock_product p ON p.id = m.product_id
              JOIN product_group g ON g.id = m.group_id";

        var prm = new
        {
            groupId, productId, type, from, to,
            search = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%",
            limit = pageSize,
            offset = (page - 1) * pageSize
        };

        using var conn = _db.Create();
        var total = await conn.ExecuteScalarAsync<long>("SELECT COUNT(*)" + fromSql + whereSql, prm);
        var items = await conn.QueryAsync<StockMovement>(
            "SELECT m.*, p.name AS product_name, g.code AS group_code" + fromSql + whereSql +
            " ORDER BY m.movement_date DESC, m.id DESC LIMIT @limit OFFSET @offset", prm);

        return new PagedResult<StockMovement>
        {
            Items = items.ToList(), Total = total, Page = page, PageSize = pageSize
        };
    }

    public async Task<StockMovement?> GetByIdAsync(long id)
    {
        using var conn = _db.Create();
        return await conn.QuerySingleOrDefaultAsync<StockMovement>(@"
            SELECT m.*, p.name AS product_name, g.code AS group_code
              FROM stock_movement m
              JOIN stock_product p ON p.id = m.product_id
              JOIN product_group g ON g.id = m.group_id
             WHERE m.id = @id AND m.deleted_at IS NULL", new { id });
    }

    // field oldindan tekshirilgan bo'lishi shart (counterparty|region|agent)
    public async Task<IEnumerable<string>> GetSuggestionsAsync(string field, long? groupId)
    {
        var sql = $@"
            SELECT DISTINCT {field} FROM stock_movement
             WHERE deleted_at IS NULL AND {field} IS NOT NULL AND {field} <> ''"
            + (groupId is null ? "" : " AND group_id = @groupId")
            + $" ORDER BY {field} LIMIT 1000";

        using var conn = _db.Create();
        return await conn.QueryAsync<string>(sql, new { groupId });
    }

    // ============================================================
    //  Yozish
    // ============================================================

    public async Task<long> CreateAsync(CreateStockMovementRequest req, long? createdBy)
    {
        await using var conn = (NpgsqlConnection)_db.Create();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var products = await LoadProductsAsync(conn, tx, new[] { req.ProductId });
        var product = RequireProduct(products, req.ProductId, req.GroupId, requireActive: true, label: "");
        var price = ResolvePrice(req.MovementType, req.Price, product, "");

        var deltas = new List<Delta>();
        AddDeltas(deltas, req.ProductId, req.FromWarehouse, req.ToWarehouse, req.Qty, +1);
        await LockAndCheckAsync(conn, tx, deltas);

        var id = await InsertAsync(conn, tx, new InsertRow(
            req.MovementDate, req.GroupId, req.ProductId, req.MovementType,
            req.FromWarehouse, req.ToWarehouse, req.Qty, price,
            StockRules.Clean(req.Counterparty), StockRules.Clean(req.Region), StockRules.Clean(req.Agent),
            null, StockRules.Clean(req.Note), createdBy));

        await tx.CommitAsync();
        return id;
    }

    public async Task<(string DocNo, List<long> Ids)> CreateBatchAsync(StockMovementBatchRequest req, long? createdBy)
    {
        await using var conn = (NpgsqlConnection)_db.Create();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var products = await LoadProductsAsync(conn, tx, req.Lines.Select(l => l.ProductId));
        var deltas = new List<Delta>();
        var prices = new List<decimal?>();

        for (var i = 0; i < req.Lines.Count; i++)
        {
            var l = req.Lines[i];
            var label = $"{i + 1}-qator: ";
            var product = RequireProduct(products, l.ProductId, req.GroupId, requireActive: true, label);
            prices.Add(ResolvePrice(req.MovementType, l.Price, product, label));
            AddDeltas(deltas, l.ProductId, req.FromWarehouse, req.ToWarehouse, l.Qty, +1);
        }

        await LockAndCheckAsync(conn, tx, deltas);

        var docNo = await NextDocNoAsync(conn, tx, req.MovementType);
        var ids = new List<long>();
        for (var i = 0; i < req.Lines.Count; i++)
        {
            var l = req.Lines[i];
            ids.Add(await InsertAsync(conn, tx, new InsertRow(
                req.MovementDate, req.GroupId, l.ProductId, req.MovementType,
                req.FromWarehouse, req.ToWarehouse, l.Qty, prices[i],
                StockRules.Clean(req.Counterparty), StockRules.Clean(req.Region), StockRules.Clean(req.Agent),
                docNo, StockRules.Clean(l.Note) ?? StockRules.Clean(req.Note), createdBy)));
        }

        await tx.CommitAsync();
        return (docNo, ids);
    }

    // Guruhdagi eski opening'lar o'chiriladi (soft), yangilari yoziladi.
    public async Task<(string? DocNo, int Inserted, int Replaced)> SaveOpeningAsync(OpeningRequest req, long? createdBy)
    {
        await using var conn = (NpgsqlConnection)_db.Create();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var groupExists = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM product_group WHERE id = @GroupId)", new { req.GroupId }, tx);
        if (!groupExists)
            throw new InvalidOperationException($"Guruh topilmadi: id={req.GroupId}");

        var lines = req.Lines.Where(l => l.Nal > 0 || l.Mediwell > 0).ToList();
        var products = await LoadProductsAsync(conn, tx, lines.Select(l => l.ProductId));
        foreach (var l in lines)
            RequireProduct(products, l.ProductId, req.GroupId, requireActive: false, label: "");

        var old = (await conn.QueryAsync<OldMovement>(@"
            SELECT id, product_id, from_warehouse, to_warehouse, qty
              FROM stock_movement
             WHERE group_id = @GroupId AND movement_type = 'opening' AND deleted_at IS NULL",
            new { req.GroupId }, tx)).ToList();

        var deltas = new List<Delta>();
        foreach (var o in old)
            AddDeltas(deltas, o.ProductId, o.FromWarehouse, o.ToWarehouse, o.Qty, -1);
        foreach (var l in lines)
        {
            if (l.Nal > 0) AddDeltas(deltas, l.ProductId, null, StockRules.Nal, l.Nal, +1);
            if (l.Mediwell > 0) AddDeltas(deltas, l.ProductId, null, StockRules.Mediwell, l.Mediwell, +1);
        }
        await LockAndCheckAsync(conn, tx, deltas);

        if (old.Count > 0)
            await conn.ExecuteAsync(
                "UPDATE stock_movement SET deleted_at = now(), updated_at = now() WHERE id = ANY(@ids)",
                new { ids = old.Select(o => o.Id).ToArray() }, tx);

        string? docNo = null;
        var inserted = 0;
        if (lines.Count > 0)
        {
            docNo = await NextDocNoAsync(conn, tx, StockRules.Opening);
            var note = StockRules.Clean(req.Note);
            foreach (var l in lines)
            {
                foreach (var (wh, qty) in new[] { (StockRules.Nal, l.Nal), (StockRules.Mediwell, l.Mediwell) })
                {
                    if (qty <= 0) continue;
                    await InsertAsync(conn, tx, new InsertRow(
                        req.MovementDate, req.GroupId, l.ProductId, StockRules.Opening,
                        null, wh, qty, null, null, null, null, docNo, note, createdBy));
                    inserted++;
                }
            }
        }

        await tx.CommitAsync();
        return (docNo, inserted, old.Count);
    }

    // false = topilmadi
    public async Task<bool> UpdateAsync(long id, UpdateStockMovementRequest req)
    {
        await using var conn = (NpgsqlConnection)_db.Create();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var old = await conn.QuerySingleOrDefaultAsync<OldMovement>(@"
            SELECT id, group_id, product_id, movement_type, from_warehouse, to_warehouse, qty
              FROM stock_movement
             WHERE id = @id AND deleted_at IS NULL
               FOR UPDATE", new { id }, tx);
        if (old is null) return false;

        var (from, to) = StockRules.Normalize(old.MovementType, req.FromWarehouse, req.ToWarehouse);
        var errors = new List<string>();
        StockRules.ValidateWarehouses(old.MovementType, from, to, errors);
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors));

        var products = await LoadProductsAsync(conn, tx, new[] { req.ProductId });
        // Dori almashtirilsa — yangisi aktiv bo'lishi kerak; o'sha dori qolsa faolsiz bo'lsa ham mayli.
        var product = RequireProduct(products, req.ProductId, old.GroupId,
            requireActive: req.ProductId != old.ProductId, label: "");
        var price = ResolvePrice(old.MovementType, req.Price, product, "");

        var deltas = new List<Delta>();
        AddDeltas(deltas, old.ProductId, old.FromWarehouse, old.ToWarehouse, old.Qty, -1);
        AddDeltas(deltas, req.ProductId, from, to, req.Qty, +1);
        await LockAndCheckAsync(conn, tx, deltas);

        await conn.ExecuteAsync(@"
            UPDATE stock_movement
               SET movement_date = @MovementDate, product_id = @ProductId,
                   from_warehouse = @from, to_warehouse = @to, qty = @Qty, price = @price,
                   counterparty = @counterparty, region = @region, agent = @agent, note = @note,
                   updated_at = now()
             WHERE id = @id",
            new
            {
                id, req.MovementDate, req.ProductId, from, to, req.Qty, price,
                counterparty = StockRules.Clean(req.Counterparty),
                region = StockRules.Clean(req.Region),
                agent = StockRules.Clean(req.Agent),
                note = StockRules.Clean(req.Note)
            }, tx);

        await tx.CommitAsync();
        return true;
    }

    // Soft delete. false = topilmadi
    public async Task<bool> DeleteAsync(long id)
    {
        await using var conn = (NpgsqlConnection)_db.Create();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var old = await conn.QuerySingleOrDefaultAsync<OldMovement>(@"
            SELECT id, group_id, product_id, movement_type, from_warehouse, to_warehouse, qty
              FROM stock_movement
             WHERE id = @id AND deleted_at IS NULL
               FOR UPDATE", new { id }, tx);
        if (old is null) return false;

        var deltas = new List<Delta>();
        AddDeltas(deltas, old.ProductId, old.FromWarehouse, old.ToWarehouse, old.Qty, -1);
        await LockAndCheckAsync(conn, tx, deltas);

        await conn.ExecuteAsync(
            "UPDATE stock_movement SET deleted_at = now(), updated_at = now() WHERE id = @id", new { id }, tx);

        await tx.CommitAsync();
        return true;
    }

    // ============================================================
    //  Yordamchilar
    // ============================================================

    private record Delta(long ProductId, string Warehouse, long Amount);

    private record InsertRow(
        DateOnly MovementDate, long GroupId, long ProductId, string MovementType,
        string? FromWarehouse, string? ToWarehouse, int Qty, decimal? Price,
        string? Counterparty, string? Region, string? Agent, string? DocNo, string? Note, long? CreatedBy);

    private class ProductInfo
    {
        public long Id { get; set; }
        public long GroupId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal? PerechPrice { get; set; }
        public bool Active { get; set; }
    }

    private class OldMovement
    {
        public long Id { get; set; }
        public long GroupId { get; set; }
        public long ProductId { get; set; }
        public string MovementType { get; set; } = string.Empty;
        public string? FromWarehouse { get; set; }
        public string? ToWarehouse { get; set; }
        public int Qty { get; set; }
    }

    private class BalanceInfo
    {
        public long ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public long Nal { get; set; }
        public long Mediwell { get; set; }
    }

    private static void AddDeltas(List<Delta> deltas, long productId, string? from, string? to, int qty, int sign)
    {
        if (to is not null) deltas.Add(new Delta(productId, to, sign * (long)qty));
        if (from is not null) deltas.Add(new Delta(productId, from, -sign * (long)qty));
    }

    private static async Task<Dictionary<long, ProductInfo>> LoadProductsAsync(
        NpgsqlConnection conn, IDbTransaction tx, IEnumerable<long> ids)
    {
        var arr = ids.Distinct().ToArray();
        if (arr.Length == 0) return new();
        var rows = await conn.QueryAsync<ProductInfo>(
            "SELECT id, group_id, name, perech_price, active FROM stock_product WHERE id = ANY(@arr)",
            new { arr }, tx);
        return rows.ToDictionary(r => r.Id);
    }

    private static ProductInfo RequireProduct(
        Dictionary<long, ProductInfo> products, long productId, long groupId, bool requireActive, string label)
    {
        if (!products.TryGetValue(productId, out var p))
            throw new InvalidOperationException($"{label}Dori topilmadi: id={productId}");
        if (p.GroupId != groupId)
            throw new InvalidOperationException($"{label}«{p.Name}» bu guruhga tegishli emas.");
        if (requireActive && !p.Active)
            throw new InvalidOperationException($"{label}«{p.Name}» faolsizlantirilgan.");
        return p;
    }

    // Переч sotuvida narx bo'sh bo'lsa — dorining Переч narxi. Boshqa turlarda narx saqlanmaydi.
    private static decimal? ResolvePrice(string type, decimal? price, ProductInfo product, string label)
    {
        if (type != StockRules.SalePerech) return null;
        var resolved = price ?? product.PerechPrice;
        if (resolved is null)
            throw new InvalidOperationException(
                $"{label}«{product.Name}» uchun narx ko'rsatilmagan va dorining Переч narxi ham kiritilmagan.");
        return resolved;
    }

    // Dorilarni (o'sish tartibida — deadlock bo'lmasin) qulflab, minusga tushishni tekshiradi.
    private static async Task LockAndCheckAsync(NpgsqlConnection conn, IDbTransaction tx, List<Delta> deltas)
    {
        var ids = deltas.Select(d => d.ProductId).Distinct().OrderBy(x => x).ToArray();
        foreach (var pid in ids)
            await conn.ExecuteAsync("SELECT pg_advisory_xact_lock(@cls, @key)",
                new { cls = LockClass, key = (int)pid }, tx);

        var net = deltas
            .GroupBy(d => (d.ProductId, d.Warehouse))
            .Select(g => (g.Key.ProductId, g.Key.Warehouse, Amount: g.Sum(x => x.Amount)))
            .Where(x => x.Amount < 0)
            .ToList();
        if (net.Count == 0) return;

        var balances = (await conn.QueryAsync<BalanceInfo>(
                "SELECT product_id, product_name, nal, mediwell FROM v_stock_balance WHERE product_id = ANY(@ids)",
                new { ids = net.Select(n => n.ProductId).Distinct().ToArray() }, tx))
            .ToDictionary(b => b.ProductId);

        var problems = new List<string>();
        foreach (var (pid, wh, amount) in net)
        {
            var b = balances[pid];
            var before = wh == StockRules.Nal ? b.Nal : b.Mediwell;
            if (before + amount < 0)
                problems.Add($"{b.ProductName}: {StockRules.WarehouseLabel(wh)} omborida {Fmt(before)} ta bor, " +
                             $"{Fmt(-amount)} ta chiqarib bo'lmaydi");
        }

        if (problems.Count > 0)
            throw new NegativeStockException(problems);
    }

    private static async Task<string> NextDocNoAsync(NpgsqlConnection conn, IDbTransaction tx, string type)
    {
        var prefix = StockRules.DocPrefix(type);
        var n = await conn.ExecuteScalarAsync<int>(
            "UPDATE stock_doc_counter SET last_no = last_no + 1 WHERE prefix = @prefix RETURNING last_no",
            new { prefix }, tx);
        return $"{prefix}-{n:D6}";
    }

    private static Task<long> InsertAsync(NpgsqlConnection conn, IDbTransaction tx, InsertRow r) =>
        conn.ExecuteScalarAsync<long>(@"
            INSERT INTO stock_movement
                (movement_date, group_id, product_id, movement_type, from_warehouse, to_warehouse,
                 qty, price, counterparty, region, agent, doc_no, note, created_by)
            VALUES
                (@MovementDate, @GroupId, @ProductId, @MovementType, @FromWarehouse, @ToWarehouse,
                 @Qty, @Price, @Counterparty, @Region, @Agent, @DocNo, @Note, @CreatedBy)
            RETURNING id", r, tx);

    private static readonly NumberFormatInfo SpaceGroup = new() { NumberGroupSeparator = " " };
    public static string Fmt(long n) => n.ToString("#,0", SpaceGroup);
}
