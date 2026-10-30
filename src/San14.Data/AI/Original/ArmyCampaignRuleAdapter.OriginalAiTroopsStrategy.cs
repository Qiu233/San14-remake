using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The actual B5720 context-present prefix clears five relay
    /// operands for each current member. They are not UI target previews.</summary>
    internal GameState PrepareOriginalAiTroopsStrategyCurrent(GameState state,
        SourceWorldContext current, OriginalAiTroopsReference group)
    {
        foreach (var member in ReadOriginalAiTroopsMembersCurrent(current, group))
            state = WriteAiGroupRelay(state, current, AiGroupUnit(state, member), []);
        return state;
    }

    internal GameState CompleteOriginalAiTroopsStrategyCurrent(GameState state,
        SourceWorldContext current, OriginalAiTroopsReference group)
    {
        var before = state;
        state = CompleteAiGroupSingletonPursuit(state, current, group);
        if (!ReferenceEquals(before, state)) current = CaptureOriginalAiDomesticCurrent(state);
        return CompleteAiGroupAreaDetour(state, current, group);
    }

    private GameState CompleteAiGroupSingletonPursuit(GameState state, SourceWorldContext current,
        OriginalAiTroopsReference group)
    {
        var members = ReadOriginalAiTroopsMembersCurrent(current, group);
        if (members.Count != 1) return state;
        var army = AiGroupUnit(state, members[0]);
        byte flags = army.NativeTacticRuntime?.Flags26 ?? throw AiGroupMissing("singleton_Army26");
        if ((flags & 2) == 0 || (flags & 1) != 0) return state;
        var profile = BeginOriginalAiPursuitProfileCurrent(state);
        int sourceForce = AiGroupArmyForce(current, members[0]);
        foreach (var at in AiGroupNearby(current, members[0], 2))
            if (!AiGroupHostile(state, sourceForce, AiGroupArmyForce(current, at))) return profile.Complete(state, false).State;
        float own = ReadAiGroupFieldAttack(state, current, army);
        if (own <= 500f) return profile.Complete(state, false).State;
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw AiGroupMissing("current_player_Force");
        int playerDistrict = AiGroupKnown(ForcePrimaryDistrictSource.Resolve(current, player).PhysicalSlot, "player_primary_District");
        foreach (var target in AiGroupNearby(current, members[0], 8))
        {
            if (AiGroupArmyDistrict(current, target) != playerDistrict) continue;
            if (!AiGroupHostile(state, sourceForce, AiGroupArmyForce(current, target))) continue;
            int firstHex = AiGroupKnown(current.ReadArmyUnitRawHex2A(members[0]), "singleton_Hex");
            int secondHex = AiGroupKnown(current.ReadArmyUnitRawHex2A(target), "target_Hex");
            if (ArmyRouteSearch.Distance(firstHex, secondHex) <= 3) continue;
            if (ReadAiGroupMobility(state, current, army) <= 12f) continue;
            var targetArmy = AiGroupUnit(state, target);
            if (ReadAiGroupFieldAttack(state, current, targetArmy) * 1.5f > own) continue;
            // This source call is a real reachable-pursuit service, not an
            // unconditional permission bit. Its separate complete service must
            // be connected before this reached branch can publish a target.
            if (!ReadOriginalAiGroupPursuitAdmissionCurrent(state, current, targetArmy, army, profile)) continue;
            var changed = ((IArmyInstructionExecution)this).SetAction(state, state.Forces[army.ForceId],
                new(army.ForceId, army.Id, 7), new("original-ai:group-pursuit"));
            if (!changed.Accepted) throw AiGroupMissing(changed.MissingRuleKey ?? changed.Error ?? "pursuit_action");
            state = changed.State!;
            var selected = ((IArmyInstructionExecution)this).SetTarget(state, state.Forces[army.ForceId],
                new(army.ForceId, army.Id, new(ArmyInstructionTargetKind.Army, targetArmy.Id)), new("original-ai:group-pursuit"));
            if (!selected.Accepted) throw AiGroupMissing(selected.MissingRuleKey ?? selected.Error ?? "pursuit_target");
            return profile.Complete(selected.State!, true).State;
        }
        return profile.Complete(state, false).State;
    }

    /// <summary>AFBF0: preserve the actual word16 operand, its reached Hex
    /// lookup, Area73 gate, path order and ring exclusion. Do not replace the
    /// source operand with an Area's representative map position.</summary>
    private GameState CompleteAiGroupAreaDetour(GameState state, SourceWorldContext current,
        OriginalAiTroopsReference group)
    {
        var objective = state.ReadArmyLifecycle()?.ArmyGroups?.Objectives?.GetValueOrDefault(group.GroupId)
            ?? throw AiGroupMissing("objective_owner");
        var purpose = objective.Purpose ?? throw AiGroupMissing("purpose_owner");
        if (purpose.Kind != ArmyGroupPurposeKind.CaptureArea) return state;
        if (purpose.Target.Kind != ArmyGroupPurposeTargetKind.Area) throw AiGroupMissing("capture_Area_target_type");
        var reachedHex = current.ReadHexTableReference(purpose.Target.Id < 48400 ? purpose.Target.Id : 0)
            ?? throw AiGroupMissing("capture_target_Hex_pointer");
        var targetArea = AiGroupArea(current, reachedHex);
        if (!AiGroupKnown(current.ReadAreaNativeValidity(targetArea), "capture_target_Area_validity")) return state;
        if (AiGroupKnown(current.ReadAreaOccupationClaimForceSlot(targetArea), "capture_target_Area73") !=
            ReadOriginalAiTroopsForceCurrent(current, group)) return state;
        var members = ReadOriginalAiTroopsMembersCurrent(current, group);
        foreach (var member in members)
        {
            var unit = AiGroupUnit(state, member);
            int start = AiGroupKnown(current.ReadArmyUnitRawHex2A(member), "detour_current_Hex");
            int goal = ReadOriginalAiArmyStrategyGoalCurrent(state, current, unit);
            var target = current.ReadArmyLifecycleTargetFields(AiGroupKnown(member.PhysicalSlot, "detour_Army_slot"))
                ?? throw AiGroupMissing("detour_target_fields");
            byte kind = AiGroupKnown(target.Kind38, "detour_target_kind");
            ushort identity = AiGroupKnown(target.Identity3A, "detour_target_identity");
            var path = ArmyRouteSearch.Search(state, current, unit, start, goal, kind, identity,
                member.PhysicalSlot, out _);
            if (path.Length == 0) continue;
            int boundary = -1, previous = -1, next = -1;
            int force = ReadOriginalAiTroopsForceCurrent(current, group);
            for (int index = 0; index < path.Length; index++)
            {
                var hex = current.ReadHexTableReference(path[index]) ?? throw AiGroupMissing("detour_path_Hex");
                var area = AiGroupArea(current, hex);
                if (AiGroupKnown(current.ReadAreaCurrentForceSlot(area), "detour_path_Area_Force") == force || area == targetArea) continue;
                if (AiGroupKnown(current.ReadAreaHexIndex(area), "detour_path_Area_center") != path[index]) continue;
                boundary = path[index]; previous = index == 0 ? start : path[index - 1];
                next = index + 1 < path.Length ? path[index + 1] : -1;
                break;
            }
            if (boundary < 0 || previous < 0 || next < 0) continue;
            int formationClass = ReadOriginalAiArmyFormationClassCurrent(state, current, unit);
            var excluded = new HashSet<int>();
            if (formationClass is 0 or 1)
            {
                var center = current.ReadHexTableReference(boundary) ?? throw AiGroupMissing("detour_boundary_Hex");
                var temporary = current.CreateNearbyTemporary();
                try
                {
                    NearbyArmyTemporarySource.FillNearbyHexTemporary(current, center, formationClass == 0 ? 1 : 2, temporary);
                    var node = current.ReadHexListHead(temporary) ?? throw AiGroupMissing("detour_ring_head");
                    while (node.Identity.HasValue)
                    {
                        var hex = current.ReadHexListNodePayload(node) ?? throw AiGroupMissing("detour_ring_payload");
                        excluded.Add(AiGroupKnown(current.ReadHexPhysicalSlot(hex), "detour_ring_identity"));
                        node = current.ReadHexListNodeNext(node) ?? throw AiGroupMissing("detour_ring_next");
                    }
                }
                finally { current.ClearNearbyTemporary(temporary, true); }
            }
            else excluded.Add(boundary);
            excluded.Remove(start);
            var replacement = ArmyRouteSearch.Search(state, current, unit, previous, next, kind, identity,
                member.PhysicalSlot, out _, excludedHexIds: excluded);
            if (replacement.Length == 0) continue;
            state = WriteAiGroupRelay(state, current, unit, replacement.Take(5).ToArray());
        }
        return state;
    }

    private GameState WriteAiGroupRelay(GameState state, SourceWorldContext current, UnitState unit, IReadOnlyList<int> path)
    {
        ushort[] values = Enumerable.Repeat((ushort)48400, 5).ToArray();
        // Five explicit source clears precede every replacement, including
        // under control140. A controlled Army therefore retains the clears.
        bool controlled = AiGroupKnown(ArmySavedCommandKnowledge.ReadControl(state, unit), "relay_control140") != 0;
        if (!controlled)
            for (int index = 0; index < Math.Min(path.Count, values.Length); index++)
                if (IsOriginalAiArmyRelayHexIdentityCurrent(current, path[index])) values[index] = (ushort)path[index];
        byte status = unit.NativeTacticRuntime?.Status10 ?? throw AiGroupMissing("relay_Army_status");
        if (status == 2)
        {
            var prior = TransportRouteSourceAdapter.ReadWaypoints(state, unit);
            if (prior is not null && prior.SequenceEqual(values)) return state;
            var resource = state.ResourceState;
            if (TransportRouteSourceAdapter.ReadChoice(state, unit) is byte choice)
                resource = TransportRouteSourceAdapter.Write(resource, unit.Id, values, choice);
            return state with { ResourceState = resource, Units = new(state.Units)
                { [unit.Id] = unit with { TransportRoute = TransportRouteSourceAdapter.Decode(values) } } };
        }
        var before = ArmyMarchSourceAdapter.ReadOrdinaryWaypoints(unit.March);
        if (before is not null && before.SequenceEqual(values)) return state;
        var march = unit.March ?? new ArmyMarchState(null, null, null);
        march = march with { Target = ArmyMarchSourceAdapter.DecodeDestination(values[0]),
            FollowingWaypoints = new(values.Skip(1).Select(raw => (ArmyMarchDestination?)ArmyMarchSourceAdapter.DecodeDestination(raw)).ToArray()) };
        return state with { Units = new(state.Units) { [unit.Id] = unit with { March = march } } };
    }

    private float ReadAiGroupFieldAttack(GameState state, SourceWorldContext current, UnitState unit)
    {
        _baseCapacityInputs.BindCurrent(state, current); _personRelationInputs.BindCurrent(state, current);
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        var rules = new TacticRemakeCurrentContext(current, _catalog, _map, _terrain, _usage, Revision, abilities, new AiCrisisTacticQueries(this));
        float? actual = null;
        var result = ReadUnitSourceAxesCurrent(state, unit.Id, null, current, rules, abilities, null,
            conditionalModifiers: false, commonModifiers: false, requestedStat: BattleUnitArmyStatKind.FieldAttack,
            rawStatObserver: (kind, value) => actual = value);
        return actual ?? throw new InvalidDataException(result.Missing.GetValueOrDefault("without.field")
            ?? result.Missing.GetValueOrDefault("without.common") ?? AiGroupMissing("raw_field_attack").Message);
    }

    private float ReadAiGroupMobility(GameState state, SourceWorldContext current, UnitState unit)
    {
        int physical = unit.NativeArmyOrigin?.NativeSlotIndex ?? throw AiGroupMissing("mobility_Army_slot");
        int rawHex = AiGroupKnown(current.ReadArmyUnitRawHex2A(new(physical)), "mobility_Hex");
        var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0) ?? throw AiGroupMissing("mobility_Hex_pointer");
        byte surface = AiGroupKnown(current.ReadHexSurfaceCode(hex), "mobility_surface");
        int formation = AiGroupKnown((_terrain.Catalog.Types[surface <= 30 ? surface : 0].RawField2C & 2) != 0
            ? ArmyConfigurationSourceAdapter.ReadNaval(state, unit) : ArmyConfigurationSourceAdapter.ReadCurrentLand(state, unit), "mobility_formation");
        var result = ArmyRouteMobilitySource.Read(state, current, unit, formation, surface);
        return result.Value ?? throw AiGroupMissing(result.UnresolvedOwner ?? "mobility");
    }

    private static UnitState AiGroupUnit(GameState state, ArmyUnitReference reference) =>
        state.Units.GetValueOrDefault(ArmyDestructionQueue.ResolveUnitId(state, reference.PhysicalSlot)
            ?? throw AiGroupMissing("member_domain_identity")) ?? throw AiGroupMissing("member_owner");
    private static int AiGroupArmyForce(SourceWorldContext current, ArmyUnitReference army) =>
        AiGroupKnown(current.ReadArmyUnitForceReference(army)?.PhysicalSlot, "Army_Force");
    private static int AiGroupArmyDistrict(SourceWorldContext current, ArmyUnitReference army)
    {
        int commander = AiGroupKnown(current.ReadArmyUnitCommanderId(army), "Army_Commander");
        int person = AiGroupKnown(current.ReadPersonLookupReference(commander <= 6000 ? commander : 0)?.PhysicalSlot, "Commander_pointer");
        int raw = AiGroupKnown(current.ReadPersonNativeValidity(person), "Commander_validity")
            ? AiGroupKnown(current.ReadPersonDistrictSlot(person), "Commander_District") : 0;
        return AiGroupKnown(current.ReadDistrictTableReference(raw <= 51 ? raw : 0)?.PhysicalSlot, "District_pointer");
    }
    private static bool AiGroupHostile(GameState state, int first, int second) =>
        AiGroupKnown(ArmyLogisticsPreparation.ReadNativeRelationGate(state, first, second), "Force_hostility");
    private static AreaReference AiGroupArea(SourceWorldContext current, HexReference hex)
    {
        int raw = AiGroupKnown(current.ReadHexAreaSlot(hex), "Hex_Area");
        return current.ReadAreaTableReference(raw <= 500 ? raw : 0) ?? throw AiGroupMissing("Area_pointer");
    }
    private static IReadOnlyList<ArmyUnitReference> AiGroupNearby(SourceWorldContext current, ArmyUnitReference army, int radius)
    {
        var result = new List<ArmyUnitReference>(); var temporary = current.CreateNearbyTemporary();
        try
        {
            NearbyArmyTemporarySource.FillNearbyArmyTemporary(current, army, radius, temporary);
            var node = current.ReadRegistryHead(new(temporary.Identity)) ?? throw AiGroupMissing("nearby_head");
            while (node.Identity.HasValue)
            {
                var payload = current.ReadRegistryNodePayload(node) ?? throw AiGroupMissing("nearby_payload");
                if (payload.Kind != RegistryKind.Army || payload.PhysicalSlot is not int physical) throw AiGroupMissing("nearby_Army_identity");
                result.Add(new(physical));
                node = current.ReadRegistryNodeNext(node) ?? throw AiGroupMissing("nearby_next");
            }
        }
        finally { current.ClearNearbyTemporary(temporary, true); }
        return result;
    }
    private static T AiGroupKnown<T>(T? value, string field) where T : struct => value ?? throw AiGroupMissing(field);
    private static InvalidDataException AiGroupMissing(string field) => new("original_ai.group_strategy." + field);
}
