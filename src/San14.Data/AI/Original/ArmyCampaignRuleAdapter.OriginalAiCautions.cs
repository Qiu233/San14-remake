using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IArmyCautionCommandRules
{
    CommandResult IArmyCautionCommandRules.ClearInvalidArmyCautions(GameState state, ForceState force,
        ClearInvalidArmyCautionsOrder order, CommandContext context)
    {
        try
        {
            var current = CaptureOriginalAiDomesticCurrent(state);
            var working = state;
            foreach (int relation in order.RelationIds)
                if (!IsOriginalAiCautionArmyActiveCurrent(working, current, relation))
                    working = CancelOriginalAiCautionCurrent(working, relation).State;
            return CommandResult.Accept(working);
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, error.Message); }
    }

    CommandResult IArmyCautionCommandRules.CreateArmyCaution(GameState state, ForceState force,
        CreateArmyCautionOrder order, CommandContext context)
    {
        try { return CreateArmyCautionCurrent(state, force.Id, order.UnitId, order.BaseMapObjectId); }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, error.Message); }
    }

    internal bool IsOriginalAiCautionTargetCurrent(GameState state, SourceWorldContext current,
        int unitId, int baseMapObjectId)
    {
        int physical = WorldIndexContext.FindArmyPhysical(state, unitId)
            ?? throw CautionMissing("Army_receiver");
        var grouping = new ArmyMapGroupingCurrentQueries(state, current, _scenario, _governorInputs,
            Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        if (CautionKnown(grouping.IsAtHome2C96E0(physical), "Army_at_home")) return false;
        if (!CautionKnown(current.ReadArmyUnitNativeValidity(new(physical)), "Army_validity")) return false;
        var target = BaseLookup.Find(baseMapObjectId, current);
        //The node's gate reads the virtual owner, without another base-validity test.
        return CautionKnown(current.ReadBaseForceSlot(target), "Base_owner") != 0;
    }

    internal int ReadOriginalAiCautionActorForce(SourceWorldContext current, OriginalAiActor actor)
    {
        //The same actual actor resolver handles CTroops, District and Force.
        //A military group is resolved through its current first-member District,
        //not through the stored ForceId of an unrelated actor type.
        return (ReadOriginalAiMyselfForce(current, actor)
            ?? throw CautionMissing("actor_Force_pointer")).Id;
    }

    internal bool OriginalAiCautionTargetsArmy(GameState state, int relationId, int unitId)
    {
        if ((uint)relationId >= 1001) throw CautionMissing("relation_identity");
        var relation = (state.ReadArmyLifecycle()?.ArmyCautions ?? throw CautionMissing("relations_owner"))
            .GetRelation(relationId);
        return relation.Unit.Kind switch
        {
            ArmyCautionUnitKind.Unknown => throw CautionMissing("relation_Army"),
            ArmyCautionUnitKind.Unit => relation.Unit.UnitId is int target
                ? target == unitId : throw CautionMissing("relation_Army_identity"),
            //An expired logical target cannot rebind to a subsequently reused receiver.
            ArmyCautionUnitKind.None or ArmyCautionUnitKind.Expired => false,
            _ => throw CautionMissing("relation_Army_kind")
        };
    }

    internal bool IsOriginalAiCautionArmyActiveCurrent(GameState state, SourceWorldContext current, int relationId)
    {
        if ((uint)relationId >= 1001) throw CautionMissing("relation_identity");
        var relation = (state.ReadArmyLifecycle()?.ArmyCautions ?? throw CautionMissing("relations_owner"))
            .GetRelation(relationId);
        switch (relation.Unit.Kind)
        {
            case ArmyCautionUnitKind.Unknown: throw CautionMissing("relation_Army");
            case ArmyCautionUnitKind.None:
            case ArmyCautionUnitKind.Expired: return false;
            case ArmyCautionUnitKind.Unit:
                int id = relation.Unit.UnitId ?? throw CautionMissing("relation_Army_identity");
                //A retired logical target cannot bind to a recycled native slot.
                if (WorldIndexContext.FindArmyPhysical(state, id) is not int physical) return false;
                return CautionKnown(current.ReadArmyUnitNativeValidity(new(physical)), "relation_Army_validity");
            default: throw CautionMissing("relation_Army_kind");
        }
    }

    private CommandResult CreateArmyCautionCurrent(GameState state, int actorForce, int unitId, int baseMapObjectId)
    {
        var current = CaptureOriginalAiDomesticCurrent(state);
        if (!IsOriginalAiCautionTargetCurrent(state, current, unitId, baseMapObjectId))
            return CommandResult.Reject("Army caution target is ineligible.", "army-caution.target");
        var lifecycle = state.ReadArmyLifecycle() ?? throw CautionMissing("lifecycle");
        var cautions = lifecycle.ArmyCautions ?? throw CautionMissing("relations_owner");
        int? selected = null;
        //Actual table order, including the nonallocatable sentinel. Native
        //invalid partial rows are eligible; absence from the registry alone is not.
        for (int slot = 0; slot < 1001; slot++)
        {
            var pointer = current.ReadRegistryTableReference(RegistryKind.Table81B50, slot)
                ?? throw CautionMissing("relation_table_pointer");
            if (pointer.PhysicalSlot is not int physical || physical == 0) continue;
            if (!CautionKnown(current.ReadRegistryObjectNativeValidity(pointer), "relation_validity"))
            { selected = physical; break; }
        }
        if (selected is not int relationId)
            return CommandResult.Reject("No inactive caution relation is available.", "army-caution.capacity");
        if (!lifecycle.Indexes.Registries.TryGetValue(WorldEntityKind.ArmyCaution, out var registry) || registry is null)
            throw CautionMissing("registry");
        var at = BaseLookup.Find(baseMapObjectId, current);
        int location = Math.Min(61, (int)CautionKnown(current.ReadBaseObjectLink(at), "Base4E"));
        int armyPhysical = WorldIndexContext.FindArmyPhysical(state, unitId) ?? throw CautionMissing("Army_receiver_again");
        bool registered = armyPhysical is >= 1 and <= 500 && location >= 1 &&
            !registry.Any(row => row.Kind == WorldEntityKind.ArmyCaution && row.Id == relationId);
        List<WorldEntityReference> updatedRegistry = new(registry);
        bool linked = false;
        if (registered)
        {
            updatedRegistry.Add(new(WorldEntityKind.ArmyCaution, relationId));
            int other = CautionKnown(current.ReadBaseForceSlot(at), "Base_owner_again");
            linked = other == actorForce || ReadOriginalAiCautionForeignAdmission(state, current, actorForce, other);
        }
        var relations = new Dictionary<int, ArmyCautionRelation>(cautions.Relations);
        if (linked) relations[relationId] = new(new(ArmyCautionUnitKind.Unit, unitId), new(true, location));
        else relations.Remove(relationId); //226BA0, including registration failure.
        var registries = new Dictionary<WorldEntityKind, List<WorldEntityReference>?>(lifecycle.Indexes.Registries)
        { [WorldEntityKind.ArmyCaution] = updatedRegistry };
        var published = state.WithArmyLifecycle(lifecycle with
        { ArmyCautions = cautions with { Relations = relations }, Indexes = lifecycle.Indexes with { Registries = registries } });
        return CommandResult.Accept(published) with { ArmyCautionCreation = new(relationId, linked) };
    }

    /// <summary>The separate global cancellation consumer32B4D0. The relation
    /// is reset even when absent from the registry; only its own occurrences
    /// are removed. Native027FF0 intentionally does not call this method.</summary>
    internal (GameState State, bool WasRegistered) CancelOriginalAiCautionCurrent(GameState state, int relationId)
    {
        if ((uint)relationId >= 1001) throw CautionMissing("cancel_relation_identity");
        var lifecycle = state.ReadArmyLifecycle() ?? throw CautionMissing("cancel_lifecycle");
        var cautions = lifecycle.ArmyCautions ?? throw CautionMissing("cancel_relations_owner");
        if (!lifecycle.Indexes.Registries.TryGetValue(WorldEntityKind.ArmyCaution, out var registry) || registry is null)
            throw CautionMissing("cancel_registry");
        var relations = new Dictionary<int, ArmyCautionRelation>(cautions.Relations);
        relations.Remove(relationId);
        var ordered = new List<WorldEntityReference>(registry);
        bool removed = ordered.RemoveAll(row => row.Kind == WorldEntityKind.ArmyCaution && row.Id == relationId) != 0;
        var registries = new Dictionary<WorldEntityKind, List<WorldEntityReference>?>(lifecycle.Indexes.Registries)
        { [WorldEntityKind.ArmyCaution] = ordered };
        return (state.WithArmyLifecycle(lifecycle with { ArmyCautions = cautions with { Relations = relations },
            Indexes = lifecycle.Indexes with { Registries = registries } }), removed);
    }

    private static bool ReadOriginalAiCautionForeignAdmission(GameState state, SourceWorldContext current, int actor, int other)
        => ReadOriginalAiForeignAdmissionCurrent(state, current, actor, other, 0);

    //26C1C0's four callers share the admission gates and select one row of
    //personality weights. Caution construction remains exactly mode0.
    private static bool ReadOriginalAiForeignAdmissionCurrent(GameState state, SourceWorldContext current,
        int actor, int other, int mode)
    {
        if ((uint)mode >= 4) throw CautionMissing("foreign_admission_mode");
        var first = current.ReadForceTableReference(actor is >= 0 and <= 51 ? actor : 0) ?? throw CautionMissing("actor_Force_pointer");
        var second = current.ReadForceTableReference(other is >= 0 and <= 51 ? other : 0) ?? throw CautionMissing("other_Force_pointer");
        if (!CautionKnown(current.ReadForceNativeValidity(first), "actor_Force_validity") ||
            !CautionKnown(current.ReadForceNativeValidity(second), "other_Force_validity")) return false;
        int a = CautionKnown(first.PhysicalSlot, "actor_Force_identity"), b = CautionKnown(second.PhysicalSlot, "other_Force_identity");
        if (a == b) return false;
        byte alliance = ReadReportForceRelationByte(state, first, b, CautionMissing);
        bool coalition = CautionKnown(current.ReadConditionForceCoalitionMembership(new(a)), "actor_coalition") &&
            CautionKnown(current.ReadConditionForceCoalitionMembership(new(b)), "other_coalition");
        int friendship = CautionKnown(current.ReadConditionForceFriendshipLevel(new(a), new(b)), "friendship_level");
        int ruler = Ruler(a), otherRuler = Ruler(b);
        //22B9D0 mode0 builds precisely the valid eight-entry D0 list.
        bool disliked = false;
        for (int index = 0; index < 8; index++)
        {
            ushort id = CautionKnown(OwnedPersonRelationInputs.ReadCurrentDislikeIdentity(state, ruler, index), "ruler_D0_" + index);
            int person = Person(id);
            if (CautionKnown(current.ReadPersonNativeValidity(person), "D0_Person_validity") && person == otherRuler) disliked = true;
        }
        if (!((alliance & 127) > 5 || coalition) || friendship != 5 || disliked) return false;
        int penalty = Word(ruler, 0x17A) > 19 && Word(otherRuler, 0x17A) > 19 ? -10 : -60;
        ReadOnlySpan<int> weights = mode switch
        {
            0 => [2, 6, 4, 2, 0],
            1 => [2, 2, 0, 2, 4],
            2 => [4, 0, 0, 0, 4],
            _ => [0, 0, 2, 4, 2]
        };
        int score = unchecked(weights[0] * Word(ruler, 0x16A) +
            weights[1] * CautionKnown(current.ReadAllegianceDisposition(ruler), "ruler16C") +
            weights[2] * Word(ruler, 0x16E) + weights[3] * Word(ruler, 0x178) +
            (weights[4] == 0 ? 0 : weights[4] * Word(ruler, 0x17C)) + penalty);
        return score > 0;

        int Ruler(int force) => Person(current.ReadForceRecord(force)?.RawRuler10 ?? throw CautionMissing("Force_ruler10"));
        int Person(ushort id) => (id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot
            ?? throw CautionMissing("Person_lookup_pointer");
        int Word(int person, int field) => CautionKnown(RulerCaptureReleaseInputs.ReadCurrentWord(state, person, field), "Person_" + field.ToString("X"));
    }

    private static T CautionKnown<T>(T? value, string field) where T : struct => value ?? throw CautionMissing(field);
    private static InvalidDataException CautionMissing(string field) => new("original_ai.cautions." + field);
}
