using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState ApplyNormalTurnPostEconomyCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, int action)
    {
        if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw PostEconomyMissing("shared_pool");
        return action switch
        {
            42 => AddNormalTurnMissionMeritCurrent(state, current),
            43 => ConsumeNormalTurnSettlementsCurrent(state, current, pool),
            44 => RemoveNormalTurnHostileTrapsCurrent(state, current, pool),
            45 => RestoreNormalTurnSavedCommandsCurrent(state, current),
            46 => MaintainNormalTurnArmyCommandsCurrent(state, current),
            47 => state, //4F8560 returns literal true, with no writer.
            48 => throw PostEconomyMissing("event_playback_continuation_required"),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
    }

    private GameState AddNormalTurnMissionMeritCurrent(GameState state, SourceWorldContext current)
    {
        var working = state;
        var tasks = current.ReadOwnedPersonCommandManager() ?? throw PostEconomyMissing("manager18_owner");
        foreach (var entry in current.ReadCurrentPersonLinkedEntities() ?? throw PostEconomyMissing("manager18"))
        {
            var task = entry ?? throw PostEconomyMissing("manager18_pointer");
            if (!PostEconomyNeed(current.ReadLinkedEntityNativeValidity(task), "command_validity")) continue;
            int id = PostEconomyNeed(task.Identity, "command_identity");
            int kind = tasks.Returns.TryGetValue(id, out var returning) ? returning.IsReassignment ? 1 : 15 :
                tasks.Disappearances?.ContainsKey(id) == true ? 16 :
                tasks.Talents?.GetValueOrDefault(id) is { } talent ? talent.IsRecruitWait ? 22 : talent.Kind == TalentOrderKind.Search ? 3 : 2 :
                tasks.Diplomacies?.GetValueOrDefault(id) is { } diplomacy ? diplomacy.NativeKind :
                tasks.Strategies?.GetValueOrDefault(id) is { } strategic ? strategic.Kind :
                tasks.Independences?.ContainsKey(id) == true ? 23 : throw PostEconomyMissing("command_virtual98");
            if (kind != 25) continue;
            foreach (var raw in current.ReadLinkedEntityMemberIds(task) ?? throw PostEconomyMissing("command_members"))
            {
                uint member = PostEconomyNeed(raw, "command_member");
                var person = (member <= 6000 ? current.ReadPersonLookupReference((int)member) : current.ReadTransferPersonTableReference(0))
                    ?? throw PostEconomyMissing("command_member_pointer");
                int physical = PostEconomyNeed(person.PhysicalSlot, "command_member_physical");
                if (!PostEconomyNeed(current.ReadPersonNativeValidity(physical), "command_member_validity")) continue;
                int personId = PostEconomyNeed(current.ReadPersonIdentity(physical), "command_member_id");
                //TC1A4AFA0=40, tenfold merit, source reason43. The shared
                //1695C0 operation owns all trait/doctrine/stage corrections.
                var applied = _pendingMerit.TryApplyWithinArmyOperation(working, current, personId, 400);
                if (applied.Outcome == PersonPendingMeritOutcome.CurrentInputsUnresolved)
                    throw PostEconomyMissing("command_member_pending_merit");
                working = applied.State;
            }
        }
        return working;
    }

    private GameState ConsumeNormalTurnSettlementsCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool)
    {
        var consumed = new OwnedArmyDestructionStage(this).TryConsumeDeferredDefeatsCurrent(state, current, pool,
            null, state.NativeMapSeason?.ThirdDate ?? throw PostEconomyMissing("settlement_date"));
        var working = consumed.State ?? throw PostEconomyMissing(consumed.UnresolvedOwner ?? "settlement");
        return working with { PendingCommandReports = [.. working.PendingCommandReports, .. consumed.Reports] };
    }

    private GameState RemoveNormalTurnHostileTrapsCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool)
    {
        var effects = new List<CampaignEffectRequest>();
        var working = state;
        foreach (var entry in current.ReadRegisteredObjectReferences() ?? throw PostEconomyMissing("ObjectE0"))
        {
            var obj = entry ?? throw PostEconomyMissing("ObjectE0_pointer");
            if (ConstructionObjectIsRetired(current, obj)) continue;
            int physical = PostEconomyNeed(obj.PhysicalSlot, "Object_physical");
            int kind = (current.ReadPhysicalObject(physical) ?? throw PostEconomyMissing("Object_record")).RawField12;
            var definition = current.ReadObjectDefinitionReference(kind <= 30 ? kind : 0)
                ?? throw PostEconomyMissing("Object_definition_pointer");
            if (PostEconomyNeed(current.ReadObjectDefinitionRaw2C(definition), "Object_definition2C") != 4) continue;
            int raw = PostEconomyNeed(current.ReadObjectHexIndex(obj), "Object_Hex10");
            var hex = current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw PostEconomyMissing("Object_Hex_pointer");
            if (PostEconomyNeed(current.ReadObjectForceSlot13(obj), "Object_Force13") !=
                PostEconomyNeed(current.ReadHexForceSlot(hex), "Object_Hex_Force14"))
                working = RemoveMapObjectWithoutActor(working, current, obj, effects);
        }
        working = RetireDiplomacyAreaObjects(working, current, pool, effects);
        return AppendNormalTurnMaintenanceEffects(working, effects, CampaignEffectSource.MapObjectRetirement);
    }

    private GameState RestoreNormalTurnSavedCommandsCurrent(GameState state, SourceWorldContext current)
    {
        current.BindOwnedDestructionQueue(state.PendingCombatSettlements, state);
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
        var working = state;
        var effects = new List<CampaignEffectRequest>();
        var operation = FacilityOperation.Playable(state);
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Army) ?? throw PostEconomyMissing("restoration_Army60"))
        {
            int physical = entry is { Kind: RegistryKind.Army, PhysicalSlot: int slot } ? slot : throw PostEconomyMissing("restoration_Army_pointer");
            var receiver = working.ReadArmyLifecycle()!.ReceiverSlots()[physical];
            UnitState? image = receiver.ActiveUnitId is int id
                ? working.Units.GetValueOrDefault(id) ?? throw PostEconomyMissing("restoration_active_Army") : receiver.PendingImage;
            byte control = receiver.SavedCommand?.Control ?? (image?.NativeCommandControl140 is byte known ? known : image is null
                ? receiver.CommandControl140 ?? throw PostEconomyMissing("restoration_retired_control140")
                : throw PostEconomyMissing("restoration_current_control140"));
            if (control == 0) continue;
            byte? action = null; ushort? identity = null;
            working = RestoreCurrentSavedCommand(working, current, receiver, image, player,
                operation.ReadCampaignDay, effects, ref action, ref identity);
        }
        return AppendNormalTurnMaintenanceEffects(working, effects, CampaignEffectSource.ArmyCommandMaintenance);
    }

    private GameState MaintainNormalTurnArmyCommandsCurrent(GameState state, SourceWorldContext current)
    {
        var operation = FacilityOperation.Playable(state);
        var working = state;
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Army) ?? throw PostEconomyMissing("maintenance_Army60"))
        {
            int physical = entry is { Kind: RegistryKind.Army, PhysicalSlot: int slot } ? slot : throw PostEconomyMissing("maintenance_Army_pointer");
            var image = NotificationArmy(working, physical);
            current.SetOwnedPersonCommandManager(working.PersonnelCommands);
            working = MaintainOrdinaryArmyCommand(working, image, operation, null, current);
            var maintained = NotificationArmy(working, physical);
            if (!ReferenceEquals(image.OverrideTarget, maintained.OverrideTarget))
                current.SetArmyCommandOverride(physical, ArmyOverrideTargetSourceAdapter.Read(working, maintained));
        }
        return working;
    }

    private static GameState AppendNormalTurnMaintenanceEffects(GameState state, List<CampaignEffectRequest> effects,
        CampaignEffectSource source) => effects.Count == 0 ? state : state with {
            CampaignEffectRequests = (state.CampaignEffectRequests ?? throw PostEconomyMissing("effect_owner"))
                .Append(source, state.NativeMapSeason?.AdvancedCalendarSteps ?? throw PostEconomyMissing("effect_revision"), effects) };
    private static T PostEconomyNeed<T>(T? value, string field) where T : struct => value ?? throw PostEconomyMissing(field);
    private static InvalidDataException PostEconomyMissing(string field) => new("normal.turn.post-economy." + field);
}
