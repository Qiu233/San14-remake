namespace San14.Data;

/// <summary>Fixed popularity explanations from the installed Game messages.
/// Both the live page publication and startup text preparation use the same
/// resource dialect without requiring a world or a random source.</summary>
public sealed class InformationDestinyExplanations
{
    private readonly NarrationResourceMessages messages;

    public InformationDestinyExplanations(LoadedMessages resource) => messages = new(resource.Messages);

    public string Read(int messageId) => messages.ResolveInformationExplanation(messageId);
}
