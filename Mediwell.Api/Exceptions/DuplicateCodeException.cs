namespace Mediwell.Api.Exceptions;

public class DuplicateCodeException :Exception
{
    public List<string> Codes { get; }
    public DuplicateCodeException(List<string> codes)
        : base($"Bu kodlar allaqachon bazada bor: {string.Join(", ", codes)}")
    {
        Codes = codes;
    }
}