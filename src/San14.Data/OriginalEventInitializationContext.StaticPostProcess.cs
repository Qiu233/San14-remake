namespace San14.Data;

public sealed partial class OriginalEventInitializationContext : IOriginalStaticTablePostProcessState
{
    private readonly Dictionary<(int Table, int Physical), uint?> _staticSlot7Targets = [];
    /// <summary>Required complete handlers for remaining original callbacks.
    /// Partial operators must not be registered as complete slot7 callbacks.</summary>
    public Dictionary<uint, Action<OriginalStaticPostProcessReference>> StaticPostProcessHandlers { get; } = [];
    public void SetStaticTableSlot7Target(int tableOffset, int physical, uint? target)
    {
        _ = OriginalStaticTablePostProcess.SupportedTarget(tableOffset);
        Check(physical, StaticTableCount(tableOffset), "static slot7 receiver");
        _staticSlot7Targets[(tableOffset, physical)] = target;
    }
    public OriginalStaticPostProcessReference? ReadStaticPostProcessReference(int tableOffset, int slot)
    {
        _ = OriginalStaticTablePostProcess.SupportedTarget(tableOffset);
        OriginalStaticTableReference? reference = ReadStaticTableReference(tableOffset, slot);
        if (reference is null) return null;
        uint? target = reference.PhysicalSlot.HasValue && _staticSlot7Targets.TryGetValue((tableOffset, reference.PhysicalSlot.Value), out var current)
            ? current : OriginalStaticTablePostProcess.SupportedTarget(tableOffset);
        return new(tableOffset, reference.PhysicalSlot, target);
    }
    public void InvokeStaticPostProcess(OriginalStaticPostProcessReference reference)
    {
        int physical = reference.PhysicalSlot ?? throw new InvalidDataException("Null static slot7 receiver.");
        switch (reference.Slot7Target)
        {
            case 0x370500: return;
            case 0x2243E0: PostProcessBase(new(OriginalBaseKind.City, physical)); return;
            case 0x224750: PostProcessBase(new(OriginalBaseKind.Gate, physical)); return;
            case 0x2253A0: PostProcessPersonality(new(physical)); return;
            case 0x225410: PostProcessPolicyEffect(physical); return;
            case 0x224800: _ = PostProcessPerson(physical); return;
            case 0x2244C0:
                IEnumerable<int> persons = ProjectRegistry(OriginalRegistryKind.Person, person =>
                    person?.PhysicalSlot ?? throw new InvalidDataException("Current district slot7 person pointer is unknown or null."))
                    ?? throw new InvalidDataException("Current district slot7 person registry is unknown.");
                _ = OriginalDistrictPostLoad.Apply(physical, persons, this);
                return;
            case uint target when StaticPostProcessHandlers.TryGetValue(target, out var handler): handler(reference); return;
            default: throw new InvalidDataException($"Required complete static slot7 handler{reference.Slot7Target:X} is not bound.");
        }
    }
    public OriginalRegisteredPersonPruningResult ApplyStaticPostLoadFunction()
    {
        OriginalStaticTablePostProcess.Apply(this);
        return PostProcessRegisteredPersons();
    }
}
