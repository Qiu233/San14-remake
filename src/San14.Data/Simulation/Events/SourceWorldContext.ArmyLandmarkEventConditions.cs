using San14.Data.Formats;

namespace San14.Data;

public sealed partial class SourceWorldContext
{
    public ForceArmyCityPresence? ReadConditionForceArmyCityPresence(
        S14EventConditionForceReference force, S14EventConditionCityPointer city)
    {
        if (ReadRegistryReferences(RegistryKind.Army) is not { } armies) return null;
        bool Eligible(RegistryObjectReference? entry)
        {
            if (force.PhysicalSlot is not (>= 1 and <= 51)) return false;
            if (entry is not { Kind: RegistryKind.Army, PhysicalSlot: int physical })
                throw new InvalidDataException("Current force army registration pointer is unknown or null.");
            if (ReadArmyUnitRawForceSlot(new(physical)) != force.PhysicalSlot) return false;
            return !(ReadOwnedArmyExclusion233560(entry)
                ?? throw new InvalidDataException("Current force army exclusion is unknown."));
        }
        // The iterator distance is a complete first pass, not an Any query.
        int count = 0;
        foreach (var entry in armies) if (Eligible(entry)) count++;
        if (count == 0) return new(false, false);

        bool found = false;
        foreach (var entry in armies)
        {
            if (!Eligible(entry)) continue;
            ushort raw = ReadArmyUnitRawHex2A(new(entry!.PhysicalSlot))
                ?? throw new InvalidDataException("Current event army hex is unknown.");
            var hex = ReadHexTableReference(raw < 48400 ? raw : 0)
                ?? throw new InvalidDataException("Current event army hex pointer is unknown.");
            if (!hex.PhysicalSlot.HasValue) continue; // Every nonnull CHex is valid.
            ushort rawArea = ReadHexAreaSlot(hex)
                ?? throw new InvalidDataException("Current event army hex area is unknown.");
            var area = ReadAreaTableReference(rawArea < 501 ? rawArea : 0)
                ?? throw new InvalidDataException("Current event army area pointer is unknown.");
            bool valid = ReadAreaNativeValidity(area)
                ?? throw new InvalidDataException("Current event army area validity is unknown.");
            if (!valid) continue;
            byte original = ReadAreaValidityCode(area)
                ?? throw new InvalidDataException("Current event army original city is unknown.");
            int target = city.PhysicalSlot
                ?? throw new InvalidDataException("Current event army target city pointer is null.");
            if (original == target) found = true;
            // A hit never skips the remaining registered actors.
        }
        return new(true, found);
    }

    public Func<int, byte?>? ConditionSceneryLevelReader { get; set; }
    public SceneryReference? ReadConditionSceneryTableSlot(int slot) => ReadSceneryTableReference(slot);
    public bool? ReadConditionSceneryValidity(SceneryReference scenery) => ReadSceneryNativeValidity(scenery);
    public byte? ReadConditionSceneryFlags(SceneryReference scenery) => ReadSceneryFlags47(scenery);
    public byte? ReadConditionSceneryLevel(SceneryReference scenery) => scenery.PhysicalSlot is int physical
        ? ConditionSceneryLevelReader?.Invoke(physical) : null;
    public byte? ReadConditionSceneryForceSlot(SceneryReference scenery)
    {
        ushort? raw = ReadSceneryHexIndex(scenery);
        if (raw is null) return null;
        var hex = ReadHexTableReference(raw.Value < 48400 ? raw.Value : 0);
        if (hex is null) return null;
        if (!hex.PhysicalSlot.HasValue) return 0;
        if (ReadSceneryFlags47(scenery) is not byte flags) return null;
        return (flags & 1) != 0 ? ReadHexForceSlot(hex) : (byte)0;
    }
}
