namespace Mediwell.Api.Data;

// Bazadagi UNIQUE cheklov buzilishi (SQLSTATE 23505) — endpoint buni 409 ga aylantiradi.
public class DuplicateKeyException : Exception
{
    public DuplicateKeyException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
