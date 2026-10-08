using San14.Core;

namespace San14.Data;

public sealed record OriginalBaseCaptureForceResultReceipt(bool NativeReturn, bool WorldGate,
    bool ResultGate, byte? Flags12After, ushort? Value162After, int Delta162);
public sealed record OriginalBaseCaptureForceResult(GameState? State,
    OriginalBaseCaptureForceResultReceipt? Receipt, string? UnresolvedOwner);
public sealed record OriginalBaseCaptureCityCallbackGate(bool? Reached, int? MatchingForcePhysical,
    string? UnresolvedOwner);
public sealed record OriginalBaseCaptureForce164Write(string Kind, int RequestedGain, int? Before164,
    int? After164, int? BeforeLevel, int? AfterLevel, bool NativeReturn, int? PlayerDisplayGain,
    bool PlayerReachedLevelTen);
public sealed record OriginalBaseCaptureTailReceipt(int Trait4C, int Effect6,
    int BeforeStateCount, int AfterStateCount, int BeforeRegionCount, int AfterRegionCount,
    OriginalBaseCaptureForce164Write[] ForceWrites, ushort? World1562Before, ushort? World1562After);
public sealed record OriginalBaseCaptureTailResult(GameState? State, OriginalBaseCaptureTailReceipt? Receipt,
    string? UnresolvedOwner);

/// <summary>2851B0's two real State/Region temporaries. This is a single
/// operation receipt, not a saved owner or a reconstructed territorial list.</summary>
public sealed record OriginalBaseCaptureTerritoryReceipt(OriginalHexListHandle States,
    OriginalHexListHandle Regions, int StateCount, int RegionCount);
public sealed record OriginalBaseCaptureTerritoryResult(OriginalBaseCaptureTerritoryReceipt? Receipt,
    string? UnresolvedOwner);

