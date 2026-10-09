using San14.Core;

namespace San14.Data;

/// <summary>Translates resource registry views at the boundary. Core owns settlement state and rules.</summary>
public static class ArmyDestructionQueue
{
    public static GameState InstallFreshOwned(GameState state)
    {
        if (state.PendingCombatSettlements is not null) throw new InvalidDataException("Combat settlements are already initialized.");
        return state with { PendingCombatSettlements = new([]) };
    }

    public static int? ResolveUnitId(GameState state, int? physical)
    {
        if (physical is not int slot || state.NativeArmyLifecycle is not { } lifecycle || (uint)slot >= lifecycle.Slots.Length) return null;
        var entry = lifecycle.Slots[slot];
        return entry.ActiveUnitId ?? entry.PendingImage?.Id;
    }
    public static int? ResolveUnitSlot(GameState state, int unitId) => state.NativeArmyLifecycle?.Slots
        .FirstOrDefault(slot => slot.ActiveUnitId == unitId || slot.PendingImage?.Id == unitId)?.PhysicalSlot;

    public static SettlementBase? ResolveBase(SourceWorldContext current, BaseReference reference)
    {
        if (reference.PhysicalSlot is null || current.ReadBaseObjectLink(reference) is not ushort location) return null;
        return new(reference.Kind == BaseKind.City ? SettlementBaseKind.City : SettlementBaseKind.Gate, location);
    }
    public static BaseReference ResolveBaseReference(GameState state, SourceWorldContext current, SettlementBase target)
    {
        if (target.Kind == SettlementBaseKind.City && !state.Cities.ContainsKey(target.Id) ||
            target.Kind == SettlementBaseKind.Gate && !state.Gates.ContainsKey(target.Id))
            throw new InvalidDataException("Settlement base is missing from the current world.");
        var kind = target.Kind == SettlementBaseKind.City ? BaseKind.City : BaseKind.Gate;
        // The original resource table identities are translated only here.
        int count = kind == BaseKind.City ? 52 : 11;
        for (int physical = 1; physical < count; physical++)
        {
            var reference = new BaseReference(kind, physical);
            if (current.ReadBaseObjectLink(reference) == target.Id) return reference;
        }
        throw new InvalidDataException("Settlement base has no resource registry mapping.");
    }
    public static ICombatSettlementQueries BindCurrent(GameState state, SourceWorldContext current,
        bool readSavedArmyValidity = false) => new CurrentQueries(state, current, readSavedArmyValidity);

    private sealed class CurrentQueries(GameState state, SourceWorldContext current,
        bool readSavedArmyValidity) : ICombatSettlementQueries
    {
        public bool? IsUnitValid(int unitId)
        {
            if (ResolveUnitSlot(state, unitId) is not int physical) return false;
            if (!readSavedArmyValidity) return current.ReadArmyUnitNativeValidity(new(physical));
            var slot = state.NativeArmyLifecycle!.Slots[physical];
            return slot.Commander12 is ushort commander ? commander == 0 ? false : slot.Status10 is byte status ? status != 0 : null : null;
        }
        public bool? IsBaseValid(SettlementBase target) => current.ReadBaseNativeValidity(ResolveBaseReference(state, current, target));
        public bool? IsCaptureExclusionApplicable(int winnerUnitId)
        {
            if (ResolveUnitSlot(state, winnerUnitId) is not int physical) return false;
            ushort? commander = readSavedArmyValidity ? state.NativeArmyLifecycle!.Slots[physical].Commander12 : current.ReadArmyUnitCommanderId(new(physical));
            if (commander is null) return null;
            var person = commander <= 6000 ? current.ReadPersonLookupReference(commander.Value) : current.ReadTransferPersonTableReference(0);
            if (person?.PhysicalSlot is not int identity || current.ReadPersonNativeValidity(identity) is not bool valid) return null;
            byte? force;
            if (valid) force = current.ReadPersonOldForceSlot(identity);
            else
            {
                var district = current.ReadDistrictTableReference(0);
                if (district?.PhysicalSlot is not int districtId || current.ReadDistrictNativeValidity(districtId) is not bool districtValid) return null;
                force = districtValid ? current.ReadDistrictForceId(districtId) : (byte)0;
            }
            // Source categories for special participants do not exclude a conquering army.
            return force is byte category ? category is not (>= 46 and <= 51) : null;
        }
    }
}
