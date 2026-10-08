namespace San14.Data;

public sealed record OriginalArmyUnitReference(int? PhysicalSlot);
public sealed record OriginalPersonLinkedEntityReference(int? Identity);
public readonly record struct OriginalNativeLocation(int Kind, int Index);
public sealed record OriginalPersonPositionRepairResult(IReadOnlyList<ushort> FirstLocationWrites,
    IReadOnlyList<ushort> SecondLocationWrites);

public interface IOriginalPersonPositionRepairState
{
    byte? ReadPersonStatus(int physicalSlot);
    bool? ReadBaseNativeValidity(OriginalBaseReference reference);
    ushort? ReadPersonFirstLocation(int physicalSlot);
    ushort? ReadPersonSecondLocation(int physicalSlot);
    ushort? ReadPersonIdentity(int physicalSlot);
    OriginalArmyUnitReference? ReadArmyUnitTableReference(int slot);
    ushort? ReadArmyUnitCommanderId(OriginalArmyUnitReference reference);
    bool? ReadArmyUnitNativeValidity(OriginalArmyUnitReference reference);
    OriginalPersonLinkedEntityReference? ReadPersonLinkedEntity(int physicalSlot);
    bool? ReadLinkedEntityNativeValidity(OriginalPersonLinkedEntityReference reference);
    OriginalAreaReference? ReadAreaTableReference(int slot);
    byte? ReadAreaCitySlot(OriginalAreaReference reference);
    OriginalBaseReference? ReadCityTableReference(int slot);
    ushort? ReadBaseObjectLink(OriginalBaseReference reference);
    void WritePersonFirstLocation(int physicalSlot, ushort location);
    void WritePersonSecondLocation(int physicalSlot, ushort location);
}

/// <summary>224F4B..225324. Uses B's saved base and old force; no rollback of completed writes.</summary>
public static class OriginalPersonPositionRepair
{
    public static OriginalPersonPositionRepairResult Apply(int personPhysicalSlot,
        OriginalPersonBaseSyncResult saved, IOriginalPersonPositionRepairState state)
    {
        ArgumentNullException.ThrowIfNull(saved); ArgumentNullException.ThrowIfNull(state);
        if (personPhysicalSlot is < 0 or >= 1851)
            throw new InvalidDataException("Person position repair is outside the supported CE physical table.");
        var firstWrites = new List<ushort>(); var secondWrites = new List<ushort>();
        byte status = Known(state.ReadPersonStatus(personPhysicalSlot), "C current status11E");
        if (status is >= 1 and <= 4 && Known(state.ReadBaseNativeValidity(saved.Base), "C saved base validity") &&
            saved.CachedOldForceSlot is >= 1 and <= 51)
        {
            OriginalNativeLocation first = DecodeFirstLocation(Known(state.ReadPersonFirstLocation(personPhysicalSlot), "C first11A"));
            OriginalNativeLocation second = DecodeSecondLocation(Known(state.ReadPersonSecondLocation(personPhysicalSlot), "C second11C"));
            if (first != second)
            {
                OriginalArmyUnitReference unit = Pointer(state.ReadArmyUnitTableReference(second.Kind == 27 ? second.Index : 0), "C army unit");
                ushort id = Known(state.ReadPersonIdentity(personPhysicalSlot), "C current person ID10");
                ushort commander = Known(state.ReadArmyUnitCommanderId(unit), "C army commander12");
                if (commander != id) unit = Pointer(state.ReadArmyUnitTableReference(0), "C fallback army unit");
                OriginalPersonLinkedEntityReference linked = Pointer(state.ReadPersonLinkedEntity(personPhysicalSlot), "C linked entity");
                if (!Known(state.ReadLinkedEntityNativeValidity(linked), "C linked entity validity") &&
                    !Known(state.ReadArmyUnitNativeValidity(unit), "C saved army validity"))
                    WriteSecond();
            }
        }

        // 225166 captures status once. E uses this same byte even after D's writer callbacks.
        byte laterStatus = Known(state.ReadPersonStatus(personPhysicalSlot), "D/E cached status11E");
        if (laterStatus == 5)
        {
            OriginalNativeLocation second = DecodeSecondLocation(Known(state.ReadPersonSecondLocation(personPhysicalSlot), "D second11C"));
            if (second.Kind == 10)
            {
                OriginalAreaReference area = Pointer(state.ReadAreaTableReference(second.Index), "D area");
                byte rawCity = Known(state.ReadAreaCitySlot(area), "D current area city35");
                int citySlot = rawCity <= 51 ? rawCity : 0;
                OriginalBaseReference city = Pointer(state.ReadCityTableReference(citySlot), "D city comparison");
                ushort comparedLink = Known(state.ReadBaseObjectLink(city), "D city link4E comparison");
                ushort currentFirst = Known(state.ReadPersonFirstLocation(personPhysicalSlot), "D first11A comparison");
                if (comparedLink != currentFirst)
                {
                    city = Pointer(state.ReadCityTableReference(citySlot), "D city request");
                    ushort currentLink = Known(state.ReadBaseObjectLink(city), "D city link4E request");
                    ushort value = (ushort)Math.Min((int)currentLink, 61);
                    state.WritePersonFirstLocation(personPhysicalSlot, value);
                    firstWrites.Add(value);
                }
            }
        }
        if (laterStatus == 6)
        {
            OriginalNativeLocation second = DecodeSecondLocation(Known(state.ReadPersonSecondLocation(personPhysicalSlot), "E second11C"));
            if (second.Kind == 27)
            {
                OriginalArmyUnitReference unit = Pointer(state.ReadArmyUnitTableReference(second.Index), "E army unit");
                if (!Known(state.ReadArmyUnitNativeValidity(unit), "E army validity")) WriteSecond();
            }
        }
        return new(firstWrites.AsReadOnly(), secondWrites.AsReadOnly());

        void WriteSecond()
        {
            ushort current = Known(state.ReadPersonFirstLocation(personPhysicalSlot), "fresh first11A for second write");
            ushort value = (ushort)Math.Min((int)current, 1061);
            state.WritePersonSecondLocation(personPhysicalSlot, value);
            secondWrites.Add(value);
        }
    }

    public static OriginalNativeLocation DecodeFirstLocation(ushort value) => value switch
    {
        >= 1 and <= 51 => new(5, value),
        >= 52 and <= 61 => new(6, value - 51),
        _ => new(5, 0)
    };
    public static OriginalNativeLocation DecodeSecondLocation(ushort value) => value switch
    {
        >= 562 and <= 1061 => new(10, value - 561),
        >= 62 and <= 561 => new(27, value - 61),
        >= 52 and <= 61 => new(6, value - 51),
        >= 1 and <= 51 => new(5, value),
        _ => new(255, 0)
    };
    private static T Known<T>(T? value, string name) where T : struct => value
        ?? throw new InvalidDataException($"Current person position input {name} is unknown.");
    private static T Pointer<T>(T? value, string name) where T : class => value
        ?? throw new InvalidDataException($"Current person position {name} pointer query is unknown.");
}
