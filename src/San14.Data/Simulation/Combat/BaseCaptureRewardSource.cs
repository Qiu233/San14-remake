using San14.Core;

namespace San14.Data;

public sealed record BaseCaptureForceResultReceipt(bool NativeReturn, bool WorldGate,
    bool ResultGate, byte? FlagsAfter, int? ProgressAfter, int ProgressDelta);
public sealed record BaseCaptureForceResult(GameState? State,
    BaseCaptureForceResultReceipt? Receipt, string? UnresolvedOwner);
public sealed record BaseCaptureCityCallbackGate(bool? Reached, int? MatchingForcePhysical,
    string? UnresolvedOwner);
public sealed record BaseCaptureForce164Write(string Kind, int RequestedGain, int? Before164,
    int? After164, int? BeforeLevel, int? AfterLevel, bool NativeReturn, int? PlayerDisplayGain,
    bool PlayerReachedLevelTen);
public sealed record BaseCaptureTailReceipt(int Trait4C, int Effect6,
    int BeforeStateCount, int AfterStateCount, int BeforeRegionCount, int AfterRegionCount,
    BaseCaptureForce164Write[] ForceWrites, ushort? World1562Before, ushort? World1562After);
public sealed record BaseCaptureTailResult(GameState? State, BaseCaptureTailReceipt? Receipt,
    string? UnresolvedOwner);

/// <summary>2851B0's two real State/Region temporaries. This is a single
/// operation receipt, not a saved owner or a reconstructed territorial list.</summary>
public sealed record BaseCaptureTerritoryReceipt(HexListHandle States,
    HexListHandle Regions, int StateCount, int RegionCount);
public sealed record BaseCaptureTerritoryResult(BaseCaptureTerritoryReceipt? Receipt,
    string? UnresolvedOwner);

