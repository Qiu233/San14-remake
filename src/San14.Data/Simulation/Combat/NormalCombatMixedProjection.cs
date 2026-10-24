using San14.Core;

namespace San14.Data;

/// <summary>Values captured at actual actor/target creation. Target counts
/// remain zero; an actor receives its complete accepted count before sorting.</summary>
public sealed record NormalCombatParticipant(int Kind, int PhysicalSlot, int? UnitId,
    byte Morale, ushort Troops, int HexId, int ForceSlot, byte AcceptedCount);
public sealed record NormalCombatMixedPair(int GenerationOrdinal,
    NormalCombatParticipant Actor, NormalCombatParticipant Target);
public sealed record NormalCombatActorTargets(NormalCombatParticipant Actor,
    NormalCombatParticipant[] Targets, bool BaseHasFormationContact);
public sealed record NormalCombatMixedProjection(NormalCombatActorTargets[] Actors,
    NormalCombatMixedPair[] OrderedPairs);

/// <summary>Finite170240/170900 candidate production over current
/// saved registries. This module does not apply damage or consume App messages.</summary>
internal sealed class NormalCombatMixedProducer(GameState world, SourceWorldContext current,
    TacticRemakeCurrentContext tactics, ArmyMapGroupingCurrentQueries armies)
{
    internal NormalCombatMixedProjection Generate()
    {
        var actors = new List<NormalCombatActorTargets>();
        var pairs = new List<NormalCombatMixedPair>();
        foreach (int physical in armies.ReadRegistry() ?? throw Missing("Army_registry"))
        {
            if (Need(armies.IsAtHome2C96E0(physical), "Army_home") ||
                Need(armies.IsExcluded233560(physical), "Army_excluded")) continue;
            var army = Army(physical);
            var native = army.NativeTacticRuntime ?? throw Missing("Army_tactic_runtime");
            if (army.Troops == 0 || native.Abnormal23 != 0) continue;
            byte action = Need(ArmyConfigurationSourceAdapter.ReadAction(world, army), "Army_action");
            //170240 omits ordinary attack only when2C9070 actually says
            //this Fire command is ready; marching/blocked Fire still fights.
            if (action == 16 && native.Abnormal24 == 0 && native.Abnormal25 == 0 &&
                FireCommandIsReady(army, physical)) continue;
            int range = Need(tactics.ReadArmyNativePrimaryRange(world, army), "Army_range");
            Add(DescribeArmy(army), range, native.Abnormal25 != 0 ? native.ProvocationOwner
                ?? throw Missing("Army_provocation") : null, null);
        }
        AddBases(NormalBaseCombatSnapshots.ReadCityActors(world, current));
        AddBases(NormalBaseCombatSnapshots.ReadGateActors(world, current));
        //167100 is insertion with a non-strict source predicate: a later
        //equal actor of the same kind moves before an earlier one.
        for (int index = 1; index < pairs.Count; index++)
        {
            var value = pairs[index]; int at = index;
            while (at > 0 && Before(value, pairs[at - 1])) { pairs[at] = pairs[at - 1]; at--; }
            pairs[at] = value;
        }
        return new(actors.ToArray(), pairs.ToArray());

        void AddBases(NormalBaseCombatActorsResult result)
        {
            foreach (var row in result.Actors ?? throw Missing(result.UnresolvedOwner ?? "Base_actors"))
            {
                var snapshot = row.Snapshot;
                var reference = new BaseReference(snapshot.NativeKind4 == 5 ? BaseKind.City : BaseKind.Gate, snapshot.PhysicalSlot0);
                var fields = current.ReadCaptureBaseThreatFields(reference) ?? throw Missing("Base_forced_fields");
                (int Kind, int Id)? forced = Need(fields.Byte4B, "Base4B") == 0 ? null :
                    (Need(fields.Byte62, "Base62"), Need(fields.Word64, "Base64"));
                Add(FromBase(snapshot), row.RangeHexes, null, forced);
            }
        }
        void Add(NormalCombatParticipant actor, int radius, NativeTacticOwnerReference? armyForced, (int Kind, int Id)? baseForced)
        {
            var targets = new List<NormalCombatParticipant>(); bool formationContact = false;
            foreach (int hex in TacticRemakeCurrentContext.NativeHexOrder(actor.HexId, Math.Min(radius, 3)).Skip(1))
            {
                var read = FieldHexOccupantSource.ReadCurrent(world, current, hex);
                var occupant = read.Receipt ?? throw Missing(read.UnresolvedOwner ?? "Hex_occupant");
                if (occupant.NativeKind == 255) continue;
                if (baseForced is { } forced && (forced.Kind != occupant.NativeKind || forced.Id != occupant.NativePhysicalSlot)) continue;
                if (armyForced is { } selected)
                {
                    bool same = selected.Kind switch
                    {
                        DeploymentTargetKind.Unit => selected.Id == occupant.GameplayArmyId && occupant.NativeKind == 27,
                        DeploymentTargetKind.Location => selected.Id == occupant.BaseLocationId && occupant.NativeKind is 5 or 6,
                        _ => throw Missing("Army_forced_target_kind")
                    };
                    if (!same) continue;
                }
                NormalCombatParticipant target;
                if (occupant.NativeKind is 5 or 6)
                {
                    var found = BaseLookup.Find(occupant.BaseLocationId ?? throw Missing("Base_location"), current);
                    if (!Need(current.ReadBaseNativeValidity(found), "target_Base_valid")) continue;
                    int end = current.ReadBaseCurrentHexIndex(found);
                    //Source representative-distance gate is verified separately
                    //from city footprint membership; no city-center shortcut.
                    if (occupant.NativeKind == 6 && Distance(actor.HexId, end) > Math.Min(radius, 3)) continue;
                    int force = Need(current.ReadBaseForceSlot(found), "target_Base_force");
                    if (!Need(ArmyLogisticsPreparation.ReadNativeRelationGate(world, actor.ForceSlot, force), "Base_relation")) continue;
                    if (targets.Any(previous => previous.HexId == end)) continue;
                    if (actor.Kind != 27) throw Missing("Base_to_Base_application_scope");
                    var snapshot = found.Kind == BaseKind.City
                        ? NormalBaseCombatSnapshots.ReadCityTarget(world, current, found)
                        : NormalBaseCombatSnapshots.ReadGateTarget(world, current, found);
                    target = FromBase(snapshot ?? throw Missing("target_Base_snapshot"));
                }
                else if (occupant.NativeKind == 27)
                {
                    var army = Army(occupant.NativePhysicalSlot);
                    int force = Need(armies.ReadForce(occupant.NativePhysicalSlot)?.PhysicalSlot, "target_Army_force");
                    if (!Need(ArmyLogisticsPreparation.ReadNativeRelationGate(world, actor.ForceSlot, force), "Army_relation")) continue;
                    if (Need(armies.IsAtHome2C96E0(occupant.NativePhysicalSlot), "target_Army_home") ||
                        Need(armies.IsExcluded233560(occupant.NativePhysicalSlot), "target_Army_excluded")) continue;
                    target = DescribeArmy(army);
                    if (actor.Kind is 5 or 6 && Distance(actor.HexId, army.HexId) == (actor.Kind == 5 ? 2 : 1))
                        formationContact |= (Need(tactics.ReadOrdinaryFormationBits(world, army), "target_formation_A6") & 2) != 0;
                }
                else
                {
                    var obj = occupant.CapturedObject ?? throw Missing("Object_pointer");
                    int force = Need(current.ReadObjectForceSlot13(obj), "Object_force");
                    if (!Need(ArmyLogisticsPreparation.ReadNativeRelationGate(world, actor.ForceSlot, force), "Object_relation")) continue;
                    if (armyForced is null && baseForced is null)
                    {
                        var definition = FacilityConstructionLegality.DefinitionOf(current, obj);
                        if (!Need(current.ReadObjectDefinitionValidity(definition), "Object_definition_validity")) continue;
                        int category = Need(current.ReadObjectDefinitionRaw2C(definition), "Object_definition_category");
                        if (category != 2)
                        {
                            if (category != 3 || actor.Kind != 27) continue;
                            var army = Army(actor.PhysicalSlot);
                            bool explicitTarget = false;
                            if (Need(ArmyConfigurationSourceAdapter.ReadAction(world, army), "Object_actor_action") == 6)
                            {
                                var fields = current.ReadArmyLifecycleTargetFields(actor.PhysicalSlot) ?? throw Missing("Object_actor_target");
                                explicitTarget = Need(fields.Kind38, "Object_actor_target_kind") == 11 &&
                                    Need(fields.Identity3A, "Object_actor_target_identity") == occupant.NativePhysicalSlot;
                            }
                            if (!explicitTarget)
                            {
                                var temporary = ArmyOverrideTargetSourceAdapter.Read(world, army);
                                explicitTarget = Need(temporary.Kind3C, "Object_actor_override_kind") == 11 &&
                                    Need(temporary.Identity3E, "Object_actor_override_identity") == occupant.NativePhysicalSlot;
                            }
                            if (!explicitTarget) continue;
                        }
                    }
                    //170F31 clears byte8/wordA. These are not Object HP14.
                    int objectHex = Need(current.ReadObjectHexIndex(obj), "Object_hex");
                    target = new(11, occupant.NativePhysicalSlot, null, 0, 0,
                        Math.Min(objectHex, 48400), force, 0);
                }
                targets.Add(target);
            }
            if (targets.Count == 0) return;
            actor = actor with { AcceptedCount = checked((byte)targets.Count) };
            actors.Add(new(actor, targets.ToArray(), formationContact));
            foreach (var target in targets) pairs.Add(new(pairs.Count, actor, target));
        }
    }

    private bool FireCommandIsReady(UnitState army, int physical)
    {
        //Every admitted initial/existing Fire writer stores16/9/physical.
        //2B5610 reads that main target, never the first waypoint, override,
        //displayed destination or cached route endpoint.
        var fields = current.ReadArmyLifecycleTargetFields(physical) ?? throw Missing("Fire_main_target");
        if (Need(fields.Kind38, "Fire_main_target_kind") != 9) throw Missing("Fire_main_target_kind_scope");
        ushort raw = Need(fields.Identity3A, "Fire_main_target_identity");
        var goal = current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw Missing("Fire_Hex_pointer");
        if (goal.PhysicalSlot is null) return false;
        var city = CityAt(goal);
        if (Need(current.ReadBaseNativeValidity(city), "Fire_City_validity"))
            goal = CurrentGateMapQueries.FindHex(city, current);
        ushort link = Need(current.ReadHexObjectLink(goal), "Fire_Gate_link");
        var gate = current.ReadGateTableReference(link is >= 52 and <= 61 ? link - 51 : 0)
            ?? throw Missing("Fire_Gate_pointer");
        if (Need(current.ReadBaseNativeValidity(gate), "Fire_Gate_validity"))
            goal = CurrentGateMapQueries.FindHex(gate, current);
        int target = goal.PhysicalSlot ?? throw Missing("Fire_target_Hex_identity");
        if (Need(current.ReadCaptureHexWord16(target), "Fire_target_Hex16") != 0) return false;
        if (Need(ArmyMarchSourceAdapter.EncodeDestination(army.March?.Target), "Fire_first_waypoint") != 48400) return false;
        var following = army.March?.FollowingWaypoints ?? throw Missing("Fire_following_waypoints");
        //Omitted entries of the explicit continuation are known None. Preserve
        //the native ordered early return without reading unused unknown words.
        foreach (var point in following.Destinations)
            if (Need(ArmyMarchSourceAdapter.EncodeDestination(point), "Fire_following_waypoint") != 48400) return false;
        var obj = FacilityConstructionLegality.ObjectAt(current, goal);
        if (Need(current.ReadObjectNativeValidity(obj), "Fire_Object_validity"))
        {
            var definition = FacilityConstructionLegality.DefinitionOf(current, obj);
            if (Need(current.ReadObjectDefinitionValidity(definition), "Fire_definition_validity") &&
                Need(current.ReadObjectDefinitionRaw2C(definition), "Fire_definition_category") != 4 &&
                !Need(ArmyLogisticsPreparation.ReadNativeRelationGate(world,
                    current.ReadArmyUnitRawForceSlot(new(physical)),
                    Need(current.ReadObjectForceSlot13(obj), "Fire_Object_force")), "Fire_Object_relation")) return false;
        }
        //22A9E0 is deliberately repeated after both center normalizations.
        //It is a Base receiver validity test, not Terrain16 or a cached kind.
        city = CityAt(goal);
        int required = Need(current.ReadBaseNativeValidity(city), "Fire_distance_City_validity") ? 2 : 1;
        ushort position = Need(current.ReadArmyUnitRawHex2A(new(physical)), "Fire_Army2A");
        return Distance(position, target) == required;

        BaseReference CityAt(HexReference hex)
        {
            ushort rawLink = Need(current.ReadHexObjectLink(hex), "Fire_City_link");
            return current.ReadCityTableReference(rawLink <= 51 ? rawLink : 0)
                ?? throw Missing("Fire_City_pointer");
        }
    }

    private UnitState Army(int physical)
    {
        var receiver = world.ReadArmyLifecycle()?.ReceiverSlots().ElementAtOrDefault(physical) ?? throw Missing("Army_receiver");
        return receiver.ActiveUnitId is int id ? world.Units.GetValueOrDefault(id) ?? throw Missing("Army_active")
            : receiver.PendingImage ?? throw Missing("Army_current_image");
    }
    private NormalCombatParticipant DescribeArmy(UnitState army)
    {
        int slot = army.NativeArmyOrigin?.NativeSlotIndex ?? throw Missing("Army_physical");
        int force = Need(armies.ReadForce(slot)?.PhysicalSlot, "Army_force");
        byte morale = Need(ArmyMoraleSourceAdapter.Read(army), "Army_morale");
        int maximum = Need(tactics.ForceMoraleMaximum(world, force), "Army_morale_maximum");
        return new(27, slot, army.Id, checked((byte)Math.Clamp(Math.Min(morale, maximum), 0, 255)),
            checked((ushort)army.Troops), army.HexId, force, 0);
    }
    private static NormalCombatParticipant FromBase(NormalBaseCombatSnapshot value) => new(value.NativeKind4,
        value.PhysicalSlot0, null, value.Morale8, value.TroopsA, value.HexC, value.Force10, 0);
    internal static bool Before(NormalCombatMixedPair left, NormalCombatMixedPair right)
    {
        var a = left.Target; var b = right.Target;
        if (a.Morale != b.Morale) return a.Morale < b.Morale;
        if (a.Troops != b.Troops) return a.Troops < b.Troops;
        if (a.Kind != b.Kind) return Priority(a.Kind) < Priority(b.Kind);
        if (a.PhysicalSlot != b.PhysicalSlot) return a.PhysicalSlot < b.PhysicalSlot;
        a = left.Actor; b = right.Actor;
        if (a.Morale != b.Morale) return a.Morale > b.Morale;
        if (a.Troops != b.Troops) return a.Troops > b.Troops;
        return Priority(a.Kind) <= Priority(b.Kind);
    }
    internal static int Priority(int kind) => kind switch { 27 => 0, 5 => 1, 6 => 2, _ => 3 };
    internal static int Distance(int first, int second)
    {
        int x1 = first % 220, y1 = first / 220, x2 = second % 220, y2 = second / 220;
        int dx = Math.Abs(x2 - x1), dy = Math.Abs(2 * (y2 - y1) - (x1 % 2 == 0 ? 1 : 0) + (x2 % 2 == 0 ? 1 : 0));
        return dy < dx ? dx : dx + (dy - dx) / 2;
    }
    internal static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    internal static InvalidDataException Missing(string field) => new("combat.mixed." + field);
}
