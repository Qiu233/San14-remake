using San14.Data;

/// <summary>Fresh owned Host Q selection and actual current single-View table.
/// Q3F8 comes from BF58A0/BF6DC0 constructor writes, not a runtime observation.
/// Later selection/binding edits invalidate this finite owner instead of
/// inheriting a literal slot from a previously captured original process.</summary>
public sealed class SceneViewNativeMapNode51ExecutionOwner
{
    private readonly MainSceneNode51Source source;
    private readonly MainMapContextCurrentSource context;
    private readonly string collection;
    private readonly uint currentWord3F8;
    private bool retained = true;
    private long frame;
    private MapLightingBinding? slot0;
    private MainSceneNode51Execution? current;

    private SceneViewNativeMapNode51ExecutionOwner(MainSceneNode51Source source,
        MainMapContextCurrentSource context, string collection)
    {
        this.source = source; this.context = context; this.collection = collection;
        // Actual TC BF5AE6 XOR EDX; BF5AFD writes qword Q3F8.
        // Paired SC BF7006 XOR EDX; BF701D writes the same qword.
        // High32 bytes are also cleared, not used as unknown padding.
        currentWord3F8 = 0;
    }

    public static SceneViewNativeMapNode51ExecutionOwner CreateFreshSingleView(
        GameResourceRepository repository, string locale, MainMapContextCurrentSource context,
        string currentOwnedCollectionIdentity, int sourceNodeIndex = 51)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentOwnedCollectionIdentity);
        var source = MainSceneNode51Source.Load(repository, locale, sourceNodeIndex);
        if (context.Host.Locale != source.Locale || context.Host.RuleVersion != source.RuleVersion)
            throw new InvalidDataException("Single-View selection must use this exact fresh Host constructor and node51 executable source.");
        return new(source, context, currentOwnedCollectionIdentity);
    }

    public void MarkImportedEditedOrRebound(string writer)
    { ArgumentException.ThrowIfNullOrWhiteSpace(writer); retained = false; slot0 = null; current = null; }

    public MainSceneNode51Execution PublishAndPrepareCurrent(long currentFrame,
        MapLightingBinding actualCurrentView)
    {
        ArgumentNullException.ThrowIfNull(actualCurrentView);
        if (!retained || currentFrame < 1 || currentFrame <= frame)
            throw new InvalidDataException("Owned single-View execution was edited/rebound or is not a new actual publication.");
        frame = currentFrame; slot0 = actualCurrentView;
        uint selected = (currentWord3F8 & 0x7FFFFFFF) + (uint)(source.SourceViewId & 0x7F);
        if (selected != 0 || slot0 is null)
            throw new NotSupportedException("This source owner supports only its actual nonnull owned single View publication.");
        return current = source.PrepareCurrent(new(currentFrame, collection + "/execution/" + currentFrame,
            collection, source.ViewCollectionSourceNameHash, true, false, currentWord3F8,
            checked((int)selected), slot0, true,
            context.SourceProvenance + ";Q3F8=fresh-retained-constructor-qword;TC:BF5AE6/BF5AFD;SC:BF7006/BF701D;" +
            $"actual-owned-table-slot0-published;node{source.NodeIndex}-CPU-and-bindings-retained;not-original-runtime-observation")
            { CurrentContextOwnerIdentity = context.OwnedHostIdentity });
    }

    public void ValidateCurrent(MainSceneNode51Execution actual)
    {
        if (!retained || !ReferenceEquals(actual, current) || actual.FrameNumber != frame || actual.Invocation.SelectedView != slot0)
            throw new InvalidDataException("Node51 selection receipt is stale, edited or belongs to another current owned table.");
        actual.RequireSelectedView(frame, slot0!);
    }
}
