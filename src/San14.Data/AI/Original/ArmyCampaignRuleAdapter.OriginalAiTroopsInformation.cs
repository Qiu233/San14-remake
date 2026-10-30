using System.Collections.Immutable;
using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IArmyGroupPurposeExecution
{
    internal TroopsInformationValue ReadOriginalAiTroopsInformationCurrent(GameState state,
        SourceWorldContext current, OriginalAiActor actor, OriginalAiTroopsReference group, int mode)
    {
        if (mode == 0) return new TroopsInformationInteger(group.GroupId);
        if (mode == 1) return new TroopsInformationInteger((int)TroopsInfoPurpose(state, group).Kind);
        if (mode == 2) return new TroopsInformationEntity(TroopsInfoArmyEntity(state,
            TroopsInfoRepresentativeArmy(state, current, group)));
        if (mode == 3) return new TroopsInformationArmies(ReadOriginalAiTroopsMembersCurrent(current, group)
            .Select(army => TroopsInfoArmyEntity(state, army)).ToArray());
        if (mode == 4)
        {
            var purpose = TroopsInfoPurpose(state, group);
            int id = purpose.Target.Id;
            //08ECA0 switches on the current purpose word, including a
            //17/18 target retained by035990. Unknown purpose genuinely
            //leaves the output variable unchanged; it is not a missing owner.
            ArmyGroupPurposeTargetKind? targetKind = (int)purpose.Kind switch
            {
                0 or 2 or 8 or 9 or 10 or 11 or 12 or 17 or 18 or 22 => ArmyGroupPurposeTargetKind.Foothold,
                1 or 19 => ArmyGroupPurposeTargetKind.CautionGroup,
                3 or 13 or 14 or 15 or 16 or 21 or 23 => ArmyGroupPurposeTargetKind.Area,
                4 or 20 => ArmyGroupPurposeTargetKind.MapObject,
                5 or 6 or 7 => ArmyGroupPurposeTargetKind.Hex,
                _ => null
            };
            if (targetKind == ArmyGroupPurposeTargetKind.Foothold)
            {
                if (id is < 1 or > 61) return new TroopsInformationUnchanged();
                var home = BaseLookup.Find(id, current);
                return new TroopsInformationEntity(new(home.Kind == BaseKind.City ? WorldEntityKind.City : WorldEntityKind.Gate,
                    TroopsInfoKnown(current.ReadBaseObjectLink(home), "goal_Foothold_identity")));
            }
            if (targetKind == ArmyGroupPurposeTargetKind.CautionGroup)
            {
                if (id is < 1 or > 500) return new TroopsInformationUnchanged();
                var at = current.ReadStaticTableReference(0x80BA8, id) ?? throw TroopsInfoMissing("goal_Caution_pointer");
                return new TroopsInformationEntity(new(WorldEntityKind.ArmyCautionGroup, TroopsInfoKnown(at.PhysicalSlot, "goal_Caution_identity")));
            }
            if (targetKind == ArmyGroupPurposeTargetKind.Hex)
                return id is >= 0 and < 48400 ? new TroopsInformationHex(TroopsInfoHex(current, id)) : new TroopsInformationUnchanged();
            if (targetKind == ArmyGroupPurposeTargetKind.Area)
            {
                if (id is < 1 or > 500) return new TroopsInformationUnchanged();
                var area = current.ReadAreaTableReference(id)
                    ?? throw TroopsInfoMissing("goal_Area_pointer");
                return new TroopsInformationEntity(new(WorldEntityKind.Area,
                    TroopsInfoKnown(area.PhysicalSlot, "goal_Area_identity")));
            }
            if (targetKind == ArmyGroupPurposeTargetKind.MapObject)
            {
                if (id is < 1 or > 3000) return new TroopsInformationUnchanged();
                var obj = current.ReadObjectTableReference(id) ?? throw TroopsInfoMissing("goal_Object_pointer");
                int physical = TroopsInfoKnown(obj.PhysicalSlot, "goal_Object_identity");
                var directory = current.CurrentMapObjectDirectory ?? throw TroopsInfoMissing("goal_Object_directory");
                return new TroopsInformationEntity(new(WorldEntityKind.MapObject, directory.ReceiverAtPhysical(physical).Value));
            }
            return new TroopsInformationUnchanged();
        }
        if (mode == 5)
        {
            if (actor.AgentKind is OriginalAiAgentKind.Force or OriginalAiAgentKind.District)
                return new TroopsInformationUnchanged(); //0B5F10 is a genuine null for these scopes.
            if (actor.AgentKind != OriginalAiAgentKind.Troops || actor.Troops is not { } own)
                throw TroopsInfoMissing("MyselfTroops_actual_Army_scope");
            if (!ReadOriginalAiTroopsValidityCurrent(current, own)) return new TroopsInformationUnchanged();
            int sum = Sum(own, 0), personality = TroopsInfoActorWord(state, current, actor, 0x16A);
            return new TroopsInformationInteger(unchecked(sum + unchecked(unchecked(sum * Parameter("original-ai.troops-ready-personality-percent")) * personality) / 10000));
        }
        if (mode == 6) throw TroopsInfoMissing("IGameData_current_object_identity");
        if (mode == 7) return new TroopsInformationEntity(new(WorldEntityKind.District,
            ReadOriginalAiTroopsDistrictCurrent(current, group)));
        if (mode is 8 or 9 or 10) return new TroopsInformationInteger(Sum(group, mode == 8 ? 1 : mode == 9 ? 2 : 0));
        if (mode is 11 or 12)
        {
            int total = Sum(group, mode == 11 ? 1 : 2);
            int personality = TroopsInfoActorWord(state, current, actor, mode == 11 ? 0x170 : 0x172);
            int deduction = unchecked(unchecked(total * personality) * Parameter(mode == 11
                ? "original-ai.troops-gold-preservation-percent" : "original-ai.troops-supplies-preservation-percent"));
            return new TroopsInformationInteger(unchecked(total - deduction / 10000));
        }
        if (mode == 13)
        {
            float total = 0f;
            foreach (var army in ReadOriginalAiTroopsMembersCurrent(current, group))
                total += (float)TroopsInfoKnown(current.ReadArmyReturnResourceFields(army)?.Ready16, "member_troops") *
                    ReadOriginalAiArmyCommonCurrent(state, current, army) / (float)Parameter(OriginalAiCrisisSource.StrengthDivisorKey);
            return new TroopsInformationInteger(BattleUnitSourceMath.TruncateSingleToInt32(total));
        }
        if (mode == 14)
        {
            var plan = TroopsInfoObjective(state, group).RelayHexIds ?? throw TroopsInfoMissing("relay_Hex_plan");
            return new TroopsInformationHexes(plan.Select(id => TroopsInfoHex(current, id)).ToArray());
        }
        if (mode == 15) return new TroopsInformationEntity(new(WorldEntityKind.Force, ReadOriginalAiTroopsForceCurrent(current, group)));
        throw TroopsInfoMissing("selector_" + mode);

        int Parameter(string key) => state.NativeArmySimulation?.Parameters.Int32Values.TryGetValue(key, out int value) == true
            ? value : throw TroopsInfoMissing("parameter." + key);
        int Sum(OriginalAiTroopsReference selected, int resource)
        {
            int total = 0;
            foreach (var army in ReadOriginalAiTroopsMembersCurrent(current, selected))
            {
                var fields = current.ReadArmyReturnResourceFields(army) ?? throw TroopsInfoMissing("member_resources");
                total = unchecked(total + TroopsInfoKnown(resource == 0 ? fields.Ready16
                    : resource == 1 ? fields.Gold5C : fields.Supplies60, "member_resource_" + resource));
            }
            return total;
        }
    }

    internal OriginalAiCommandPreparation PrepareOriginalAiTroopsPurposeCurrent(GameState state, SourceWorldContext current,
        OriginalAiActor actor, OriginalAiTroopsReference group, int selector, OriginalAiEntity? entity, OriginalAiHex? hex)
    {
        if (!ReadOriginalAiTroopsValidityCurrent(current, group) || TroopsInfoExpeditionMember(state, current, group))
            return new(false, null);
        ArmyGroupPurposeKind kind = selector switch
        {
            0 => ArmyGroupPurposeKind.Withdraw, 1 or 19 => ArmyGroupPurposeKind.InterceptCautionGroup,
            7 => ArmyGroupPurposeKind.HoldAtHex, 12 => ArmyGroupPurposeKind.TransportToFoothold,
            13 => ArmyGroupPurposeKind.CaptureArea,
            17 => ArmyGroupPurposeKind.EnterCity, 18 => ArmyGroupPurposeKind.EnterGate,
            _ => throw TroopsInfoMissing("purpose_semantic_variant_" + selector)
        };
        ArmyGroupPurposeTarget target;
        if (kind == ArmyGroupPurposeKind.HoldAtHex)
        {
            if (hex is not { } at) throw TroopsInfoMissing("purpose_Hex_object");
            target = new(ArmyGroupPurposeTargetKind.Hex, TroopsInfoHex(current, at.Id).Id);
        }
        else if (kind == ArmyGroupPurposeKind.CaptureArea)
        {
            if (entity is not { Kind: WorldEntityKind.Area }) throw TroopsInfoMissing("purpose_Area_object");
            var area = current.ReadAreaTableReference(entity.Value.Id is >= 0 and <= 500 ? entity.Value.Id : 0)
                ?? throw TroopsInfoMissing("purpose_Area_pointer");
            target = new(ArmyGroupPurposeTargetKind.Area, TroopsInfoKnown(area.PhysicalSlot, "purpose_Area_identity"));
        }
        else if (kind == ArmyGroupPurposeKind.InterceptCautionGroup)
        {
            if (entity is { Kind: not WorldEntityKind.ArmyCautionGroup }) throw TroopsInfoMissing("purpose_Caution_object_type");
            int id = entity?.Id ?? 0;
            var at = current.ReadStaticTableReference(0x80BA8, id is >= 0 and <= 500 ? id : 0)
                ?? throw TroopsInfoMissing("purpose_Caution_pointer");
            int physical = TroopsInfoKnown(at.PhysicalSlot, "purpose_Caution_identity");
            bool valid = TroopsInfoKnown(current.ReadRegistryObjectNativeValidity(new(RegistryKind.CautionTroops, physical)), "purpose_Caution_validity");
            target = new(ArmyGroupPurposeTargetKind.CautionGroup, valid ? physical : 0);
        }
        else
        {
            if (entity is { Kind: not (WorldEntityKind.City or WorldEntityKind.Gate) })
                throw TroopsInfoMissing("purpose_Foothold_object_type");
            if (kind == ArmyGroupPurposeKind.TransportToFoothold && entity is null)
                throw TroopsInfoMissing("purpose_transport_Foothold_object");
            var home = BaseLookup.Find(entity?.Id ?? 0, current);
            bool validate = kind is ArmyGroupPurposeKind.Withdraw or ArmyGroupPurposeKind.EnterCity or ArmyGroupPurposeKind.EnterGate;
            int location = validate && !TroopsInfoKnown(current.ReadBaseNativeValidity(home), "purpose_Foothold_validity")
                ? 0 : TroopsInfoKnown(current.ReadBaseObjectLink(home), "purpose_Foothold_identity");
            target = new(ArmyGroupPurposeTargetKind.Foothold, location);
        }
        return new(true, new SetArmyGroupPurposeOrder(actor.ForceId, group.GroupId, new(kind, target)));
    }

    CommandResult IArmyGroupPurposeExecution.SetArmyGroupPurpose(GameState state, ForceState force,
        SetArmyGroupPurposeOrder order, CommandContext context)
    {
        try
        {
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            var group = new OriginalAiTroopsReference(order.GroupId);
            if (!TroopsInfoAuthorized(state, current, force, context, group) || TroopsInfoExpeditionMember(state, current, group))
                return CommandResult.Reject("Military group is outside current command scope.", "group.purpose_scope");
            GameState result = TroopsInfoWriteObjective(state, group, purpose: order.Purpose);
            if (order.Purpose.Kind is ArmyGroupPurposeKind.Withdraw or ArmyGroupPurposeKind.EnterCity or ArmyGroupPurposeKind.EnterGate)
                foreach (var member in ReadOriginalAiTroopsMembersCurrent(current, group))
                    result = TroopsInfoResetMember(result, current, member);
            return CommandResult.Accept(result);
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, "Military group purpose inputs are unavailable."); }
    }

    CommandResult IArmyGroupPurposeExecution.SetArmyGroupRelayHexes(GameState state, ForceState force,
        SetArmyGroupRelayHexesOrder order, CommandContext context)
    {
        try
        {
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            var group = new OriginalAiTroopsReference(order.GroupId);
            if (!TroopsInfoAuthorized(state, current, force, context, group))
                return CommandResult.Reject("Military group is outside current command scope.", "group.relay_scope");
            var points = order.HexIds.Select(id => TroopsInfoHex(current, id).Id).ToImmutableArray();
            return CommandResult.Accept(TroopsInfoWriteObjective(state, group, relay: points));
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, "Military group relay inputs are unavailable."); }
    }

    /// <summary>Only the actual successful transport creation tail calls this
    /// writer. Its source admits the created Army's group-zero placeholder.</summary>
    internal GameState ApplyOriginalAiTransportGroupPurposeCurrent(GameState state, SourceWorldContext current,
        ArmyUnitReference createdArmy, int destinationLocationId)
    {
        if (!TroopsInfoKnown(current.ReadArmyUnitNativeValidity(createdArmy), "created_transport_validity")) return state;
        if ((uint)destinationLocationId > 61) throw TroopsInfoMissing("transport_destination_Foothold");
        return TroopsInfoWriteObjective(state, current.ReadOriginalAiArmyTroopsCurrent(createdArmy),
            new(ArmyGroupPurposeKind.TransportToFoothold, new(ArmyGroupPurposeTargetKind.Foothold, destinationLocationId)));
    }

    private static GameState TroopsInfoWriteObjective(GameState state, OriginalAiTroopsReference group,
        ArmyGroupPurpose? purpose = null, ImmutableArray<int>? relay = null)
    {
        var lifecycle = state.ReadArmyLifecycle() ?? throw TroopsInfoMissing("group_lifecycle");
        var groups = lifecycle.ArmyGroups ?? throw TroopsInfoMissing("group_owner");
        var rows = groups.Objectives ?? ImmutableDictionary<int, ArmyGroupObjectiveState>.Empty;
        var before = rows.GetValueOrDefault(group.GroupId);
        var changed = new ArmyGroupObjectiveState(purpose ?? before?.Purpose, relay ?? before?.RelayHexIds);
        return state.WithArmyLifecycle(lifecycle with { ArmyGroups = groups with { Objectives = rows.SetItem(group.GroupId, changed) } });
    }

    private GameState TroopsInfoResetMember(GameState state, SourceWorldContext current, ArmyUnitReference member)
    {
        int id = TroopsInfoUnitId(state, member), physical = TroopsInfoKnown(member.PhysicalSlot, "member_identity");
        var unit = state.Units.GetValueOrDefault(id) ?? throw TroopsInfoMissing("member_current_owner");
        var runtime = unit.NativeTacticRuntime ?? throw TroopsInfoMissing("member_runtime");
        if (runtime.Abnormal25 == 0) state = ClearTrackedArmyOverride(state, current, unit);
        unit = state.Units[id];
        if (TroopsInfoKnown(ArmySavedCommandKnowledge.ReadControl(state, unit), "member_behavior_lock") != 0) return state;
        var behavior = unit.Behavior ?? throw TroopsInfoMissing("member_behavior");
        behavior = behavior with { AttackNearbyEnemies = false, PursueRetreatingEnemies = false };
        int raw = TroopsInfoKnown(current.ReadArmyUnitCommanderId(new(physical)), "member_Commander");
        int person = TroopsInfoPerson(current, raw);
        if (TroopsInfoKnown(current.ReadPersonNativeValidity(person), "member_Commander_validity"))
            behavior = ReadCurrentDeploymentPolicyTraits(current, person).ApplyDefaults(behavior);
        return state with { Units = new(state.Units) { [id] = unit with { Behavior = behavior } } };
    }

    private bool TroopsInfoAuthorized(GameState state, SourceWorldContext current, ForceState force,
        CommandContext context, OriginalAiTroopsReference group)
    {
        if (!ReadOriginalAiTroopsValidityCurrent(current, group) || ReadOriginalAiTroopsForceCurrent(current, group) != force.Id) return false;
        int district = ReadOriginalAiTroopsDistrictCurrent(current, group);
        return state.Districts.TryGetValue(district, out var owner) && owner.ForceId == force.Id &&
            (context.DelegatedDistrictId is int delegated ? delegated == district : owner.Number == 1);
    }

    private bool TroopsInfoExpeditionMember(GameState state, SourceWorldContext current, OriginalAiTroopsReference group)
    {
        foreach (var member in ReadOriginalAiTroopsMembersCurrent(current, group))
        {
            int id = TroopsInfoUnitId(state, member), physical = TroopsInfoKnown(member.PhysicalSlot, "member_identity");
            byte? value = state.Units.GetValueOrDefault(id)?.NativeMapUiByte13C ?? state.ReadArmyLifecycle()?.Slots[physical].MapUiByte13C;
            if (TroopsInfoKnown(value, "member_expedition_membership") != 0) return true;
        }
        return false;
    }

    private ArmyUnitReference TroopsInfoRepresentativeArmy(GameState state, SourceWorldContext current, OriginalAiTroopsReference group)
    {
        var groups = state.ReadArmyLifecycle()?.ArmyGroups ?? throw TroopsInfoMissing("group_owner");
        var leader = groups.GetLeader(group.GroupId);
        if (!leader.IsKnown) throw TroopsInfoMissing("group_leader");
        ArmyUnitReference at = current.ReadArmyUnitTableReference(0) ?? throw TroopsInfoMissing("Army_placeholder");
        if (leader.Officer is { Kind: ArmyGroupOfficerKind.Officer, OfficerId: int personId })
        {
            int person = TroopsInfoPerson(current, personId);
            var position = PersonPositionRepair.DecodeSecondLocation(TroopsInfoKnown(current.ReadPersonSecondLocation(person), "leader_position"));
            if (position.Kind == 27)
            {
                var candidate = current.ReadArmyUnitTableReference(position.Index <= 500 ? position.Index : 0)
                    ?? throw TroopsInfoMissing("leader_Army_pointer");
                if (TroopsInfoKnown(current.ReadArmyUnitCommanderId(candidate), "leader_Army_Commander") ==
                    TroopsInfoKnown(current.ReadPersonIdentity(person), "leader_identity")) at = candidate;
            }
        }
        if (TroopsInfoKnown(current.ReadArmyUnitNativeValidity(at), "leader_Army_validity")) return at;
        return ReadOriginalAiTroopsMembersCurrent(current, group).OrderBy(army =>
            TroopsInfoKnown(army.PhysicalSlot, "member_sort_identity")).FirstOrDefault() ?? at;
    }

    private int TroopsInfoActorWord(GameState state, SourceWorldContext current, OriginalAiActor actor, int offset)
    {
        int? selected = null;
        if (actor.AgentKind == OriginalAiAgentKind.Force)
        {
            if (!ReadOriginalAiForceValidity(current, actor.ForceId)) return 0;
            selected = TroopsInfoPerson(current, current.ReadForceRecord(actor.ForceId)?.RawRuler10 ?? throw TroopsInfoMissing("actor_ruler"));
        }
        else if (actor.AgentKind == OriginalAiAgentKind.District)
        {
            int district = actor.DistrictId ?? throw TroopsInfoMissing("actor_District");
            if (!ReadOriginalAiDistrictValidity(current, district)) return 0;
            selected = TroopsInfoPerson(current, TroopsInfoKnown(current.ReadDistrictLeader(district), "actor_District_leader"));
        }
        else if (actor.AgentKind == OriginalAiAgentKind.Troops && actor.Troops is { } own)
        {
            if (!ReadOriginalAiTroopsValidityCurrent(current, own)) return 0;
            int maximum = int.MinValue; bool bound = false;
            foreach (var member in ReadOriginalAiTroopsMembersCurrent(current, own))
            {
                int person = TroopsInfoPerson(current, TroopsInfoKnown(current.ReadArmyUnitCommanderId(member), "actor_member_Commander"));
                int strength = 0;
                if (TroopsInfoKnown(current.ReadPersonNativeValidity(person), "actor_Commander_validity"))
                {
                    if (!bound) { _baseCapacityInputs.BindCurrent(state, current); bound = true; }
                    strength = ReadOriginalAiCityDutyAbility(state, current, TroopsInfoKnown(current.ReadPersonIdentity(person), "actor_Commander_identity"), 0);
                }
                if (strength <= maximum) continue;
                maximum = strength; selected = person;
            }
        }
        else if ((int?)actor.AgentKind == 1) return 0; //The source has no Agent1 personality branch.
        else throw TroopsInfoMissing("actor_scope");
        return selected is int physical ? TroopsInfoKnown(RulerCaptureReleaseInputs.ReadCurrentWord(state, physical, offset), "actor_personality_" + offset.ToString("X")) : 0;
    }

    private static ArmyGroupObjectiveState TroopsInfoObjective(GameState state, OriginalAiTroopsReference group) =>
        state.ReadArmyLifecycle()?.ArmyGroups?.Objectives?.GetValueOrDefault(group.GroupId) ?? throw TroopsInfoMissing("current_objective");
    private static ArmyGroupPurpose TroopsInfoPurpose(GameState state, OriginalAiTroopsReference group) =>
        TroopsInfoObjective(state, group).Purpose ?? throw TroopsInfoMissing("current_purpose");
    private static OriginalAiHex TroopsInfoHex(SourceWorldContext current, int id) =>
        new(TroopsInfoKnown((current.ReadHexTableReference(id is >= 0 and < 48400 ? id : 0)
            ?? throw TroopsInfoMissing("Hex_pointer")).PhysicalSlot, "Hex_identity"));
    private static int TroopsInfoPerson(SourceWorldContext current, int raw) => TroopsInfoKnown(
        (raw is >= 0 and <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_pointer");
    private static int TroopsInfoUnitId(GameState state, ArmyUnitReference army)
    {
        int physical = TroopsInfoKnown(army.PhysicalSlot, "Army_identity");
        return ArmyDestructionQueue.ResolveUnitId(state, physical) ??
            throw TroopsInfoMissing(physical == 0 ? "Army_placeholder_object" : "Army_domain_identity");
    }
    private static OriginalAiEntity TroopsInfoArmyEntity(GameState state, ArmyUnitReference army) => new(WorldEntityKind.Army, TroopsInfoUnitId(state, army));
    private static T TroopsInfoKnown<T>(T? value, string field) where T : struct => value ?? throw TroopsInfoMissing(field);
    private static InvalidDataException TroopsInfoMissing(string field) => new("original_ai.troops_information." + field);
}
