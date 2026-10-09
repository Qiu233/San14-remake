using San14.Core;

namespace San14.Data;

public sealed record TacticPrimaryArmyCandidatesSource(int RangeHexes,
    int[] OrderedArmyIds, ulong RandomState);

public sealed partial class TacticRemakeCurrentContext
{
    /// <summary>Current Army-only170660 membership/count projection. Reached
    /// hostile Base/Object qualifications remain unknown, so this result never
    /// silently drops non-Army candidates. Sorting by capped morale is a later
    /// operation and cannot change this complete membership or count.</summary>
    public TacticPrimaryArmyCandidatesSource? ResolveArmyNativePrimaryCandidates(GameState state, UnitState caster)
    {
        if (ArmyValid(state, caster) != true || caster.NativeTacticRuntime is not { } native ||
            caster.NativeArmyConfiguration?.Action37 is not byte action || caster.ForceId is >= 46 and <= 51)
            return null;
        if (action == 0x10) return null; //2C8EF0 reaches additional owner/path inputs.
        var preparation = new ArmyLogisticsPreparation(this, _definitions, _map, _terrain);
        int? home = preparation.ReadCommanderBaseHex(state, caster);
        bool? excluded = ArmyExcluded(state, caster.Id);
        if (home is null || excluded is null) return null;
        int? range = ReadArmyNativePrimaryRange(state, caster);
        if (range is null) return null;
        if (caster.Troops == 0 || native.Abnormal23 > 0 || home == caster.HexId || excluded.Value)
            return new(range.Value, [], state.RandomState);
        if (caster.Troops is < 0 or > ushort.MaxValue) return null;
        var ids = new List<int>();
        foreach (int hex in NativeHexOrder(caster.HexId, Math.Min(range.Value, 3)).Skip(1))
        {
            if (!TryOccupant(state, hex, out Occupant? occupant)) return null;
            if (occupant is null) continue;
            if (native.Abnormal25 > 0)
            {
                if (native.ProvocationOwner is not { } owner) return null;
                if (occupant.Army is { } candidateArmy)
                { if (owner.Kind != DeploymentTargetKind.Unit || owner.Id != candidateArmy.Id) continue; }
                else if (occupant.BaseLocation is int location)
                { if (owner.Kind != DeploymentTargetKind.Location || owner.Id != location) continue; }
                else return null;
            }
            int? force = occupant.Army?.ForceId ?? (occupant.CapturedObject is { } obj
                ? _definitions.ReadObjectForceSlot13(obj) : null);
            if (force is null) return null;
            bool? hostile = ArmyLogisticsPreparation.ReadNativeRelationGate(state, caster.ForceId, force.Value);
            if (hostile is null) return null;
            if (!hostile.Value) continue;
            //170900 keeps qualifying Base targets in the descriptor/count14
            //even when a later pair consumer skips their pending destruction.
            //This Army-only projection cannot silently subtract that target.
            if (occupant.Army is not { } target) return null;
            if (target.ForceId is >= 46 and <= 51 || target.Troops is < 0 or > ushort.MaxValue) return null;
            bool? targetExcluded = ArmyExcluded(state, target.Id);
            int? targetHome = preparation.ReadCommanderBaseHex(state, target);
            if (targetExcluded is null || targetHome is null) return null;
            if (targetExcluded.Value || targetHome == target.HexId) continue;
            //170660 retains a valid zero-troop Army snapshot. The separate
            //16FBC0 predicted-damage comparison decides its tactic eligibility.
            ids.Add(target.Id);
        }
        return new(range.Value, ids.ToArray(), state.RandomState);
    }

    /// <summary>Army branch of16CA90→2C6F20 over current owned formation
    /// bytes/vector. This is the primary attack-candidate radius; Raw64 in a
    /// tactic instruction is a separate effect-selection radius.</summary>
    public int? ReadArmyNativePrimaryRange(GameState state, UnitState army)
    {
        if (ArmyValid(state, army) != true || CurrentSurfaceCode(army) is not byte surface ||
            army.NativeArmyConfiguration is not { } configuration) return null;
        int terrainSlot = surface <= 30 ? surface : 0;
        byte? selected = (_terrain.Catalog.Types[terrainSlot].RawField2C & 2) != 0
            ? configuration.Formation1C : configuration.Formation1B;
        if (selected is null) return null;
        int formation = selected <= 20 ? selected.Value : 0;
        byte? bits = TacticRemakeCurrentFormations.ReadParameter(state, formation, 0xA6);
        if (bits is null) return null;
        //TC22C990 supplies the current Force pointer to2C70A0. Force193's
        //high bit forces formation10's range bit; this is not an Army byte.
        if (formation == 10)
        {
            if (state.NativeArmySimulation?.Forces.GetValueOrDefault(army.ForceId)?.Flags193 is not byte flags) return null;
            if ((flags & 0x80) != 0) return 2;
        }
        return 1 + (bits.Value & 1);
    }
}
