using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Read-only268690(Person,actualCHex,emitReport=0). No random
    /// sample, report, merit, morale, fire write or preview Army is created.
    /// Endpoints are already resolved physical Hex identities.</summary>
    public UnitFireProbability QueryUnitsFireProbability(GameState state,
        IReadOnlyList<int> unitIds, int targetHexId)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(unitIds);
        try
        {
            if (unitIds.Count is < 1 or > 30 || unitIds.Distinct().Count() != unitIds.Count ||
                targetHexId is < 0 or >= 48400) throw FirePreviewMissing("selected-set-or-target");
            var current = CaptureWritableTransportRouteCurrent(state);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            _baseCapacityInputs.BindCurrent(state, current);
            return ReadUnitsFireProbability(state, current, unitIds, new(targetHexId));
        }
        catch (InvalidDataException error)
        {
            return new(targetHexId, Array.AsReadOnly(unitIds.Select(id =>
                new UnitFireProbabilityRow(id, null, null, error.Message)).ToArray()), null, error.Message);
        }
    }

    private UnitFireProbability ReadUnitsFireProbability(GameState state,
        SourceWorldContext current, IReadOnlyList<int> unitIds, HexReference target)
    {
        CaptureBaseThreatInputs.BindCurrent(state, current);
        var rows = new List<UnitFireProbabilityRow>(unitIds.Count);
        foreach (int id in unitIds)
        {
            try
            {
                if (!state.Units.TryGetValue(id, out var unit)) throw FirePreviewMissing("selected-unit");
                var person = current.ReadPersonLookupReference(unit.OfficerId <= 6000 ? unit.OfficerId : 0)
                    ?? throw FirePreviewMissing("selected-person-pointer");
                int rate = person.PhysicalSlot is int physical && FirePreviewKnown(current.ReadPersonNativeValidity(physical), "selected-person-validity")
                    ? ReadFireProbability(state, current, physical, target) : 0;
                rows.Add(new(id, rate, Math.Clamp(rate, 0, 100), null));
            }
            catch (InvalidDataException error) { rows.Add(new(id, null, null, error.Message)); }
        }
        string? missing = rows.FirstOrDefault(row => row.UnresolvedInput is not null)?.UnresolvedInput;
        return new(FirePreviewKnown(target.PhysicalSlot, "target-physical"), rows.AsReadOnly(),
            missing is null ? rows.Max(row => row.DisplayRate!.Value) : null, missing);
    }

    private int ReadFireProbability(GameState state, SourceWorldContext current,
        int person, HexReference target)
    {
        if (target.PhysicalSlot is not int physicalHex) return 0;
        int code = FirePreviewKnown(current.ReadHexSurfaceCode(target), "target-surface");
        var terrain = current.ReadSurfaceTypeTableReference(code <= 30 ? code : 0)
            ?? throw FirePreviewMissing("target-terrain-pointer");
        if (terrain.PhysicalSlot is not int terrainPhysical) return 0;
        var obj = FacilityConstructionLegality.ObjectAt(current, target);
        //2C6D20 checks the integer physical grid index through the Hex table;
        //231660 then reads the occupant from the original target pointer.
        var lookupTarget = current.ReadHexTableReference(physicalHex) ?? throw FirePreviewMissing("occupancy-test-Hex-pointer");
        int testOccupant = unchecked((short)FirePreviewKnown(current.ReadHexRaw1C(lookupTarget), "occupancy-test-Hex1C"));
        ArmyUnitReference? occupant = null;
        if (testOccupant is >= 1 and <= 500)
        {
            int raw = unchecked((short)FirePreviewKnown(current.ReadHexRaw1C(target), "target-Hex1C"));
            var candidate = current.ReadArmyUnitTableReference(raw <= 500 && raw >= 0 ? raw : 0)
                ?? throw FirePreviewMissing("target-Army-pointer");
            if (FirePreviewKnown(current.ReadArmyUnitNativeValidity(candidate), "target-Army-validity")) occupant = candidate;
        }
        bool Protected(int owner) => FirePreviewKnown(current.ReadPersonNativeValidity(owner), "protected-person-validity") &&
            FirePreviewKnown(TacticRemakeCurrentTraits.CommanderHasEffect(current, owner, 25), "protected-trait25");
        int Commander(ArmyUnitReference army) => FireCommandPerson(current,
            FirePreviewKnown(current.ReadArmyUnitCommanderId(army), "target-commander12"));
        if (occupant is not null && Protected(Commander(occupant))) return 0;

        int defender = 0, abnormal = 0;
        var basis = FireCommandBase(current, obj);
        if (basis is not null)
        {
            if (FirePreviewKnown(current.ReadBaseNativeValidity(basis), "target-Base-validity"))
            {
                int governor = FireCommandPerson(current, FirePreviewKnown(current.ReadArmyReturnBaseGovernor2E(basis), "target-governor2E"));
                if (FirePreviewKnown(current.ReadPersonNativeValidity(governor), "target-governor-validity"))
                {
                    if (Protected(governor)) return 0;
                    defender = FireCommandIntelligence(state, current, governor);
                }
                var fields = current.ReadCaptureBaseThreatFields(basis) ?? throw FirePreviewMissing("target-Base-abnormal");
                if (FirePreviewKnown(fields.Byte49, "target-Base49") != 0 || FirePreviewKnown(fields.Byte4B, "target-Base4B") != 0)
                    abnormal = Parameter(SimulationParameterKeys.FireCommand.AbnormalTargetBonus);
            }
        }
        else if (occupant is not null)
        {
            int commander = Commander(occupant);
            defender = FirePreviewKnown(current.ReadPersonNativeValidity(commander), "defender-validity")
                ? FireCommandIntelligence(state, current, commander) : 0;
            var runtime = FireCommandTargetRuntime(state, occupant);
            if (runtime.Abnormal23 != 0 || runtime.Abnormal24 != 0 || runtime.Abnormal25 != 0)
                abnormal = Parameter(SimulationParameterKeys.FireCommand.AbnormalTargetBonus);
        }
        if (FirePreviewKnown(TacticRemakeCurrentTraits.CommanderHasEffect(current, person, 25), "attacker-trait25")) return 100;
        if (FirePreviewKnown(TacticRemakeCurrentBattleControl.Read(state, 69), "tutorial69"))
            throw FirePreviewMissing("tutorial-selected-person");
        int category = _terrain.Catalog.Types[terrainPhysical].RawField41;
        int terrainBonus = category switch
        {
            1 => Parameter(SimulationParameterKeys.FireCommand.TerrainOneRate),
            2 => Parameter(SimulationParameterKeys.FireCommand.TerrainTwoRate),
            3 => Parameter(SimulationParameterKeys.FireCommand.TerrainThreeRate), _ => 0
        };
        int baseline = ArmyFireCommandProbabilityMath.Baseline(FireCommandIntelligence(state, current, person),
            defender, terrainBonus, abnormal, Parameter(SimulationParameterKeys.FireCommand.MinimumRate));
        int position = FirePreviewKnown(current.ReadPersonSecondLocation(person), "attacker-E4");
        var associated = current.ReadArmyUnitTableReference(position is >= 62 and <= 561 ? position - 61 : 0)
            ?? throw FirePreviewMissing("attacker-Army-pointer");
        int correction = 0;
        if (FirePreviewKnown(current.ReadArmyUnitNativeValidity(associated), "attacker-Army-validity") &&
            FirePreviewKnown(current.ReadArmyUnitCommanderId(associated), "attacker-Army-commander") == FirePreviewKnown(current.ReadPersonIdentity(person), "attacker-identity"))
        {
            int[] traits = TacticRemakeCurrentTraits.ArmyContributions(current, person,
                current.ReadArmyReturnTraitFields(associated)?.Traits64) ?? throw FirePreviewMissing("attacker-trait26");
            correction = ArmyFireCommandProbabilityMath.TraitCorrection(baseline, traits[26],
                Parameter(SimulationParameterKeys.FireCommand.TraitRateScale));
        }
        var force = current.ReadPersonForce(person) ?? throw FirePreviewMissing("attacker-Force-pointer");
        int panel = FirePreviewKnown(current.ReadForceNativeValidity(force), "attacker-Force-validity")
            ? FirePreviewKnown(_baseCapacityInputs.ReadPanelModifier(state, FirePreviewKnown(force.PhysicalSlot, "attacker-Force-physical"), 47), "attacker-panel47") : 0;
        return ArmyFireCommandProbabilityMath.ApplyModifiers(baseline, correction, panel);

        int Parameter(string key) => state.NativeArmySimulation?.Parameters is { } bank &&
            !bank.Float32Values.ContainsKey(key) && bank.Int32Values.TryGetValue(key, out int value)
            ? value : throw FirePreviewMissing("parameter." + key);
    }

    private static T FirePreviewKnown<T>(T? value, string field) where T : struct => value ?? throw FirePreviewMissing(field);
    private static InvalidDataException FirePreviewMissing(string field) => new("unit.fire-probability." + field);
}
