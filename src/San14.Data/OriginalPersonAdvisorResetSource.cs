using San14.Core;

namespace San14.Data;

public sealed record OriginalAdvisorBaseMoraleWrite(OriginalBaseReference Base, byte Before44, byte After44);
public sealed record OriginalAdvisorArmyMoraleWrite(int PhysicalSlot, byte Before1A, byte After1A);
public sealed record OriginalAdvisorCityRefresh(int CityPhysical, int DurabilityBefore, int DurabilityAfter,
    bool DurabilitySetterReached, byte Flag66After);
public sealed record OriginalPersonAdvisorResetReceipt(int ForcePhysical, int? Policy14Level, byte? Force15EAfter,
    OriginalAdvisorBaseMoraleWrite[] Bases, OriginalAdvisorArmyMoraleWrite[] Armies,
    OriginalAdvisorCityRefresh[] Cities, OriginalForcePolicyScratchReceipt[] PolicyQueries);
public sealed record OriginalPersonAdvisorResetResult(GameState? State, OriginalPersonAdvisorResetReceipt? Receipt, string? UnresolvedOwner);

/// <summary>Shared current27BE40 morale body:26DA10 calls it alone,
/// while29C430's advisor-removal caller clears52 before all three refreshes.
/// The caller owns its private current world and discards it on unknown.</summary>
public static class OriginalPersonAdvisorResetSource
{
    public static string City66Key(int city) => "ce-advisor-refresh-current:city:" + city + ":66";
    public static void BindCurrent(GameState state, OriginalEventInitializationContext current)
    {
        foreach (var city in state.Cities.Keys)
            if (state.NativeArmySimulation?.Parameters.Int32Values.TryGetValue(City66Key(city), out int value) == true && value is >= 0 and <= 255)
                current.SetBasePostLoadFlag66(new(OriginalBaseKind.City, city), (byte)value);
    }

    public static OriginalPersonAdvisorResetResult ApplyCurrent(GameState state,
        OriginalEventInitializationContext current, OriginalCurrentListPool pool, OriginalForceReference force)
        => ApplyRefreshCurrent(state, current, pool, force, advisorRemoved: true);

    /// <summary>26DA10's changed-Force27BE40 only. Retains the current
    /// advisor52/cache15E and does not reach27AA40 or2FF040.</summary>
    public static OriginalPersonAdvisorResetResult RefreshCurrentForce(GameState state,
        OriginalEventInitializationContext current, OriginalForceReference force)
        => ApplyRefreshCurrent(state, current, null, force, advisorRemoved: false);

