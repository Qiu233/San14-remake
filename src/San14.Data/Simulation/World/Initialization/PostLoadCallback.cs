namespace San14.Data;

/// <summary>Project callback roles for static post-load stages and associated
/// payloads. Values are semantic roles, not original code addresses.</summary>
public enum PostLoadCallbackKind
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

public readonly record struct PostLoadCallback(
    PostLoadCallbackKind Kind, string? HandlerIdentity = null)
{
    public static PostLoadCallback NoOp => new(PostLoadCallbackKind.NoOp);

    public static PostLoadCallback ForHandler(string handlerIdentity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerIdentity);
        return new(PostLoadCallbackKind.External, handlerIdentity);
    }
}