/// <summary>Finite Base kind5/6 callbacks inside2851B0. Callers supply their
/// same private current world. These leaves do not capture a Base, retire an
/// Army, perform terminal handling or replace the enclosing source operation.</summary>
public static partial class OriginalBaseCaptureRewardSource
{
    public static OriginalBaseCaptureForceResult ApplyInitialForceResult(GameState input,
        OriginalEventInitializationContext current, OriginalForceReference winner, OriginalBaseReference target)
    {
        try
        {
            if (!K(current.ReadForceNativeValidity(winner), "257670 Force validity"))
                return new(input, new(false, false, false, null, null, 0), null);
            int slot = K(current.ReadForcePhysicalSlot(winner), "257670 Force physical");
            var force = Force(input, slot); var world = input.NativeArmySimulation!.World;
            int oldForce = ReadBaseForce(input, current, target);
            _ = BaseKind(current, target);
            byte worldFlags = K(world.Flags165C, "257670 World165C");
            bool worldGate = false;
            byte? flags = null;
            if (worldFlags != 0)
            {
                flags = K(force.Flags12, "257670 Force12");
                if ((flags.Value & 6) == 2)
                    worldGate = oldForce == K(world.SelectedForce1656, "257670 World1656");
            }
            bool resultGate = false; ushort? before = null;
            byte gate = K(force.ResultGate15F, "257670 Force15F");
            if (gate is >= 1 and <= 51)
            {
                before = K(force.ResultValue162, "257670 Force162");
                if (before < 60000)
                {
                    byte definition = K(force.ResultDefinition160, "257670 Force160");
                    var resultBase = OriginalBaseLookup.Find(definition, current);
                    resultGate = oldForce == ReadBaseForce(input, current, resultBase);
                }
            }
            if (!worldGate && !resultGate)
                return new(input, new(false, false, false, null, null, 0), null);
            byte? nextFlags = worldGate ? (byte)(flags!.Value | 4) : null;
            //Base caller uses loss0/killed1 with a known null second pointer.
            //It therefore reaches only this Base constant, never Army498FC.
            int delta = resultGate ? P(input, 0x1A49914) : 0;
            ushort? next = resultGate ? (ushort)Math.Clamp(unchecked(before!.Value + delta), 0, 60000) : null;
            var simulation = input.NativeArmySimulation!;
            var saved = input with { NativeArmySimulation = simulation with { Forces = new(simulation.Forces)
                { [slot] = force with { Flags12 = nextFlags ?? force.Flags12, ResultValue162 = next ?? force.ResultValue162 } } } };
            return new(saved, new(true, worldGate, resultGate, nextFlags, next, delta), null);
        }
        catch (MissingOwner error) { return new(null, null, error.Message); }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    /// <summary>2851B0 City-only C0/Force160 scan. False is a reached native
    /// skip; true requires the separate2A2620 callback and is not completion.</summary>
    public static OriginalBaseCaptureCityCallbackGate ReadInitialCityCallbackGate(GameState input,
        OriginalEventInitializationContext current, OriginalBaseReference target)
    {
        try
        {
            if (BaseKind(current, target) == 6) return new(false, null, null);
            ushort location = K(current.ReadBaseObjectLink(target), "2851B0 Base4E");
            var forces = current.ReadRegistryReferences(OriginalRegistryKind.Force) ?? throw Missing("2851B0 current ForceC0 chain");
            foreach (var row in forces)
            {
                if (row?.Kind != OriginalRegistryKind.Force || row.PhysicalSlot is not int physical)
                    throw Missing("2851B0 ForceC0 payload");
                //The source does not test each Force's native validity here.
                if (K(Force(input, physical).ResultDefinition160, "2851B0 ForceC0 byte160") == location)
                    return new(true, physical, null);
            }
            return new(false, null, null);
        }
        catch (MissingOwner error) { return new(null, null, error.Message); }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    public static OriginalBaseCaptureTerritoryResult BeginTerritoryRewards(GameState state,
        OriginalEventInitializationContext current, OriginalForceReference force)
    {
        OriginalHexListHandle? states = null, regions = null;
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
    public static OriginalBaseCaptureTailResult ApplyTail(GameState input,
        OriginalEventInitializationContext current, OriginalBaseReference target, OriginalForceReference winner,
        OriginalDistrictReference winnerDistrict, OriginalArmyUnitReference winnerArmy,
        OriginalBaseCaptureTerritoryReceipt territory)
    {
        OriginalBaseCaptureTailResult result;
        try { result = ApplyTailBody(input, current, target, winner, winnerDistrict, winnerArmy, territory); }
        finally { Release(current, territory.Regions); Release(current, territory.States); }
        if (result.State is null) return result;
        try { return result with { State = WithCurrentPool(result.State, current) }; }
        catch (Exception error) when (error is MissingOwner or InvalidDataException)
        { return new(null, null, error.Message); }
    }

    private static OriginalBaseCaptureTailResult ApplyTailBody(GameState input,
        OriginalEventInitializationContext current, OriginalBaseReference target, OriginalForceReference winner,
        OriginalDistrictReference winnerDistrict, OriginalArmyUnitReference winnerArmy,
        OriginalBaseCaptureTerritoryReceipt territory)
    {
        try
        {
            var working = input; var writes = new List<OriginalBaseCaptureForce164Write>();
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
                        trait = unchecked(K(OriginalTacticRemakeCurrentTraits.CommanderEffectSum(current, person, 0x4C), "2851B0 effect4C") * P(working, 0x1A490B4));
                }
                var advantage = OriginalPersonReturnPathSource.ReadForceAdvantage(working, current, winner, 6);
                effect6 = K(advantage.Value, advantage.UnresolvedOwner ?? "2851B0 effect6");
                int effectTerm = unchecked(effect6 * P(working, 0x1A49BB8));
                Grant("Base", 0x1A4906C);
                afterStates = FillStates(working, current, winner, territory.States);
                if (afterStates > territory.StateCount) Grant("State", 0x1A49078);
                afterRegions = FillRegions(working, current, winner, territory.States, territory.Regions);
                if (afterRegions > territory.RegionCount) Grant("Region", 0x1A49084);

                void Grant(string label, int rva)
                {
                    int scale = P(working, rva);
                    int gain = unchecked(unchecked(effectTerm * scale) / 100 + unchecked(trait * scale) / 100 + scale);
                    if ((K(Force(working, forceSlot).Flags12, "2851B0 late Force12") & 8) != 0)
                        gain = unchecked(gain + unchecked(scale * P(working, 0x1A49794)) / 100);
                    if (gain < 0) { writes.Add(new(label, gain, null, null, null, null, false, null, false)); return; }
                    int before = K(current.ReadForce164(winner), "258A60 late Force164");
                    int oldLevel = K(OriginalOwnedForce164Inputs.ReadLevel(working, before), "258A60 before level");
                    int after = Math.Clamp(unchecked(before + gain), 0, 9999999);
                    int newLevel = K(OriginalOwnedForce164Inputs.ReadLevel(working, after), "258A60 after level");
                    var player = current.ReadCurrentPlayerForce() ?? throw Missing("258A60 current Player Force");
                    bool isPlayer = K(current.ReadForceNativeValidity(player), "258A60 Player Force validity") && player == winner;
                    int? display = null;
                    if (isPlayer && gain > 0 && oldLevel != 10)
                    {
                        int shown = newLevel == 10 ? unchecked(K(OriginalOwnedForce164Inputs.ReadMaximumThreshold(working), "258A60 level10 threshold") - before) : gain;
                        if (shown > 0) display = shown;
                    }
                    current.SetForce164(forceSlot, after);
                    var simulation = working.NativeArmySimulation!;
                    working = working with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with
                        { Int32Values = new(simulation.Parameters.Int32Values) { [OriginalOwnedForce164Inputs.ForceValueKey(forceSlot)] = after } } } };
                    writes.Add(new(label, gain, before, after, oldLevel, newLevel, oldLevel < newLevel, display,
                        isPlayer && oldLevel < newLevel && newLevel == 10));
                }
            }
            ushort? beforeWorld = null, afterWorld = null;
            if (BaseKind(current, target) == 5 && K(current.ReadDistrictNativeValidity(winnerDistrict), "2851B0 late District validity") &&
                PlayerRulerDistrict(current, winnerDistrict))
            {
                beforeWorld = K(current.ReadCaptureWorld1562(), "2851B0 World1562");
                afterWorld = (ushort)Math.Min(9999, (int)beforeWorld.Value + 1);
                current.SetCaptureWorld1562(afterWorld);
                working = OriginalBaseCaptureCurrentInputs.WithWorld1562(working, afterWorld);
            }
            return new(working, new(trait, effect6, territory.StateCount, afterStates, territory.RegionCount, afterRegions,
                writes.ToArray(), beforeWorld, afterWorld), null);
        }
        catch (Exception error) when (error is MissingOwner or InvalidDataException)
        {
            return new(null, null, error.Message);
        }
    }

    public static void ReleaseTerritoryRewards(OriginalEventInitializationContext current,
        OriginalBaseCaptureTerritoryReceipt receipt)
    { Release(current, receipt.Regions); Release(current, receipt.States); }

    private static bool PlayerRulerDistrict(OriginalEventInitializationContext current, OriginalDistrictReference district)
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
    private static int BaseKind(OriginalEventInitializationContext current, OriginalBaseReference target)
    {
        int link = K(current.ReadBaseObjectLink(target), "2851B0 Base4E");
        return link is >= 1 and <= 51 ? 5 : link is >= 52 and <= 61 ? 6 : throw Missing("branch.2851B0 unsupported Base kind");
    }
    private static NativeArmyForceState Force(GameState state, int slot) => state.NativeArmySimulation?.Forces.GetValueOrDefault(slot)
        ?? throw Missing("2851B0 saved current Force fields");
    private static int P(GameState state, int rva) => K(OriginalArmyDestructionParameterSeed.ReadCurrentTc(state, rva), $"2851B0 parameter{rva:X}");
    private static OriginalHexListHandle Temporary(OriginalEventInitializationContext current)
        => current.CreateTemporaryHexList() ?? throw Missing("2851B0 temporary allocator");
    private static void Release(OriginalEventInitializationContext current, OriginalHexListHandle handle)
    { current.ClearTemporaryHexList(handle); current.ReleaseTemporaryHexList(handle); }
    private static GameState WithCurrentPool(GameState state, OriginalEventInitializationContext current) => state with
    { NativeArmyLifecycle = state.NativeArmyLifecycle! with { Pool = (current.ReadOwnedCurrentListPool() ?? throw Missing("2851B0 current primary pool")).ExportOwnedState() } };
    private static T K<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static MissingOwner Missing(string name) => new("current.BaseCaptureReward: " + name);
    private sealed class MissingOwner(string message) : Exception(message);
}
