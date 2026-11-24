namespace Mediwell.Api.Models;

// ============================================================
//  Sklad hisobi (markirovkasiz) modellari.
//  Markirovka modulidagi StockGroup/StockItem bilan aralashtirmang.
// ============================================================

public static class StockRules
{
    // Schema'dagi CHECK cheklovlari bilan bir xil bo'lishi shart.
    public const string Opening = "opening";
    public const string Receipt = "receipt";
    public const string Transfer = "transfer";
    public const string WriteOff = "write_off";
    public const string SalePerech = "sale_perech";
    public const string SaleNal = "sale_nal";

    public const string Nal = "nal";
    public const string Mediwell = "mediwell";

    public static readonly IReadOnlyList<string> Types =
        new[] { Opening, Receipt, Transfer, WriteOff, SalePerech, SaleNal };

    public static readonly IReadOnlyList<string> Warehouses = new[] { Nal, Mediwell };

    public static string DocPrefix(string type) => type switch
    {
        Opening => "OPN",
        Receipt => "KIR",
        Transfer => "KOC",
        WriteOff => "SPI",
        SalePerech => "PER",
        SaleNal => "NAL",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static string WarehouseLabel(string wh) => wh == Nal ? "нал" : "mediwell";

    // sale_perech / sale_nal uchun "qayerdan" avtomatik; keraksiz tomon tozalanadi.
    public static (string? From, string? To) Normalize(string type, string? from, string? to)
    {
        from = string.IsNullOrWhiteSpace(from) ? null : from.Trim();
        to = string.IsNullOrWhiteSpace(to) ? null : to.Trim();
        return type switch
        {
            Opening or Receipt => (null, to),
            WriteOff => (from, null),
            SalePerech => (Mediwell, null),
            SaleNal => (Nal, null),
            _ => (from, to)
        };
    }

    // Normalize'dan keyin chaqiriladi.
    public static void ValidateWarehouses(string type, string? from, string? to, List<string> errors)
    {
        if (from is not null && !Warehouses.Contains(from))
            errors.Add($"FromWarehouse noto'g'ri: '{from}'. Ruxsat etilgan: nal, mediwell.");
        if (to is not null && !Warehouses.Contains(to))
            errors.Add($"ToWarehouse noto'g'ri: '{to}'. Ruxsat etilgan: nal, mediwell.");

        switch (type)
        {
            case Opening or Receipt when to is null:
                errors.Add("Kirim uchun ombor (ToWarehouse) tanlanishi kerak.");
                break;
            case WriteOff when from is null:
                errors.Add("Spisanie uchun ombor (FromWarehouse) tanlanishi kerak.");
                break;
            case Transfer when from is null || to is null:
                errors.Add("Ko'chirish uchun ikkala ombor (qayerdan va qayerga) tanlanishi kerak.");
                break;
            case Transfer when from == to:
                errors.Add("Ko'chirishda omborlar har xil bo'lishi kerak.");
                break;
        }
    }

    public static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

// ---------- Guruhlar ----------

public class ProductGroup
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool Active { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SaveProductGroupRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool Active { get; set; } = true;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Code)) errors.Add("Code bo'sh bo'lmasligi kerak.");
        if (string.IsNullOrWhiteSpace(Name)) errors.Add("Name bo'sh bo'lmasligi kerak.");
        return errors;
    }
}

// ---------- Dorilar ----------

public class StockProduct
{
    public long Id { get; set; }
    public long GroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? PerechPrice { get; set; }
    public int SortOrder { get; set; }
    public bool Active { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool HasMovements { get; set; }
}

public class SaveStockProductRequest
{
    public long GroupId { get; set; }          // PUT'da e'tiborga olinmaydi (guruh o'zgarmaydi)
    public string Name { get; set; } = string.Empty;
    public decimal? PerechPrice { get; set; }
    public int SortOrder { get; set; }
    public bool Active { get; set; } = true;