/// <summary>Finite Base kind5/6 callbacks inside2851B0. Callers supply their
/// same private current world. These leaves do not capture a Base, retire an
/// Army, perform terminal handling or replace the enclosing source operation.</summary>
public static partial class BaseCaptureRewardSource
{
    public static BaseCaptureForceResult ApplyInitialForceResult(GameState input,
        SourceWorldContext current, ForceReference winner, BaseReference target)
    {
        try
        {
            if (!K(current.ReadForceNativeValidity(winner), "257670 Force validity"))
                return new(input, new(false, false, false, null, null, 0), null);
            int slot = K(current.ReadForcePhysicalSlot(winner), "257670 Force physical");
            var force = Force(input, slot);
            int oldForce = ReadBaseForce(input, current, target);
            _ = ReadBaseKind(current, target);
            var resolution = ForceCombatObjectiveRules.Resolve(new ForceCombatObjectiveCurrent(input, slot, current,
                new NormalArmyForceResultDefinitions(current),
                () => new(0, P(input, SimulationParameterKeys.ArmyDestruction.BaseCaptureResultGain)), true),
                oldForce, 0, true) ?? throw Missing("force.combat_objective");
            if (!resolution.Triggered)
                return new(input, new(false, false, false, null, null, 0), null);
            byte? nextFlags = resolution.MarkSelectedForceDefeated ? (byte)(force.Flags12!.Value | 4) : null;
            var simulation = input.NativeArmySimulation!;
            var domainForces = input.Forces;
            if (resolution.ProgressAfter is int progress)
                domainForces = new(domainForces) { [slot] = domainForces[slot] with
                    { CombatObjective = domainForces[slot].CombatObjective! with { Progress = progress } } };
            var saved = input with { NativeArmySimulation = simulation with { Forces = new(simulation.Forces)
                { [slot] = force with { Flags12 = nextFlags ?? force.Flags12 } } }, Forces = domainForces };
            return new(saved, new(true, resolution.SelectedForceMatched, resolution.ObjectiveMatched,
                nextFlags, resolution.ProgressAfter, resolution.ProgressDelta), null);
        }
        catch (MissingOwner error) { return new(null, null, error.Message); }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    /// <summary>2851B0 City-only C0/Force160 scan. False is a reached native
    /// skip; true requires the separate2A2620 callback and is not completion.</summary>
    public static BaseCaptureCityCallbackGate ReadInitialCityCallbackGate(GameState input,
        SourceWorldContext current, BaseReference target)
    {
        try
        {
            if (ReadBaseKind(current, target) == 6) return new(false, null, null);
            ushort location = K(current.ReadBaseObjectLink(target), "2851B0 Base4E");
            var forces = current.ReadRegistryReferences(RegistryKind.Force) ?? throw Missing("2851B0 current ForceC0 chain");
            foreach (var row in forces)
            {
                if (row?.Kind != RegistryKind.Force || row.PhysicalSlot is not int physical)
                    throw Missing("2851B0 ForceC0 payload");
                //The source does not test each Force's native validity here.
                if (K(input.Forces.GetValueOrDefault(physical)?.CombatObjective?.TargetLocationId,
                    "force.objective.target_location") == location)
                    return new(true, physical, null);
            }
            return new(false, null, null);
        }
        catch (MissingOwner error) { return new(null, null, error.Message); }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    public static BaseCaptureTerritoryResult BeginTerritoryRewards(GameState state,
        SourceWorldContext current, ForceReference force)
    {
        HexListHandle? states = null, regions = null;
        try
        {
            states = Temporary(current); regions = Temporary(current);
            int stateCount = FillStates(state, current, force, states);
            int regionCount = FillRegions(state, current, force, states, regions);
            return new(new(states, regions, stateCount, regionCount), null);
        }
        catch (Exception error) when (error is MissingOwner or InvalidDataException)
        {
            if (regions is not null) Release(current, regions);
            if (states is not null) Release(current, states);
            return new(null, error.Message);
        }
    }

    /// <summary>Apply after actual26DA10/terminal gate and source29F220.
    /// Enclosing caller must skip this tail on the native terminal branch.
    /// The two real temporaries are consumed and released at this final tail.</summary>
    public static BaseCaptureTailResult ApplyTail(GameState input,
        SourceWorldContext current, BaseReference target, ForceReference winner,
        DistrictReference winnerDistrict, ArmyUnitReference winnerArmy,
        BaseCaptureTerritoryReceipt territory)
    {
        BaseCaptureTailResult result;
        try { result = ApplyTailBody(input, current, target, winner, winnerDistrict, winnerArmy, territory); }
        finally { Release(current, territory.Regions); Release(current, territory.States); }
        if (result.State is null) return result;
        try { return result with { State = WithCurrentPool(result.State, current) }; }
        catch (Exception error) when (error is MissingOwner or InvalidDataException)
        { return new(null, null, error.Message); }
    }

    private static BaseCaptureTailResult ApplyTailBody(GameState input,
        SourceWorldContext current, BaseReference target, ForceReference winner,
        DistrictReference winnerDistrict, ArmyUnitReference winnerArmy,
        BaseCaptureTerritoryReceipt territory)
    {
        try
        {
            var working = input; var writes = new List<BaseCaptureForce164Write>();
            int trait = 0, effect6 = 0, afterStates = territory.StateCount, afterRegions = territory.RegionCount;
            if (K(current.ReadForceNativeValidity(winner), "2851B0 late Force validity"))
            {
                int forceSlot = K(current.ReadForcePhysicalSlot(winner), "2851B0 late Force physical");
                if (K(current.ReadArmyUnitNativeValidity(winnerArmy), "2851B0 late Army validity"))
                {
                    ushort commanderId = K(current.ReadArmyUnitCommanderId(winnerArmy), "2851B0 late Army12");
                    var commander = current.ReadPersonLookupReference(commanderId <= 6000 ? commanderId : 0)
                        ?? throw Missing("2851B0 late commander pointer");
                    if (commander.PhysicalSlot is int person && K(current.ReadPersonNativeValidity(person), "2851B0 late commander validity"))
                        trait = unchecked(K(TacticRemakeCurrentTraits.CommanderEffectSum(current, person, 0x4C), "2851B0 effect4C") * P(working, SimulationParameterKeys.ArmyDestruction.ForceExperienceTraitMultiplier));
                }
                var advantage = PersonReturnPathSource.ReadForceAdvantage(working, current, winner, 6);
                effect6 = K(advantage.Value, advantage.UnresolvedOwner ?? "2851B0 effect6");
                int effectTerm = unchecked(effect6 * P(working, SimulationParameterKeys.ArmyDestruction.ForceExperienceLandAdvantageMultiplier));
                Grant("Base", SimulationParameterKeys.ArmyDestruction.BaseCaptureExperienceGain);
                afterStates = FillStates(working, current, winner, territory.States);
                if (afterStates > territory.StateCount) Grant("State", SimulationParameterKeys.ArmyDestruction.StateCaptureExperienceGain);
                afterRegions = FillRegions(working, current, winner, territory.States, territory.Regions);
                if (afterRegions > territory.RegionCount) Grant("Region", SimulationParameterKeys.ArmyDestruction.RegionCaptureExperienceGain);

                void Grant(string label, string key)
                {
                    int scale = P(working, key);
                    int gain = unchecked(unchecked(effectTerm * scale) / 100 + unchecked(trait * scale) / 100 + scale);
                    if ((K(Force(working, forceSlot).Flags12, "2851B0 late Force12") & 8) != 0)
                        gain = unchecked(gain + unchecked(scale * P(working, SimulationParameterKeys.ArmyDestruction.ForceExperienceFlagBonusPercent)) / 100);
                    if (gain < 0) { writes.Add(new(label, gain, null, null, null, null, false, null, false)); return; }
                    int before = K(current.ReadForce164(winner), "258A60 late Force164");
                    int oldLevel = K(OwnedForce164Inputs.ReadLevel(working, before), "258A60 before level");
                    int after = Math.Clamp(unchecked(before + gain), 0, 9999999);
                    int newLevel = K(OwnedForce164Inputs.ReadLevel(working, after), "258A60 after level");
                    var player = current.ReadCurrentPlayerForce() ?? throw Missing("258A60 current Player Force");
                    bool isPlayer = K(current.ReadForceNativeValidity(player), "258A60 Player Force validity") && player == winner;
                    int? display = null;
                    if (isPlayer && gain > 0 && oldLevel != 10)
                    {
                        int shown = newLevel == 10 ? unchecked(K(OwnedForce164Inputs.ReadMaximumThreshold(working), "258A60 level10 threshold") - before) : gain;
                        if (shown > 0) display = shown;
                    }
                    current.SetForce164(forceSlot, after);
                    var simulation = working.NativeArmySimulation!;
                    working = working with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with
                        { Int32Values = new(simulation.Parameters.Int32Values) { [OwnedForce164Inputs.ForceValueKey(forceSlot)] = after } } } };
                    writes.Add(new(label, gain, before, after, oldLevel, newLevel, oldLevel < newLevel, display,
                        isPlayer && oldLevel < newLevel && newLevel == 10));
                }
            }
            ushort? beforeWorld = null, afterWorld = null;
            if (ReadBaseKind(current, target) == 5 && K(current.ReadDistrictNativeValidity(winnerDistrict), "2851B0 late District validity") &&
                PlayerRulerDistrict(current, winnerDistrict))
            {
                beforeWorld = K(current.ReadCaptureWorld1562(), "2851B0 World1562");
                afterWorld = (ushort)Math.Min(9999, (int)beforeWorld.Value + 1);
                current.SetCaptureWorld1562(afterWorld);
                working = BaseCaptureCurrentInputs.WithWorld1562(working, afterWorld);
            }
            return new(working, new(trait, effect6, territory.StateCount, afterStates, territory.RegionCount, afterRegions,
                writes.ToArray(), beforeWorld, afterWorld), null);
        }
        catch (Exception error) when (error is MissingOwner or InvalidDataException)
        {
            return new(null, null, error.Message);
        }
    }

