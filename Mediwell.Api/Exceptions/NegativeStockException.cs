namespace Mediwell.Api.Exceptions;

// Harakat qoldiqni minusga tushiradi — endpoint buni 409 ga aylantiradi.
public class NegativeStockException : Exception
{
    public List<string> Problems { get; }

    public NegativeStockException(List<string> problems)
        : base(problems.Count > 0 ? problems[0] : "Qoldiq yetarli emas.")
    {
        Problems = problems;
    }
}