    public IReadOnlyList<string> Validate(bool isCreate)
    {
        var errors = new List<string>();
        if (isCreate && GroupId <= 0) errors.Add("GroupId musbat bo'lishi kerak.");
        if (string.IsNullOrWhiteSpace(Name)) errors.Add("Dori nomi bo'sh bo'lmasligi kerak.");
        if (PerechPrice is < 0) errors.Add("Переч narxi manfiy bo'lishi mumkin emas.");
        return errors;
    }
}

// ---------- Qoldiq ----------

public class StockBalanceRow
{
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool Active { get; set; }
    public decimal? PerechPrice { get; set; }
    public long Nal { get; set; }
    public long Mediwell { get; set; }
    public long Total { get; set; }
}

// ---------- Harakatlar ----------

public class StockMovement
{
    public long Id { get; set; }
    public DateOnly MovementDate { get; set; }
    public long GroupId { get; set; }
    public string GroupCode { get; set; } = string.Empty;
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string MovementType { get; set; } = string.Empty;
    public string? FromWarehouse { get; set; }
    public string? ToWarehouse { get; set; }
    public int Qty { get; set; }
    public decimal? Price { get; set; }
    public decimal? Amount => Price * Qty;
    public string? Counterparty { get; set; }
    public string? Region { get; set; }
    public string? Agent { get; set; }
    public string? DocNo { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public long Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

// Bitta harakat (POST /stock-movements)
public class CreateStockMovementRequest
{
    public DateOnly MovementDate { get; set; }
    public long GroupId { get; set; }
    public long ProductId { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public string? FromWarehouse { get; set; }
    public string? ToWarehouse { get; set; }
    public int Qty { get; set; }
    public decimal? Price { get; set; }
    public string? Counterparty { get; set; }
    public string? Region { get; set; }
    public string? Agent { get; set; }
    public string? Note { get; set; }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (MovementDate == default) errors.Add("Sana (MovementDate) ko'rsatilishi kerak.");
        if (GroupId <= 0) errors.Add("GroupId musbat bo'lishi kerak.");
        if (ProductId <= 0) errors.Add("ProductId musbat bo'lishi kerak.");
        if (Qty <= 0) errors.Add("Son (Qty) musbat bo'lishi kerak.");
        if (Price is < 0) errors.Add("Narx manfiy bo'lishi mumkin emas.");

        if (!StockRules.Types.Contains(MovementType))
        {
            errors.Add($"MovementType noto'g'ri: '{MovementType}'. Ruxsat etilgan: {string.Join(", ", StockRules.Types)}.");
            return errors;
        }
        if (MovementType == StockRules.Opening)
            errors.Add("Boshlang'ich qoldiq /stock-movements/opening orqali kiritiladi.");

        (FromWarehouse, ToWarehouse) = StockRules.Normalize(MovementType, FromWarehouse, ToWarehouse);
        StockRules.ValidateWarehouses(MovementType, FromWarehouse, ToWarehouse, errors);
        return errors;
    }
}

// Bitta hujjat (POST /stock-movements/batch)
public class StockMovementBatchRequest
{
    public DateOnly MovementDate { get; set; }
    public long GroupId { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public string? FromWarehouse { get; set; }
    public string? ToWarehouse { get; set; }
    public string? Counterparty { get; set; }
    public string? Region { get; set; }
    public string? Agent { get; set; }
    public string? Note { get; set; }
    public List<BatchLine> Lines { get; set; } = new();

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (MovementDate == default) errors.Add("Sana (MovementDate) ko'rsatilishi kerak.");
        if (GroupId <= 0) errors.Add("GroupId musbat bo'lishi kerak.");

        if (!StockRules.Types.Contains(MovementType))
            errors.Add($"MovementType noto'g'ri: '{MovementType}'. Ruxsat etilgan: {string.Join(", ", StockRules.Types)}.");
        else if (MovementType == StockRules.Opening)
            errors.Add("Boshlang'ich qoldiq /stock-movements/opening orqali kiritiladi.");
        else
        {
            (FromWarehouse, ToWarehouse) = StockRules.Normalize(MovementType, FromWarehouse, ToWarehouse);
            StockRules.ValidateWarehouses(MovementType, FromWarehouse, ToWarehouse, errors);
        }

        if (Lines.Count == 0)
        {
            errors.Add("Kamida bitta qator kerak.");
            return errors;
        }

        for (var i = 0; i < Lines.Count; i++)
        {
            var l = Lines[i];
            if (l.ProductId <= 0) errors.Add($"{i + 1}-qator: dori tanlanmagan.");
            if (l.Qty <= 0) errors.Add($"{i + 1}-qator: son musbat bo'lishi kerak.");
            if (l.Price is < 0) errors.Add($"{i + 1}-qator: narx manfiy bo'lishi mumkin emas.");
        }
        return errors;
    }
}

public class BatchLine
{
    public long ProductId { get; set; }
    public int Qty { get; set; }
    public decimal? Price { get; set; }
    public string? Note { get; set; }
}

// Boshlang'ich qoldiq (POST /stock-movements/opening) — guruhdagi eski opening'lar almashtiriladi
public class OpeningRequest
{
    public long GroupId { get; set; }
    public DateOnly MovementDate { get; set; }
    public string? Note { get; set; }
    public List<OpeningLine> Lines { get; set; } = new();

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (GroupId <= 0) errors.Add("GroupId musbat bo'lishi kerak.");
        if (MovementDate == default) errors.Add("Sana (MovementDate) ko'rsatilishi kerak.");

        var seen = new HashSet<long>();
        for (var i = 0; i < Lines.Count; i++)
        {
            var l = Lines[i];
            if (l.ProductId <= 0) errors.Add($"{i + 1}-qator: ProductId musbat bo'lishi kerak.");
            else if (!seen.Add(l.ProductId)) errors.Add($"{i + 1}-qator: dori takrorlangan.");
            if (l.Nal < 0 || l.Mediwell < 0) errors.Add($"{i + 1}-qator: qoldiq manfiy bo'lishi mumkin emas.");
        }
        return errors;
    }
}

public class OpeningLine
{
    public long ProductId { get; set; }
    public int Nal { get; set; }
    public int Mediwell { get; set; }
}

// PUT /stock-movements/{id} — guruh va tur o'zgarmaydi
public class UpdateStockMovementRequest
{
    public DateOnly MovementDate { get; set; }
    public long ProductId { get; set; }
    public string? FromWarehouse { get; set; }
    public string? ToWarehouse { get; set; }
    public int Qty { get; set; }
    public decimal? Price { get; set; }
    public string? Counterparty { get; set; }
    public string? Region { get; set; }
    public string? Agent { get; set; }
    public string? Note { get; set; }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (MovementDate == default) errors.Add("Sana (MovementDate) ko'rsatilishi kerak.");
        if (ProductId <= 0) errors.Add("ProductId musbat bo'lishi kerak.");
        if (Qty <= 0) errors.Add("Son (Qty) musbat bo'lishi kerak.");
        if (Price is < 0) errors.Add("Narx manfiy bo'lishi mumkin emas.");
        return errors;
    }
}
