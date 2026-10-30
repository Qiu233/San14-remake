using San14.Core;

namespace San14.Data;

internal sealed record ArmyBaseWallApplicationReceipt(float? WallAttack, int ObjectPhysicalSlot, int ModifiedRequest,
    int BeforeDurability, int AppliedLoss, int SignedIntermediate, ushort StoredDurability,
    byte PreviousTier, byte CurrentTier, BaseFatalAdmissionReceipt? Admission);
internal sealed record ArmyBaseWallApplicationResult(GameState State, bool SkipTroopStages)
{
    internal ArmyBaseWallApplicationReceipt? Receipt { get; init; }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>174C50/29EC80 for a real Army-to-Base pair, in its caller's
    /// current/pool/sampler. Fataltrue skips later troop stages, not174EC0.
    /// Unknown requires discarding this complete unpublished operation.</summary>
    internal ArmyBaseWallApplicationResult ApplyBorrowedArmyBaseWall(GameState state,
        SourceWorldContext current, NormalCombatMixedPair pair,
        NativeArmyCombatCycleSampler sampler, List<CampaignEffectRequest> effects)
    {
        if (pair.Actor.Kind != 27 || pair.Target.Kind is not (5 or 6)) throw WallMissing("pair_kind");
        //174C50 repeats the Hex-to-Hex2CA7B0 over descriptor HexC fields,
        //not the inner Force relation2CA650 alone.
        if (!WallThreatens(state, current, pair.Actor.HexId, pair.Target.HexId)) return new(state, false);
        var target = pair.Target.Kind == 5
            ? current.ReadCityTableReference(pair.Target.PhysicalSlot < 52 ? pair.Target.PhysicalSlot : 0)
            : current.ReadGateTableReference(pair.Target.PhysicalSlot < 11 ? pair.Target.PhysicalSlot : 0);
        if (target is null) throw WallMissing("Base_pointer");
        if (!WallNeed(current.ReadBaseNativeValidity(target), "Base_validity")) return new(state, false);
        ushort link = WallNeed(current.ReadBaseObjectLink(target), "Base4E");
        var obj = current.ReadObjectTableReference(link < 3001 ? link : 0) ?? throw WallMissing("Base_Object_pointer");
        bool special = target.Kind == BaseKind.City && WallNeed(current.ReadConditionBaseSpecialGovernor(target), "232950");
        if (!WallNeed(current.ReadObjectNativeValidity(obj), "Object_validity")) return new(state, false);
        if (pair.Actor.UnitId is not int actorId || !state.Units.TryGetValue(actorId, out var image) ||
            image.NativeArmyOrigin?.NativeSlotIndex != pair.Actor.PhysicalSlot) throw WallMissing("Army_identity");
        var actor = current.ReadArmyUnitTableReference(pair.Actor.PhysicalSlot < 501 ? pair.Actor.PhysicalSlot : 0)
            ?? throw WallMissing("Army_pointer");
        if (!WallNeed(current.ReadArmyUnitNativeValidity(actor), "Army_validity")) return new(state, false);
        if (special)
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw WallMissing("special_player_pointer");
            if (!WallNeed(current.ReadForceNativeValidity(player), "special_player_validity")) return new(state, false);
            int playerId = WallNeed(current.ReadForcePhysicalSlot(player), "special_player_identity");
            if (current.ReadArmyUnitRawForceSlot(actor) != playerId) return new(state, false);
        }
        var context = Context(current, sampler);
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        var queries = new TacticRemakeCurrentBattleQueries(context, current, Revision);
        float wall = new BattleRemakeCurrentArmyAttributes(_battleSeed, current, abilities, queries, Revision)
            .ResolveWallOffense(state, image) ?? throw WallMissing("current_WallAttack2AA720");
        if (!float.IsFinite(wall) || wall < 0 || (double)wall >= 2147483648d)
            throw WallMissing("current_WallAttack_int32_conversion");
        return ApplyBorrowedBaseWallDamage(state, current, context, image, actor, obj, (int)wall, wall, effects);
    }

    /// <summary>Actual29EC80 request writer shared by ordinary174C50 and
    /// tactic177610. The request is already an int32; it must not be rounded
    /// through float a second time. The caller owns special-governor selection
    /// and the instruction marker, and retains this same current/pool.</summary>
    private ArmyBaseWallApplicationResult ApplyBorrowedBaseWallDamage(GameState state,
        SourceWorldContext current, TacticRemakeCurrentContext context, UnitState image,
        ArmyUnitReference actor, ObjectReference obj, int request, float? wallAttack,
        List<CampaignEffectRequest> effects, NativeMapSeasonDate? objectSourceDate = null,
        Func<GameState, int>? readObjectCampaignDay = null)
    {
        if (!WallNeed(current.ReadObjectNativeValidity(obj), "request_Object_validity")) return new(state, false);
        if (current.ArmyReturnTransferMode is not { Present: false }) throw WallMissing("challenge570850_498020");
        int objectPhysical = WallNeed(obj.PhysicalSlot, "Object_physical");
        if (objectPhysical is < 1 or > 3000) throw WallMissing("actual_Object_class");
        var pool = current.ReadOwnedCurrentListPool() ?? throw WallMissing("pool");
        var pending = pool.PendingObjectRetirements ?? throw WallMissing("pending_F0");
        var directory = current.CurrentMapObjectDirectory ?? throw WallMissing("Object_directory");
        var receiver = directory.ReceiverAtPhysical(objectPhysical);
        foreach (var row in pending) if (row == receiver) return new(state, false);

        var working = state;
        var definition = FacilityConstructionLegality.DefinitionOf(current, obj);
        byte category = WallNeed(current.ReadObjectDefinitionRaw2C(definition), "definition2C");
        int policy = 0, effect = 0, plus = 0, minus = 0;
        if (category is >= 2 and <= 4)
        {
            var force = current.ReadArmyUnitForceReference(actor);
            int forceId = WallNeed(current.ReadForcePhysicalSlot(force), "policy_Force_identity");
            policy = _baseCapacityInputs.ReadPanelModifier(working, forceId, 69) ?? throw WallMissing("policy69");
        }
        if (category == 3)
        {
            int person = WallNeed(current.ReadPersonLookupReference(image.OfficerId <= 6000 ? image.OfficerId : 0)?.PhysicalSlot, "trait_Commander");
            var traits = TacticRemakeCurrentTraits.ArmyContributions(current, person, ArmyTraitSourceAdapter.Encode(working, image));
            if (traits is not { Length: 127 }) throw WallMissing("trait103");
            effect = traits[103];
            if (effect > 0) plus = ReadParameter(ObjectDamagePositiveTraitAddend);
            if (effect < 0) minus = ReadParameter(ObjectDamageNegativeTraitSubtractand);
        }
        //29EC80 accepts a signed int request. Preserve its exact multiply,
        //truncation and unchecked-add order, without querying the unrelated
        //ordinary troop-damage parameter family or reconverting an int tofloat.
        int policyPart = category is >= 2 and <= 4 && policy > 0
            ? Math.Max(1, unchecked(request * policy) / 100) : 0;
        int modified = unchecked(request + policyPart);
        if (category == 3 && effect != 0)
        {
            int multiplier = ReadParameter(SimulationParameterKeys.BattleDamage.WallPersonMultiplier);
            int change = unchecked(unchecked(effect * request) * multiplier) / 100;
            modified = effect > 0
                ? unchecked(unchecked(unchecked(request + Math.Max(1, change)) + plus) + policyPart)
                : Math.Max(1, unchecked(unchecked(unchecked(Math.Min(-1, change) - minus) + request) + policyPart));
        }
        if (effect != 0)
        {
            //The source packet carries the definition identity, independently
            //of Personality and Area operands. Preserve the Base-only boundary.
            if (objectPhysical <= 61) throw WallMissing("trait103_feedback33_definition_context");
            working = QueueObjectDamageAdjustmentSpeechCurrent(working, current, actor, image,
                WallNeed(definition.PhysicalSlot, "feedback_Object_definition"),
                objectSourceDate ?? throw WallMissing("feedback_source_date"));
        }
        if (modified > 0)
        {
            var reward = ApplyBorrowedNormalArmyReward(working, current, context, image.OfficerId, 1, modified);
            if (reward.Outcome == PersonNormalArmyRewardOutcome.CurrentInputsUnresolved)
                throw WallMissing(reward.MissingPhase ?? "Commander_reward_kind1");
            working = reward.State;
        }
        int before = WallNeed(current.ReadObjectRawField14(obj), "HP14_after_reward");
        int actorHex = WallNeed(current.ReadArmyUnitRawHex2A(actor), "Army2A");
        int objectHex = WallNeed(current.ReadObjectHexIndex(obj), "Object10");
        int required = objectPhysical <= 51 ? 2 : 1;
        bool permitZero = NormalCombatMixedProducer.Distance(actorHex, objectHex) == required;
        int applied = Math.Min(modified, before - (permitZero ? 0 : 1));
        //The signed remote-HP0 cap is-1 in the source, not a forced zero.
        int intermediate = unchecked(before - applied);
        current.WriteObjectPostLoadField14(obj, intermediate);
        ushort stored = WallNeed(current.ReadObjectRawField14(obj), "stored_HP14");
        working = PublishCurrentObjectDurability(working, current, obj, stored);
        if (objectPhysical > 61)
        {
            bool destroyed = intermediate <= 0;
            int oldObjectForce = WallNeed(current.ReadObjectForceSlot13(obj), "generic_Object13");
            working = StructureDestructionForceResult.Apply(working, current,
                current.ReadArmyUnitForceReference(actor), oldObjectForce, destroyed ? 0 : applied, destroyed);
            if (destroyed)
            {
                void Publish(GameState reached) => working = reached;
                var removal = ApplyDemolitionNumbers(working, current, obj, actor, Publish);
                if (removal is not null)
                {
                    working = removal.State;
                    working = AppendDemolitionEffects(working, current, actor, image, removal,
                        () => objectSourceDate ?? throw WallMissing("remove_source_date"),
                        readObjectCampaignDay ?? throw WallMissing("remove_source_day"), effects, Publish);
                }
            }
            //29EC80's generic branch returns0;174C50 separately chooses8.
            return new(working, false)
            { Receipt = new(wallAttack, objectPhysical, modified, before, applied, intermediate, stored, 0, 0, null) };
        }
        var parsed = objectPhysical <= 51 ? current.ReadCityTableReference(objectPhysical)
            : current.ReadGateTableReference(objectPhysical - 51);
        if (parsed is null) throw WallMissing("Object_class_Base_pointer");
        byte oldTier = WallNeed(current.ReadBasePostLoadFlag66(parsed), "old66");
        byte nextTier = oldTier;
        if (oldTier != 3)
        {
            if (WallNeed(current.ReadBaseNativeValidity(parsed), "tier_Base_validity") &&
                WallNeed(current.ReadArmyUnitNativeValidity(actor), "tier_Army_validity"))
            {
                _baseCapacityInputs.BindCurrent(working, current);
                nextTier = BasePostLoad.ReadFlag66(parsed, current);
                if (nextTier != oldTier)
                {
                    if (WallNeed(current.ReadBaseObjectLink(parsed), "tier_Base4E") < 52 &&
                        WallTierVisible(working, current, parsed) && nextTier is 2 or 3)
                    {
                        int canonicalCity = WallNeed(current.ReadBaseObjectLink(parsed), "cutscene_Base4E");
                        int sourceHex = current.ReadBaseCurrentHexIndex(parsed);
                        effects.Add(new(null, null) { BaseDamageTierCutscene = new(canonicalCity, nextTier, sourceHex) });
                    }
                    current.SetBasePostLoadFlag66(parsed, nextTier);
                    working = PublishWallTier(working, current, obj, nextTier);
                }
            }
        }
        int oldForce = WallNeed(current.ReadObjectForceSlot13(obj), "Force_result_Object13");
        bool fatal = intermediate <= 0;
        var winner = current.ReadArmyUnitForceReference(actor);
        working = ApplyBaseWallForceResult(working, current, winner, oldForce, fatal ? 0 : applied, fatal);
        BaseFatalAdmissionReceipt? admission = null;
        bool nativeReturn = false;
        if (fatal && WallNeed(current.ReadObjectForceSlot13(obj), "fatal_Object13") != 0)
        {
            admission = ApplyOrdinaryBaseFatalAdmission(working, current, image, parsed);
            if (admission.AdmissionKind == CombatSettlementAdmissionKind.Full)
                throw WallMissing("Full128_immediate2851B0_or284460");
            working = working with { PendingCombatSettlements = current.OwnedDestructionQueue ?? throw WallMissing("admitted_queue") };
            current.BindOwnedDestructionQueue(working.PendingCombatSettlements, working);
            nativeReturn = true;
        }
        return new(working, nativeReturn)
        { Receipt = new(wallAttack, objectPhysical, modified, before, applied, intermediate, stored, oldTier, nextTier, admission) };

        int ReadParameter(string key)
        {
            var bank = working.NativeArmySimulation?.Parameters ?? throw WallMissing("parameter_owner");
            return !bank.Float32Values.ContainsKey(key) && bank.Int32Values.TryGetValue(key, out int value)
                ? value : throw WallMissing(key);
        }
    }

    private static GameState PublishWallTier(GameState state, SourceWorldContext current, ObjectReference obj, byte tier)
    {
        var directory = current.CurrentMapObjectDirectory ?? throw WallMissing("tier_Object_directory");
        var subject = state.MapObjects?.Find(directory.ReceiverAtPhysical(WallNeed(obj.PhysicalSlot, "tier_Object_physical")))?.Subject
            ?? throw WallMissing("tier_subject");
        if (subject.Category == MapObjectCategory.City && subject.LocationId is int city && state.Cities.TryGetValue(city, out var c))
            return state with { Cities = new(state.Cities) { [city] = c with { DamageTier = tier } } };
        if (subject.Category == MapObjectCategory.Gate && subject.LocationId is int gate && state.Gates.TryGetValue(gate, out var g))
            return state with { Gates = new(state.Gates) { [gate] = g with { DamageTier = tier } } };
        throw WallMissing("tier_canonical_Base_owner");
    }

    private static bool WallTierVisible(GameState state, SourceWorldContext current, BaseReference target)
    {
        //233A00 checks player validity then compares actual Force pointers.
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw WallMissing("tier_player_pointer");
        if (WallNeed(current.ReadForceNativeValidity(player), "tier_player_validity"))
        {
            int raw = WallNeed(current.ReadBaseForceSlot(target), "tier_BaseB0");
            var force = current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw WallMissing("tier_Base_Force_pointer");
            if (force == player) return true;
        }
        var center = current.ReadHexTableReference(current.ReadBaseCurrentHexIndex(target)) ?? throw WallMissing("tier_center_Hex");
        var temporary = current.CreateNearbyTemporary();
        try
        {
            _ = NearbyArmyTemporarySource.FillNearbyHexTemporary(current, center, 3, temporary);
            var node = current.ReadSkillTemporaryHead(temporary) ?? throw WallMissing("tier_Hex_head");
            while (node.Identity.HasValue)
            {
                var hex = current.ReadHexListNodePayload(node) ?? throw WallMissing("tier_Hex_payload");
                if (hex.PhysicalSlot.HasValue)
                {
                    int raw = unchecked((short)WallNeed(current.ReadHexRaw1C(hex), "tier_Hex1C"));
                    if (raw < 0) throw WallMissing("tier_signed_negative_Army_lookup");
                    var army = current.ReadArmyUnitTableReference(raw < 501 ? raw : 0) ?? throw WallMissing("tier_Army_pointer");
                    //No Army native-validity gate is present here.
                    var force = current.ReadArmyUnitForceReference(army);
                    var freshPlayer = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw WallMissing("tier_near_player_pointer");
                    int playerId = WallNeed(current.ReadForcePhysicalSlot(freshPlayer), "tier_near_player_identity");
                    if (playerId != 0 && WallNeed(current.ReadForcePhysicalSlot(force), "tier_near_Force_identity") == playerId &&
                        WallThreatens(state, current, WallNeed(current.ReadHexPhysicalSlot(hex), "tier_Hex_ordinal22DEF0"),
                            current.ReadBaseCurrentHexIndex(target))) return true;
                }
                node = current.ReadHexListNodeNext(node) ?? throw WallMissing("tier_Hex_next");
            }
            return false;
        }
        finally { current.ClearNearbyTemporary(temporary, true); }
    }

    private static bool WallThreatens(GameState state, SourceWorldContext current, int fromHex, int toHex)
    {
        try { return PersonArmyCaptureSource.Threatens(state, current, fromHex, toHex); }
        catch (PersonArmyCaptureSource.MissingOwner error) { throw WallMissing("2CA7B0:" + error.Message); }
    }

    private static GameState ApplyBaseWallForceResult(GameState state, SourceWorldContext current,
        ForceReference winner, int oldForce, int appliedLoss, bool destroyed)
    {
        if (!WallNeed(current.ReadForceNativeValidity(winner), "result_Force_validity")) return state;
        //No new kind11 callback: only the original kind5/6 parameter choice
        //is supplied to the shared objective algorithm. DAT141A49920 is the
        //same installed scalar used by the existing field-object loss reader.
        int physical = WallNeed(current.ReadForcePhysicalSlot(winner), "result_Force_identity");
        var resolution = ForceCombatObjectiveRules.Resolve(new ForceCombatObjectiveCurrent(state, physical, current,
            new NormalArmyForceResultDefinitions(current), () => new(appliedLoss > 0
                ? Parameter(StructureDestructionForceResult.Kind11LossScale) : 0,
                destroyed ? Parameter(SimulationParameterKeys.ArmyDestruction.BaseCaptureResultGain) : 0), true),
            oldForce, appliedLoss, destroyed) ?? throw WallMissing("Base257670_objective");
        if (!resolution.Triggered) return state;
        var simulation = state.NativeArmySimulation ?? throw WallMissing("result_simulation");
        if (resolution.MarkSelectedForceDefeated)
        {
            var force = simulation.Forces.GetValueOrDefault(physical) ?? throw WallMissing("result_Force_owner");
            byte flags = WallNeed(force.Flags12, "result_flags12");
            simulation = simulation with { Forces = new(simulation.Forces) { [physical] = force with { Flags12 = (byte)(flags | 4) } } };
        }
        var forces = state.Forces;
        if (resolution.ProgressAfter is int progress)
        {
            var force = forces.GetValueOrDefault(physical) ?? throw WallMissing("result_objective_Force");
            var objective = force.CombatObjective ?? throw WallMissing("result_objective_owner");
            forces = new(forces) { [physical] = force with { CombatObjective = objective with { Progress = progress } } };
        }
        return state with { NativeArmySimulation = simulation, Forces = forces };

        int Parameter(string key)
        {
            var bank = state.NativeArmySimulation?.Parameters ?? throw WallMissing("result_parameter_owner");
            return !bank.Float32Values.ContainsKey(key) && bank.Int32Values.TryGetValue(key, out int value)
                ? value : throw WallMissing(key);
        }
    }

    private static T WallNeed<T>(T? value, string field) where T : struct => value ?? throw WallMissing(field);
    private static InvalidDataException WallMissing(string field) => new("normal.Army_Base_Wall." + field);
}
