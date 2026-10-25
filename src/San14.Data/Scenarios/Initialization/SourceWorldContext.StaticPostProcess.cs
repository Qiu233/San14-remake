namespace San14.Data;

public sealed partial class SourceWorldContext : IStaticTablePostProcessState
{
    private readonly Dictionary<(int Table, int Physical), PostLoadCallback?> _staticPostProcessCallbacks = [];
    /// <summary>Required complete handlers for remaining original callbacks.
    /// Partial operators must not be registered as complete slot7 callbacks.</summary>
    public Dictionary<string, Action<StaticPostProcessReference>> StaticPostProcessHandlers { get; } = new(StringComparer.Ordinal);
    public void SetStaticTablePostProcessCallback(int tableOffset, int physical, PostLoadCallback? callback)
    {
        _ = StaticTablePostProcess.SupportedCallback(tableOffset);
        Check(physical, StaticTableCount(tableOffset), "static slot7 receiver");
        _staticPostProcessCallbacks[(tableOffset, physical)] = callback;
    }
    public StaticPostProcessReference? ReadStaticPostProcessReference(int tableOffset, int slot)
    {
        _ = StaticTablePostProcess.SupportedCallback(tableOffset);
        StaticTableReference? reference = ReadStaticTableReference(tableOffset, slot);
        if (reference is null) return null;
        PostLoadCallback? callback = reference.PhysicalSlot.HasValue && _staticPostProcessCallbacks.TryGetValue((tableOffset, reference.PhysicalSlot.Value), out var current)
            ? current : StaticTablePostProcess.SupportedCallback(tableOffset);
        return new(tableOffset, reference.PhysicalSlot, callback);
    }
    public void InvokeStaticPostProcess(StaticPostProcessReference reference)
    {
        int physical = reference.PhysicalSlot ?? throw new InvalidDataException("Null static slot7 receiver.");
        var callback = reference.Callback ?? throw new InvalidDataException("Current static post-process callback is unknown.");
        switch (callback.Kind)
        {
            case PostLoadCallbackKind.NoOp: return;
            case PostLoadCallbackKind.City: PostProcessBase(new(BaseKind.City, physical)); return;
            case PostLoadCallbackKind.Gate: PostProcessBase(new(BaseKind.Gate, physical)); return;
            case PostLoadCallbackKind.Personality: PostProcessPersonality(new(physical)); return;
            case PostLoadCallbackKind.Policy: PostProcessPolicyEffect(physical); return;
            case PostLoadCallbackKind.Person: _ = PostProcessPerson(physical); return;
            case PostLoadCallbackKind.District:
                IEnumerable<int> persons = ProjectRegistry(RegistryKind.Person, person =>
                    person?.PhysicalSlot ?? throw new InvalidDataException("Current district slot7 person pointer is unknown or null."))
                    ?? throw new InvalidDataException("Current district slot7 person registry is unknown.");
                _ = DistrictPostLoad.Apply(physical, persons, this);
                return;
            case PostLoadCallbackKind.External when callback.HandlerIdentity is { } identity &&
                StaticPostProcessHandlers.TryGetValue(identity, out var handler): handler(reference); return;
            default: throw new InvalidDataException($"Required complete static post-process callback {callback.Kind}/{callback.HandlerIdentity} is not bound.");
        }
    }
    //The simulation context itself owns CWorldData. Startup pointer tokens
    //are not another world owner and must not be fabricated for a Turn.
    internal RegisteredPersonPruningResult ApplyOwnedTurnPostProcess()
    {
        if (OwnedIndexes is null) throw new InvalidDataException("Turn slot7 requires the current owned pool.");
        StaticTablePostProcess.Apply(this);
        return PostProcessRegisteredPersons(worldReceiverOwned: true);
    }

    public RegisteredPersonPruningResult ApplyStaticPostLoadFunction()
    {
        StaticTablePostProcess.Apply(this);
        return PostProcessRegisteredPersons();
    }
}
