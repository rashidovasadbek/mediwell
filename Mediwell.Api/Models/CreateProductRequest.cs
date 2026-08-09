namespace Mediwell.Api.Models;

// Product entity'ning o'zini so'rov sifatida ishlatsak, klient id/active/created_at
// ni ham yubora oladi (INSERT ularni e'tiborsiz qoldiradi, lekin Swagger chalg'itadi).
public class CreateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public int NominalCapacity { get; set; }
    public string? Gtin { get; set; }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        // NOT NULL bo'sh satrni to'smaydi — shuning uchun bu yerda tekshiramiz.
        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("Name bo'sh bo'lmasligi kerak.");

        if (NominalCapacity < 0)
            errors.Add("NominalCapacity manfiy bo'la olmaydi.");

        // Faqat raqam ekanini tekshiramiz. Uzunlikni (GTIN-8/12/13/14) majburlamaymiz —
        // bazadagi mavjud yozuvlarda 11 xonali gtin ham bor.
        if (!string.IsNullOrWhiteSpace(Gtin) && !Gtin.All(char.IsAsciiDigit))
            errors.Add("Gtin faqat raqamlardan iborat bo'lishi kerak.");

        return errors;
    }
}