    public static void ReleaseTerritoryRewards(SourceWorldContext current,
        BaseCaptureTerritoryReceipt receipt)
    { Release(current, receipt.Regions); Release(current, receipt.States); }

    private static bool PlayerRulerDistrict(SourceWorldContext current, DistrictReference district)
    {
        var player = current.ReadCurrentPlayerForce() ?? throw Missing("233A90 Player Force");
        if (!K(current.ReadForceNativeValidity(player), "233A90 Player validity")) return false;
        int physical = K(district.PhysicalSlot, "233A90 District pointer");
        if (K(current.ReadDistrictRawByte(physical, 0x10), "233A90 District10") != K(current.ReadForcePhysicalSlot(player), "233A90 Player physical")) return false;
        ushort leader = K(current.ReadDistrictLeader(physical), "233A90 District12");
        var person = current.ReadPersonLookupReference(leader <= 6000 ? leader : 0) ?? throw Missing("233A90 leader pointer");
        return person.PhysicalSlot is int slot && K(current.ReadPersonNativeValidity(slot), "233A90 leader validity") &&
            K(current.ReadPersonStatus(slot), "233A90 leader E6") == 1;
    }
    private static int ReadBaseKind(SourceWorldContext current, BaseReference target)
    {
        int link = K(current.ReadBaseObjectLink(target), "2851B0 Base4E");
        return link is >= 1 and <= 51 ? 5 : link is >= 52 and <= 61 ? 6 : throw Missing("branch.2851B0 unsupported Base kind");
    }
    private static NativeArmyForceState Force(GameState state, int slot) => state.NativeArmySimulation?.Forces.GetValueOrDefault(slot)
        ?? throw Missing("2851B0 saved current Force fields");
    private static int P(GameState state, string key) => K(ArmyDestructionParameterSeed.ReadCurrent(state, key), $"2851B0 parameter{key}");
    private static HexListHandle Temporary(SourceWorldContext current)
        => current.CreateTemporaryHexList() ?? throw Missing("2851B0 temporary allocator");
    private static void Release(SourceWorldContext current, HexListHandle handle)
    { current.ClearTemporaryHexList(handle); current.ReleaseTemporaryHexList(handle); }
    private static GameState WithCurrentPool(GameState state, SourceWorldContext current) => state with
    { NativeArmyLifecycle = state.NativeArmyLifecycle! with { Indexes = (current.ReadOwnedCurrentListPool() ?? throw Missing("2851B0 current primary pool")).ExportOwnedState() } };
    private static T K<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static MissingOwner Missing(string name) => new("current.BaseCaptureReward: " + name);
    private sealed class MissingOwner(string message) : Exception(message);
}