    private static OriginalPersonAdvisorResetResult ApplyRefreshCurrent(GameState state,
        OriginalEventInitializationContext current, OriginalCurrentListPool? pool, OriginalForceReference force, bool advisorRemoved)
    {
        try
        {
            int physical = Need(force.PhysicalSlot, "Force_pointer");
            if (!Need(current.ReadForceNativeValidity(force), "Force_validity"))
                return new(state, new(physical, advisorRemoved ? 0 : null, advisorRemoved ? (byte)0 : null, [], [], [], []), null);
            if (physical is < 1 or > 45 || advisorRemoved && Need(current.ReadAssignmentForceAdvisor52(physical), "cleared_Force52") != 0)
                throw Missing("branch.ordinary_cleared_advisor52");
            pool ??= current.ReadOwnedCurrentListPool() ?? throw Missing("current_owned_primary_pool");
            var simulation = state.NativeArmySimulation ?? throw Missing("saved_parameter_owner");
            var values = advisorRemoved ? new Dictionary<string, int>(simulation.Parameters.Int32Values)
                { [OriginalBattleRemakeCurrentForcePolicies.ForceFieldKey(physical, 0x52)] = 0 } : simulation.Parameters.Int32Values;
            var working = state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } } };
            var policies = new List<OriginalForcePolicyScratchReceipt>();
            current.BaseForcePolicyEffectReader = (owner, effect) => ReadPolicy(owner, effect, true);
            var bases = new List<OriginalAdvisorBaseMoraleWrite>();
            var armies = new List<OriginalAdvisorArmyMoraleWrite>();
            var cities = new List<OriginalAdvisorCityRefresh>();

            //27BE40: the initial cap, then live manager80 Base iterator,
            //then live manager60 Army iterator. Setters take fresh own caps.
            int cap = OriginalBasePostLoad.ReadMoraleUpper(force, current);
            foreach (var entry in current.ReadRegisteredBases() ?? throw Missing("manager80_Base_chain"))
            {
                var target = entry ?? throw Missing("manager80_Base_payload");
                if (Need(current.ReadBaseForceSlot(target), "Base_current_Force") != physical) continue;
                if (!Need(current.ReadBaseNativeValidity(target), "Base_validity")) continue;
                byte before = Need(current.ReadBaseMorale44(target), "Base44");
                OriginalBasePostLoad.WriteMorale(target, Math.Min((int)before, cap), current);
                bases.Add(new(target, before, Need(current.ReadBaseMorale44(target), "Base44_after")));
            }
            foreach (var entry in OriginalArmyMovementManagerLifecycle.ReadCurrentPointerOrder(current))
            {
                int slot = Need(entry.PhysicalSlot, "manager60_Army_pointer");
                var army = new OriginalArmyUnitReference(slot);
                if (current.ReadArmyUnitForceReference(army).PhysicalSlot != physical) continue;
                if (Need(OriginalOwnedArmyDestructionCurrent.ReadArmyExcluded233560(working, current, slot), "233560_pending_Army")) continue;
                if (!Need(current.ReadArmyUnitNativeValidity(army), "Army_validity")) continue;
                var fields = current.ReadArmyReturnResourceFields(army) ?? throw Missing("Army_resource_fields");
                byte before = Need(fields.Morale1A, "Army1A");
                int getter = OriginalBasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(army), current); //22EB30.
                int requested = Math.Min(Math.Min((int)before, getter), cap);
                int setter = OriginalBasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(army), current); //241720.
                byte after = unchecked((byte)(requested < 0 ? 0 : Math.Min(requested, setter)));
                current.SetArmyReturnResourceFields(slot, fields with { Morale1A = after });
                armies.Add(new(slot, before, after));
            }

            int? level = null; byte? cache = null;
            if (advisorRemoved)
            {
                //27AA40 uses3130E0's level, not CPolicyEffect's mapped value.
                level = ReadPolicy(force, 14, false);
                int high = Need(OriginalArmyDestructionParameterSeed.ReadCurrent(working, SimulationParameterKeys.ArmyDestruction.AdvisorHighThreshold), "Force15E_high_threshold");
                cache = level >= high ? (byte)13 : (byte)(11 +
                    (level >= Need(OriginalArmyDestructionParameterSeed.ReadCurrent(working, SimulationParameterKeys.ArmyDestruction.AdvisorLowThreshold), "Force15E_low_threshold") ? 1 : 0));
                values[OriginalBattleRemakeCurrentForcePolicies.ForceFieldKey(physical, 0x15E)] = cache.Value;

                //2FF040 uses the City pointer table, not manager90/manager80.
                for (int slot = 0; slot < 52; slot++)
                {
                    var target = current.ReadCityTableReference(slot) ?? throw Missing("DD38_City_pointer");
                    if (Need(current.ReadBaseForceSlot(target), "City_Force_iterator") != physical) continue;
                    if (!Need(current.ReadBaseNativeValidity(target), "City_validity") ||
                        !Need(current.ReadBaseNativeValidity(target), "City_repeated_validity") ||
                        Need(current.ReadBaseObjectLink(target), "City4E") >= 52) continue;
                    int upper = OriginalBasePostLoad.ReadCapacities(target, current).Field46;
                    int before = OriginalBasePostLoad.ReadLinkedField14(target, current);
                    bool writes = upper <= before;
                    if (writes) OriginalBasePostLoad.WriteLinkedField14(target, upper, current);
                    int after = OriginalBasePostLoad.ReadLinkedField14(target, current);
                    byte flag = OriginalBasePostLoad.ReadFlag66(target, current);
                    current.SetBasePostLoadFlag66(target, flag);
                    cities.Add(new(slot, before, after, writes, flag)); values[City66Key(slot)] = flag;
                }
            }

            var cityStates = new Dictionary<int, CityState>(working.Cities);
            var gateStates = new Dictionary<int, GateState>(working.Gates);
            foreach (var write in bases)
            {
                int id = Need(write.Base.PhysicalSlot, "Base_publish_pointer");
                if (write.Base.Kind == OriginalBaseKind.Gate)
                {
                    int location = id + 51;
                    var gate = gateStates[location];
                    gateStates[location] = gate with { NativeCombatRuntime =
                        (gate.NativeCombatRuntime ?? throw Missing("Gate44_saved_owner")) with { Morale44 = write.After44 } };
                    continue;
                }
                var row = cityStates[id];
                cityStates[id] = row with { Morale = write.After44, NativeReturnRuntime =
                    (row.NativeReturnRuntime ?? throw Missing("City44_saved_owner")) with { Morale44 = write.After44 } };
            }
            foreach (var write in cities.Where(x => x.DurabilitySetterReached))
                cityStates[write.CityPhysical] = cityStates[write.CityPhysical] with { Durability = write.DurabilityAfter };
            var units = new Dictionary<int, UnitState>(working.Units);
            var slots = (working.NativeArmyLifecycle ?? throw Missing("Army_saved_arena")).Slots.ToArray();
            foreach (var write in armies)
            {
                var slot = slots[write.PhysicalSlot];
                if (slot.ActiveUnitId is int id) units[id] = units[id] with { NativeMorale1A = write.After1A };
                else if (slot.PendingImage is { } image) slots[write.PhysicalSlot] = slot with { PendingImage = image with { NativeMorale1A = write.After1A } };
                else throw Missing("Army_publish_image");
            }
            working = working with { Cities = cityStates, Gates = gateStates, Units = units, NativeArmySimulation = working.NativeArmySimulation! with
                { Parameters = working.NativeArmySimulation.Parameters with { Int32Values = values } },
                NativeArmyLifecycle = working.NativeArmyLifecycle with { Slots = slots, Pool = pool.ExportOwnedState() } };
            return new(working, new(physical, level, cache, bases.ToArray(), armies.ToArray(), cities.ToArray(), policies.ToArray()), null);

            int ReadPolicy(OriginalForceReference owner, int effect, bool mapped)
            {
                var receipt = OriginalForcePolicyScratchCurrent.ReadBeforeFreedom(working, current, owner, effect, mapped);
                policies.Add(receipt); return receipt.Value;
            }
        }
        catch (InvalidDataException unknown) { return new(null, null, unknown.Message); }
    }
    private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing("current." + field);
    private static InvalidDataException Missing(string field) => new("Advisor reset source unresolved: " + field);
}
