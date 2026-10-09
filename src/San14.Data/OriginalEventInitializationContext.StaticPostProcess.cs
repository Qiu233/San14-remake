namespace San14.Data;

public sealed partial class OriginalEventInitializationContext : IOriginalStaticTablePostProcessState
{
    private readonly Dictionary<(int Table, int Physical), OriginalPostLoadCallback?> _staticPostProcessCallbacks = [];
    /// <summary>Required complete handlers for remaining original callbacks.
    /// Partial operators must not be registered as complete slot7 callbacks.</summary>
    public Dictionary<string, Action<OriginalStaticPostProcessReference>> StaticPostProcessHandlers { get; } = new(StringComparer.Ordinal);
    public void SetStaticTablePostProcessCallback(int tableOffset, int physical, OriginalPostLoadCallback? callback)
    {
        _ = OriginalStaticTablePostProcess.SupportedCallback(tableOffset);
        Check(physical, StaticTableCount(tableOffset), "static slot7 receiver");
        _staticPostProcessCallbacks[(tableOffset, physical)] = callback;
    }
    public OriginalStaticPostProcessReference? ReadStaticPostProcessReference(int tableOffset, int slot)
    {
        _ = OriginalStaticTablePostProcess.SupportedCallback(tableOffset);
        OriginalStaticTableReference? reference = ReadStaticTableReference(tableOffset, slot);
        if (reference is null) return null;
        OriginalPostLoadCallback? callback = reference.PhysicalSlot.HasValue && _staticPostProcessCallbacks.TryGetValue((tableOffset, reference.PhysicalSlot.Value), out var current)
            ? current : OriginalStaticTablePostProcess.SupportedCallback(tableOffset);
        return new(tableOffset, reference.PhysicalSlot, callback);
    }
    public void InvokeStaticPostProcess(OriginalStaticPostProcessReference reference)
    {
        int physical = reference.PhysicalSlot ?? throw new InvalidDataException("Null static slot7 receiver.");
        var callback = reference.Callback ?? throw new InvalidDataException("Current static post-process callback is unknown.");
        switch (callback.Kind)
        {
            case OriginalPostLoadCallbackKind.NoOp: return;
            case OriginalPostLoadCallbackKind.City: PostProcessBase(new(OriginalBaseKind.City, physical)); return;
            case OriginalPostLoadCallbackKind.Gate: PostProcessBase(new(OriginalBaseKind.Gate, physical)); return;
            case OriginalPostLoadCallbackKind.Personality: PostProcessPersonality(new(physical)); return;
            case OriginalPostLoadCallbackKind.Policy: PostProcessPolicyEffect(physical); return;
            case OriginalPostLoadCallbackKind.Person: _ = PostProcessPerson(physical); return;
            case OriginalPostLoadCallbackKind.District:
                IEnumerable<int> persons = ProjectRegistry(OriginalRegistryKind.Person, person =>
                    person?.PhysicalSlot ?? throw new InvalidDataException("Current district slot7 person pointer is unknown or null."))
                    ?? throw new InvalidDataException("Current district slot7 person registry is unknown.");
                _ = OriginalDistrictPostLoad.Apply(physical, persons, this);
                return;
            case OriginalPostLoadCallbackKind.External when callback.HandlerIdentity is { } identity &&
                StaticPostProcessHandlers.TryGetValue(identity, out var handler): handler(reference); return;
            default: throw new InvalidDataException($"Required complete static post-process callback {callback.Kind}/{callback.HandlerIdentity} is not bound.");
        }
    }
    public OriginalRegisteredPersonPruningResult ApplyStaticPostLoadFunction()
    {
        OriginalStaticTablePostProcess.Apply(this);
        return PostProcessRegisteredPersons();
    }
}
