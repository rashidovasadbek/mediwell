namespace Mediwell.Api.Models;

public class CreateReceiptRequest
{
    // Schema'dagi CHECK cheklovlari bilan bir xil bo'lishi shart.
    public static readonly IReadOnlyList<string> AllowedEntryMethods = new[] { "file", "manual", "scan" };
    public static readonly IReadOnlyList<string> AllowedArrivalStatuses = new[] { "queued", "arrived" };

    // Kirim ma'lumoti
    public long ProductId { get; set; }
    public string Series { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public string? Supplier { get; set; }
    public string EntryMethod { get; set; } = "manual";
    public string? Note { get; set; }

    // Agregat kodlar ro'yxati
    public List<AggregateInput> Codes { get; set; } = new();

    // So'rovni bazaga urinishdan oldin tekshiramiz — aks holda CHECK/NOT NULL
    // buzilishlari 500 bo'lib qaytadi.
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (ProductId <= 0)
            errors.Add("ProductId musbat bo'lishi kerak.");

        if (string.IsNullOrWhiteSpace(Series))
            errors.Add("Series bo'sh bo'lmasligi kerak.");

        if (ExpiryDate == default)
            errors.Add("ExpiryDate ko'rsatilishi kerak.");

        if (!AllowedEntryMethods.Contains(EntryMethod))
            errors.Add($"EntryMethod noto'g'ri: '{EntryMethod}'. Ruxsat etilgan: {string.Join(", ", AllowedEntryMethods)}.");

        if (Codes.Count == 0)
        {
            errors.Add("Kamida bitta agregat kod kerak.");
            return errors;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < Codes.Count; i++)
        {
            var c = Codes[i];

            if (string.IsNullOrWhiteSpace(c.Code))
                errors.Add($"Codes[{i}]: kod bo'sh bo'lmasligi kerak.");
            else if (!seen.Add(c.Code))
                errors.Add($"Codes[{i}]: kod so'rov ichida takrorlangan — '{c.Code}'.");

            if (c.ActualCount is <= 0)
                errors.Add($"Codes[{i}]: ActualCount musbat bo'lishi kerak.");

            if (!AllowedArrivalStatuses.Contains(c.ArrivalStatus))
                errors.Add($"Codes[{i}]: ArrivalStatus noto'g'ri: '{c.ArrivalStatus}'. Ruxsat etilgan: {string.Join(", ", AllowedArrivalStatuses)}.");
        }

        return errors;
    }
    
    public List<string> UnitCodes { get; set; } = new();
}

public class AggregateInput
{
    public string Code { get; set; } = string.Empty;
    // Bo'sh bo'lsa — preparatning nominal_capacity si ishlatiladi (buzilmagan)
    // To'ldirilsa — buzilgan karobka (masalan 33)
    public int? ActualCount { get; set; }
    public string ArrivalStatus { get; set; } = "queued";
    public string? ArrivalFirm { get; set; }
}
