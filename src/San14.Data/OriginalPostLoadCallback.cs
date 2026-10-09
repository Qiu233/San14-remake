namespace San14.Data;

/// <summary>Project callback roles for static post-load stages and associated
/// payloads. Values are semantic roles, not original code addresses.</summary>
public enum OriginalPostLoadCallbackKind
{
    Unknown,
    NoOp,
    City,
    Gate,
    Personality,
    Policy,
    Person,
    District,
    External
}

public readonly record struct OriginalPostLoadCallback(
    OriginalPostLoadCallbackKind Kind, string? HandlerIdentity = null)
{
    public static OriginalPostLoadCallback NoOp => new(OriginalPostLoadCallbackKind.NoOp);

    public static OriginalPostLoadCallback ForHandler(string handlerIdentity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerIdentity);
        return new(OriginalPostLoadCallbackKind.External, handlerIdentity);
    }
}
