namespace San14.Data;

public sealed partial class SourceWorldContext : ISpecialModeForceInitializationState
{
    private readonly Dictionary<int, int?> _specialModePersonIds = [];
    private readonly Dictionary<int, int?> _forceRawFields164 = [];

    // Explicit current inputs: no guessed source/default world presence or mode.
    public bool? SpecialModeWorldPresent { get; set; }
    public int? RawWorldField48 { get; set; }
    public bool? ReadSpecialModeWorldPresent() => SpecialModeWorldPresent;
    public int? ReadSpecialModeWorldField48() => RawWorldField48;
    public void SetSpecialModePersonId(int entryIndex, int? id)
    { Check(entryIndex, 3, "special-mode world person entry"); _specialModePersonIds[entryIndex] = id; }
    public int? ReadSpecialModePersonId(int entryIndex)
    { Check(entryIndex, 3, "special-mode world person entry"); return _specialModePersonIds.GetValueOrDefault(entryIndex); }
    public ScenarioPersonReference? ReadSpecialModePersonTableReference(int id)
    {
        Check(id, 6001, "special-mode person ID lookup");
        SourceOfficerReference? reference = _officerSources.LogicalReference(id);
        return reference is null ? null : new(reference.Record);
    }
    public ScenarioPersonReference? ReadSpecialModeBasePersonReference() => _officerSources.Placeholder is { } reference ? new(reference.Record) : null;
    public bool? ReadSpecialModePersonNativeValidity(ScenarioPersonReference person)
    {
        ArgumentNullException.ThrowIfNull(person);
        return person.PhysicalSlot.HasValue ? ReadPersonNativeValidity(person.PhysicalSlot.Value) : false;
    }
    public ForceReference? ReadSpecialModePersonForce(ScenarioPersonReference person)
    {
        ArgumentNullException.ThrowIfNull(person);
        if (!person.PhysicalSlot.HasValue) throw new InvalidDataException("Null special-mode person cannot provide a force pointer.");
        return ReadPersonForce(person.PhysicalSlot.Value);
    }
    public void SetForceRawField164(int physicalSlot, int? value)
    { Check(physicalSlot, 52, "force raw164 physical slot"); _forceRawFields164[physicalSlot] = value; }
    public int? ReadForceRawField164(int physicalSlot)
    { Check(physicalSlot, 52, "force raw164 physical slot"); return _forceRawFields164.GetValueOrDefault(physicalSlot); }
    public void WriteForceRawField164(ForceReference force, int value)
    {
        ArgumentNullException.ThrowIfNull(force);
        if (!force.PhysicalSlot.HasValue) throw new InvalidDataException("Null force receiver at raw164 native write.");
        SetForceRawField164(force.PhysicalSlot.Value, value);
    }
}
