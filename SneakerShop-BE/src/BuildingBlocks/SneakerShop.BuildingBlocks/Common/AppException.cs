namespace SneakerShop.BuildingBlocks;

/// <summary>An expected, client-facing error. The global handler turns it into a ProblemDetails response.</summary>
public class AppException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
