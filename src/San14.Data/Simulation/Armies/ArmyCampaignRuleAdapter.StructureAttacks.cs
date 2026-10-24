using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private const string ObjectDamagePositiveTraitAddend = "simulation.object-damage.person103-positive-addend";
    private const string ObjectDamageNegativeTraitSubtractand = "simulation.object-damage.person103-negative-subtractand";

    /// <summary>Explicit playable Army→Object stage1 at the current coarse
    /// actual daily10h calendar cut. Generic targets return before troop damage, tactics,
    /// morale and counterattack. Retirement remains the following26/27 owner.</summary>
    private GameState CompletePlayableStructureAttacks(GameState state, FacilityOperation operation)
    {
        var selected = state.Units.Values.Where(unit => unit.MilitaryPlan is
            { Completed: false, Action: { ActionKey: "attack", Target.Kind: DeploymentTargetKind.Structure } })
            .Select(unit => unit.NativeArmyOrigin?.NativeSlotIndex).ToHashSet();
        if (selected.Count == 0) return state;
        var working = state;
        SourceWorldContext? current = null;
        var requests = new List<CampaignEffectRequest>();
        try
        {
            current = CaptureWritableTransportRouteCurrent(state);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            _baseCapacityInputs.BindCurrent(state, current);
            //This independent ordinary campaign has the same absent challenge
            //service as its existing fire/trap consumers; no App mode is inferred.
            current.ArmyReturnTransferMode = new(false, null);
            //The closure follows rewards and earlier actors in this operation.
            current.BaseForcePolicyEffectReader = (force, effect) => force.PhysicalSlot is int id &&
                working.Forces.TryGetValue(id, out var owner)
                    ? BattleRemakeCurrentForcePolicies.ReadForceEffect(working, owner, effect) : null;
            var registry = current.ReadRegistryReferences(RegistryKind.Army) ?? throw StructureAttackMissing("Army_registry");
            foreach (var entry in registry)
            {
                int physical = entry is { Kind: RegistryKind.Army, PhysicalSlot: int slot }
                    ? slot : throw StructureAttackMissing("Army_registry_pointer");
                if (!selected.Contains(physical)) continue;
                var image = NotificationArmy(working, physical);
                if (image.MilitaryPlan is not { Completed: false } plan ||
                    plan.NextWaypointIndex < plan.WaypointHexIds.Length) continue;
                var actor = current.ReadArmyUnitTableReference(physical) ?? throw StructureAttackMissing("Army_pointer");
                if (!StructureAttackKnown(current.ReadArmyUnitNativeValidity(actor), "Army_validity")) continue;
                if (StructureAttackKnown(ArmyConfigurationSourceAdapter.ReadAction(working, image), "Army_action") != 6) continue;
                if (working.Structures is not { } structures || !structures.TryGetValue(plan.Action.Target.Id, out var structure)) continue;
                var occupant = FieldHexOccupantSource.ReadCurrent(working, current, structure.HexId);
                var chosen = occupant.Receipt ?? throw StructureAttackMissing(occupant.UnresolvedOwner ?? "target_occupant");
                //A newly arrived Army/Base keeps the original occupant priority.
                if (chosen.NativeKind != 11) continue;
                var obj = chosen.CapturedObject ?? throw StructureAttackMissing("Object_pointer");
                var subject = working.MapObjects?.Find((current.CurrentMapObjectDirectory ??
                    throw StructureAttackMissing("Object_directory")).ReceiverAtPhysical(chosen.NativePhysicalSlot))?.Subject;
                if (subject?.StructureId != structure.Id ||
                    !StructureAttackKnown(current.ReadObjectNativeValidity(obj), "Object_validity")) continue;
                if (!CanAttackCurrentStructure(working, current, image, obj, chosen)) continue;
                var target = current.ReadArmyLifecycleTargetFields(physical) ?? throw StructureAttackMissing("Army_target");
                if (target.Kind38 != 11 || target.Identity3A is not ushort lookup ||
                    current.ReadObjectTableReference(lookup <= 3000 ? lookup : 0)?.PhysicalSlot != obj.PhysicalSlot)
                    throw StructureAttackMissing("Army_Object_target");
                ApplyCurrentStructureAttack(image, actor, obj);
            }
        }
        catch (InvalidDataException error)
        {
            //Ordinary advancement owns one transaction. A late unknown must
            //not adopt HP0 without its remover and reward again next period.
            //The host keeps the committed session and displays this exact leaf.
            throw new InvalidDataException("Current structure attack is unresolved: " + error.Message, error);
        }
        if (current?.ReadOwnedCurrentListPool() is { } pool && working.ReadArmyLifecycle() is { } arena)
            working = working.WithArmyLifecycle(arena with { Indexes = pool.ProjectOwnedRoles() });
        if (requests.Count != 0)
            working = working with { CampaignEffectRequests = (working.CampaignEffectRequests ??
                throw StructureAttackMissing("effects_owner")).Append(CampaignEffectSource.ArmyStructureAttack,
                operation.Revision, requests) };
        return working;

        void Publish(GameState reached) => working = reached;

        void ApplyCurrentStructureAttack(UnitState image, ArmyUnitReference actor, ObjectReference obj)
        {
            if (current!.ArmyReturnTransferMode is not { Present: false })
                throw StructureAttackMissing("challenge_570850");
            var receiver = current.CurrentMapObjectDirectory!.ReceiverAtPhysical(obj.PhysicalSlot!.Value);
            var pending = current.ReadOwnedCurrentListPool()?.PendingObjectRetirements ?? throw StructureAttackMissing("pending_objects");
            if (pending.Contains(receiver)) return;
            var context = Context(current);
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
            var queries = new TacticRemakeCurrentBattleQueries(context, current, Revision);
            float wall = new BattleRemakeCurrentArmyAttributes(_battleSeed, current, abilities, queries, Revision)
                .ResolveWallOffense(working, image) ?? throw StructureAttackMissing("current_WallAttack");
            var definition = FacilityConstructionLegality.DefinitionOf(current, obj);
            byte category = StructureAttackKnown(current.ReadObjectDefinitionRaw2C(definition), "definition_category");
            int policy = _baseCapacityInputs.ReadPanelModifier(working, image.ForceId, 69)
                ?? throw StructureAttackMissing("policy45");
            int effect = 0, plus = 0, minus = 0;
            if (category == 3)
            {
                int person = StructureAttackKnown(current.ReadPersonLookupReference(image.OfficerId <= 6000 ? image.OfficerId : 0)?.PhysicalSlot,
                    "Commander_pointer");
                var traits = TacticRemakeCurrentTraits.ArmyContributions(current, person, ArmyTraitSourceAdapter.Encode(working, image));
                if (traits is not { Length: 127 }) throw StructureAttackMissing("Army_trait103");
                effect = traits[103];
                if (effect > 0) plus = Parameter(ObjectDamagePositiveTraitAddend);
                if (effect < 0) minus = Parameter(ObjectDamageNegativeTraitSubtractand);
            }
            //HP is read after the positive modified-damage reward in29EC80.
            var parameters = context.ReadFieldObjectDamageParameters(working) ?? throw StructureAttackMissing("damage_parameters");
            var input = new WallDamageSourceInput(0, wall, true, category, policy, effect, plus, minus, true, null);
            int modified = BattleDamageSource.CalculateWallDamageBeforeCap(parameters, input);
            //1B4AB0 key33 carries a definition identity, not the existing
            //trait/Hex-context payload. Do not forge that different feedback.
            if (effect != 0) throw StructureAttackMissing("trait103_feedback33_definition_context");
            if (modified > 0)
            {
                var reward = _personReward.TryApplyWithinArmyOperation(working, current, Context(current), this, image.OfficerId, 1, modified);
                if (reward.Outcome == PersonNormalArmyRewardOutcome.CurrentInputsUnresolved)
                    throw StructureAttackMissing(reward.MissingPhase ?? "Commander_reward");
                Publish(reward.State);
            }
            int before = StructureAttackKnown(current.ReadObjectRawField14(obj), "HP14");
            int actorHex = StructureAttackKnown(current.ReadArmyUnitRawHex2A(actor), "Army_Hex2A");
            int objectHex = StructureAttackKnown(current.ReadObjectHexIndex(obj), "Object_Hex10");
            bool permitZero = NormalCombatMixedProducer.Distance(actorHex, objectHex) == 1;
            int applied = Math.Min(modified, before - (permitZero ? 0 : 1));
            if (applied < 0) throw StructureAttackMissing("signed_HP_damage");
            current.WriteObjectPostLoadField14(obj, unchecked(before - applied));
            Publish(PublishCurrentObjectDurability(working, current, obj,
                StructureAttackKnown(current.ReadObjectRawField14(obj), "stored_HP14")));
            working = working with { PeriodReports = [.. working.PeriodReports,
                new("structure_attack", image.ForceId, OfficerId: image.OfficerId, Amount: applied,
                    FromHexId: actorHex, ToHexId: objectHex) { RuleId = "ce-Army-174C50-Object-29EC80" }] };
            byte oldForce = StructureAttackKnown(current.ReadObjectForceSlot13(obj), "fatal_Object_force");
            bool destroyed = before - applied <= 0;
            var force = current.ReadArmyUnitForceReference(actor) ?? throw StructureAttackMissing("Army_Force_pointer");
            Publish(StructureDestructionForceResult.Apply(working, current, force,
                oldForce, destroyed ? 0 : applied, destroyed));
            if (!destroyed) return;
            var removal = ApplyDemolitionNumbers(working, current, obj, actor, Publish);
            if (removal is null) return;
            Publish(removal.State);
            Publish(AppendDemolitionEffects(working, current, actor, image, removal,
                operation.ReadDate, operation.ReadCampaignDay, requests, Publish));
        }

        int Parameter(string key)
        {
            var parameters = working.NativeArmySimulation?.Parameters ?? throw StructureAttackMissing("parameter_owner");
            return !parameters.Float32Values.ContainsKey(key) && parameters.Int32Values.TryGetValue(key, out int value)
                ? value : throw StructureAttackMissing(key);
        }
    }

    private bool CanAttackCurrentStructure(GameState state, SourceWorldContext current, UnitState image,
        ObjectReference target, FieldHexOccupantReceipt selected)
    {
        var definition = FacilityConstructionLegality.DefinitionOf(current, target);
        if (!StructureAttackKnown(current.ReadObjectDefinitionValidity(definition), "target_definition_validity") ||
            StructureAttackKnown(current.ReadObjectDefinitionRaw2C(definition), "target_definition_category") is not (2 or 3)) return false;
        int at = StructureAttackKnown(current.ReadArmyUnitRawHex2A(new(image.NativeArmyOrigin!.NativeSlotIndex)), "actor_Hex");
        int end = StructureAttackKnown(current.ReadObjectHexIndex(target), "target_Hex");
        var hex = current.ReadHexTableReference(at <= 48399 ? at : 0) ?? throw StructureAttackMissing("actor_Hex_pointer");
        ushort link = StructureAttackKnown(current.ReadHexObjectLink(hex), "actor_Hex_Object");
        if (link != 0 && current.ReadObjectTableReference(link <= 3000 ? link : 0)?.PhysicalSlot is >= 1 and <= 61)
        {
            var baseGate = CaptureBaseThreatSource.Evaluate(state, current, at, end);
            return baseGate.Threatens ?? throw StructureAttackMissing(baseGate.UnresolvedOwner ?? "Base_attack_gate");
        }
        int slot = unchecked((short)StructureAttackKnown(current.ReadHexRaw1C(hex), "actor_Hex_Army"));
        if (slot is < 1 or > 500) return false;
        var found = NotificationArmy(state, slot);
        var native = found.NativeTacticRuntime ?? throw StructureAttackMissing("actor_runtime");
        if (native.Abnormal23 != 0) return false;
        if (native.Abnormal25 != 0)
        {
            var forced = ArmyOverrideTargetSourceAdapter.Read(state, found);
            if (forced.Kind3C is not byte kind || forced.Identity3E is not ushort identity)
                throw StructureAttackMissing("actor_provocation");
            if (kind != selected.NativeKind || identity != selected.NativePhysicalSlot) return false;
        }
        int range = Context(current).ReadArmyNativePrimaryRange(state, found) ?? throw StructureAttackMissing("actor_range");
        if (NormalCombatMixedProducer.Distance(at, end) > range || found.Troops <= 0) return false;
        return StructureAttackKnown(ArmyLogisticsPreparation.ReadNativeRelationGate(state,
            current.ReadArmyUnitRawForceSlot(new(slot)), StructureAttackKnown(current.ReadObjectForceSlot13(target), "target_force")),
            "hostile_relation");
    }

    private static T StructureAttackKnown<T>(T? value, string field) where T : struct => value ?? throw StructureAttackMissing(field);
    private static InvalidDataException StructureAttackMissing(string field) => new("army.structure_attack." + field);
}
