namespace Server.Arkaine.LocalB2;

public sealed class LocalB2RequestException : Exception
{
    public LocalB2RequestException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public LocalB2RequestException(int statusCode, string message, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
