using San14.Core;

namespace San14.Data;

public sealed record BaseWoundedRecoveryReceipt(int LocationId, int RequestedRecovery,
    int TroopsBefore, int TroopsAfter, int WoundedBefore, int WoundedAfter,
    byte MoraleBefore, byte MoraleAfter, int? EffectOfficerId, int TraitEffect);
public sealed record BaseWoundedRecoveryResult(GameState? State, BaseWoundedRecoveryReceipt[] Receipts, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Actual stage19: scheduled Base registry recovery, including
    /// eligibility, first eligible officer's trait and both troop setters.</summary>
    public BaseWoundedRecoveryResult TryAdvanceBaseWoundedRecovery(GameState state, NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.BaseWoundedRecovery ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry ||
            moment.PreWriterThirdDate.Hour is < 0 or > 22 || (moment.PreWriterThirdDate.Hour & 1) != 0)
            throw new InvalidDataException("Base wounded recovery requires reached source stage19.");
        if (moment.PreWriterThirdDate.Hour != 2 || moment.PreWriterThirdDate.Day % 10 is not (3 or 6 or 9))
            return new(state, [], null);
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            if (!_baseCapacityInputs.BindCurrent(state, current)) throw OccupationMissing("recovery_capacity_owner");
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            PersonReturnCommands.BindCurrent(state, current);
            var cities = new Dictionary<int, CityState>(state.Cities);
            var gates = new Dictionary<int, GateState>(state.Gates);
            var receipts = new List<BaseWoundedRecoveryReceipt>();
            foreach (var entry in current.ReadRegisteredBases() ?? throw OccupationMissing("recovery_base_registry"))
            {
                var target = entry ?? throw OccupationMissing("recovery_base_pointer");
                if (!OccupationKnown(current.ReadBaseNativeValidity(target), "recovery_base_validity")) continue;
                int ready = OccupationKnown(current.ReadBaseResource(target, BaseResourceKind.Troops), "recovery_ready");
                int Cap() => BasePostLoad.ReadResourceCapacities(target, current).Troops;
                if (ready >= Cap()) continue;
                int wounded = OccupationKnown(current.ReadArmyReturnBaseWounded40(target), "recovery_wounded");
                if (wounded < 1) continue;
                int location = OccupationKnown(current.ReadBaseObjectLink(target), "recovery_location");
                bool nearby = OccupationKnown(target.Kind == BaseKind.City
                    ? target.PhysicalSlot == 0 ? state.UnassignedCityHasNearbyEnemyArmy : cities.GetValueOrDefault(location)?.HasNearbyEnemyArmy
                    : target.PhysicalSlot == 0 ? state.UnassignedGateHasNearbyEnemyArmy : gates.GetValueOrDefault(location)?.HasNearbyEnemyArmy,
                    "recovery_nearby_enemy");
                if (nearby || RecoveryFootprintBlocked(current, target)) continue;
                int mode = OccupationKnown(BattleRemakeCurrentSettings.ReadWoundedRecoveryMode(state), "recovery_mode");
                int amount = mode switch
                {
                    0 => P(SimulationParameterKeys.BaseWoundedRecovery.SlowAmount),
                    1 => P(SimulationParameterKeys.BaseWoundedRecovery.NormalAmount),
                    2 => P(SimulationParameterKeys.BaseWoundedRecovery.FastAmount),
                    _ => 0 // The actual switch's default, not an unknown input.
                };
                int divisor = P(SimulationParameterKeys.BaseWoundedRecovery.WoundedDivisor);
                if (divisor == 0) throw OccupationMissing("recovery_zero_divisor");
                int baseline = Math.Min(wounded, unchecked(unchecked(wounded / divisor + 20) * amount) / 20);
                var effect = ReadFirstRecoveryTrait(current, location);
                int modifier = 0;
                if (effect.Value != 0)
                {
                    int scaled = unchecked(unchecked(P(SimulationParameterKeys.BaseWoundedRecovery.TraitMultiplier) * effect.Value) * baseline) / 100;
                    modifier = effect.Value < 0 ? Math.Min(-1, scaled) : Math.Max(1, scaled);
                }
                int recovered = Math.Min(wounded, unchecked(baseline + modifier));
                byte morale = OccupationKnown(current.ReadBaseMorale44(target), "recovery_morale");
                byte afterMorale = morale;
                //22ACF0 is a current governor lookup, including the actual invalid slot0.
                ushort governorId = OccupationKnown(current.ReadArmyReturnBaseGovernor2E(target), "recovery_governor");
                int governor = OccupationKnown(current.ReadPersonLookupReference(governorId <= 6000 ? governorId : 0)?.PhysicalSlot,
                    "recovery_governor_pointer");
                if (ready < Cap())
                {
                    int charm = 0;
                    if (OccupationKnown(current.ReadPersonNativeValidity(governor), "recovery_governor_validity"))
                    {
                        int id = OccupationKnown(current.ReadPersonIdentity(governor), "recovery_governor_identity");
                        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
                        charm = (abilities.Resolve(state, id) ?? throw OccupationMissing("recovery_governor_abilities")).Charm;
                    }
                    int mixed = morale;
                    if (recovered > 0)
                    {
                        int recoveredMorale = unchecked((int)Math.Sqrt(unchecked(charm * 10)) + P(SimulationParameterKeys.BaseWoundedRecovery.RecoveredMorale));
                        int numerator = unchecked(unchecked(ready * morale) + unchecked(recoveredMorale * recovered));
                        float value = MathF.Round((float)numerator / unchecked(ready + recovered), MidpointRounding.AwayFromZero);
                        mixed = !float.IsFinite(value) || value < int.MinValue || value >= 2147483648f ? int.MinValue : (int)value;
                    }
                    int forceId = OccupationKnown(current.ReadBaseForceSlot(target), "recovery_morale_force");
                    var force = current.ReadForceTableReference(forceId <= 51 ? forceId : 0) ?? throw OccupationMissing("recovery_morale_force_pointer");
                    int upper = OccupationKnown(current.ReadForceNativeValidity(force), "recovery_morale_force_validity")
                        ? BasePostLoad.ReadMoraleUpper(force, current) : 100;
                    BasePostLoad.WriteMorale(target, Bound(mixed, upper), current);
                    afterMorale = OccupationKnown(current.ReadBaseMorale44(target), "recovery_written_morale");
                }
                int afterReady = Bound(Bound(unchecked(ready + recovered), Cap()), Cap());
                current.SetBaseResource(target, BaseResourceKind.Troops, afterReady);
                int afterWounded = Bound(Bound(unchecked(wounded - recovered), Cap()), Cap());
                current.SetArmyReturnBaseWounded40(target, afterWounded);
                if (target.Kind == BaseKind.City && cities.TryGetValue(location, out var city) && city.NativeReturnRuntime is { } cityRuntime)
                    cities[location] = city with { Troops = afterReady, Morale = afterMorale,
                        NativeReturnRuntime = cityRuntime with { Wounded40 = afterWounded, Morale44 = afterMorale } };
                else if (target.Kind == BaseKind.Gate && gates.TryGetValue(location, out var gate) && gate.NativeCombatRuntime is { } gateRuntime)
                    gates[location] = gate with { Troops = afterReady, NativeCombatRuntime = gateRuntime with { Wounded40 = afterWounded, Morale44 = afterMorale } };
                else throw OccupationMissing("recovery_base_domain_owner");
                receipts.Add(new(location, recovered, ready, afterReady, wounded, afterWounded, morale, afterMorale, effect.OfficerId, effect.Value));
            }
            return new(state with { Cities = cities, Gates = gates }, receipts.ToArray(), null);
            int P(string key) => state.NativeArmySimulation?.Parameters is { } parameters &&
                !parameters.Float32Values.ContainsKey(key) && parameters.Int32Values.TryGetValue(key, out int value)
                ? value : throw OccupationMissing(key);
        }
        catch (InvalidDataException error) { return new(null, [], error.Message); }
    }

    private static bool RecoveryFootprintBlocked(SourceWorldContext current, BaseReference target)
    {
        var handle = current.ReadBaseHexListHandle(target) ?? throw OccupationMissing("recovery_footprint");
        var node = current.ReadHexListHead(handle) ?? throw OccupationMissing("recovery_footprint_head");
        while (node.Identity.HasValue)
        {
            var hex = current.ReadHexListNodePayload(node) ?? throw OccupationMissing("recovery_footprint_hex");
            if (OccupationKnown(current.ReadCaptureHexWord16(OccupationKnown(hex.PhysicalSlot, "recovery_hex_pointer")), "recovery_hex_blocker") != 0) return true;
            node = current.ReadHexListNodeNext(node) ?? throw OccupationMissing("recovery_footprint_next");
        }
        return false;
    }

    private static (int? OfficerId, int Value) ReadFirstRecoveryTrait(SourceWorldContext current, int location)
    {
        if (location is < 1 or > 61) return (null, 0);
        foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw OccupationMissing("recovery_person_registry"))
        {
            int person = entry ?? throw OccupationMissing("recovery_person_pointer");
            ushort first = OccupationKnown(current.ReadPersonFirstLocation(person), "recovery_person_home");
            if (first != location || OccupationKnown(current.ReadPersonStatus(person), "recovery_person_status") is not (>= 1 and <= 4)) continue;
            ushort second = OccupationKnown(current.ReadPersonSecondLocation(person), "recovery_person_position");
            if (PersonPositionRepair.DecodeFirstLocation(first) != PersonPositionRepair.DecodeSecondLocation(second)) continue;
            var linked = current.ReadPersonLinkedEntity(person) ?? throw OccupationMissing("recovery_person_associated");
            if (linked.Identity is int receiver && OccupationKnown(current.ReadLinkedEntityNativeValidity(linked), "recovery_associated_validity") &&
                current.ReadOwnedPersonCommandManager()?.Returns.ContainsKey(receiver) != true)
                throw OccupationMissing("recovery_associated_kind"); // Known Return virtual98=15, not busy16.
            if (!OccupationKnown(current.ReadPersonNativeValidity(person), "recovery_effect_person_validity")) continue;
            int effect = OccupationKnown(TacticRemakeCurrentTraits.CommanderEffectSum(current, person, 44), "recovery_person_trait");
            if (effect != 0) return (OccupationKnown(current.ReadPersonIdentity(person), "recovery_effect_person_identity"), effect);
        }
        return (null, 0);
    }
}
