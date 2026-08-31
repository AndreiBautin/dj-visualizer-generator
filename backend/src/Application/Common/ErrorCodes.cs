namespace DjVisualizer.Application.Common;

public static class ErrorCodes
{
    public const string Validation = "validation";
    public const string NotFound = "not_found";
    public const string NotReady = "not_ready";
    public const string Unavailable = "unavailable";

    /// <summary>A per-resource allowance the caller has used up - distinct from Unavailable,
    /// which is the whole instance being out of capacity and affects everyone.</summary>
    public const string Exhausted = "exhausted";
    public const string Failure = "failure";
}
