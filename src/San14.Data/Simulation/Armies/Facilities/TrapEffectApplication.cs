using San14.Core;
using static San14.Data.TrapParameters;

namespace San14.Data;

/// <summary>One borrowed facility operation: stage22 traps and stage14 object
/// damage share numerical callbacks, temporary lists and ordered intentions.
/// Each entry retains its own candidate collection and publication owner.</summary>
internal sealed partial class TrapEffectApplication
{
    private readonly ArmyCampaignRuleAdapter package;
    private readonly NativeArmyProgressStageMoment moment;
    private readonly WorldIndexContext pool;
    private readonly ActorlessArmyDamageApplication actorlessDamage;
    private readonly HashSet<int> changedPeople = [];
    private readonly SortedDictionary<int, int> grossDamage = [];
    private HexListHandle? victimForces;
    internal GameState State { get; private set; }
    internal SourceWorldContext Current { get; }
    internal TrapFeedback Feedback { get; }

    internal TrapEffectApplication(ArmyCampaignRuleAdapter package, GameState state,
        SourceWorldContext current, NativeArmyProgressStageMoment moment,
        GameResourceRepository repository, LoadedScenario scenario,
        List<CampaignEffectRequest>? requests = null)
    {
        this.package = package; State = state; Current = current; this.moment = moment;
        pool = current.ReadOwnedCurrentListPool() ?? throw Missing("shared_pool");
        Feedback = new(package, () => State, value => State = value, current, moment, repository, scenario,
            () => SamplerWorld().Day450, requests);
        actorlessDamage = new(package, current, moment, () => State, value => State = value,
            Feedback.Traits, Feedback.EnsurePersonnel, PublishPeople, person => changedPeople.Add(person),
            Feedback.Requests, SamplerWorld);
        BindQueue();
    }

    internal GameState Run()
    {
        var candidates = Current.CreateNearbyTemporary();
        try
        {
            TrapCandidates.Fill(State, Current, candidates);
            foreach (var entry in Entries(candidates))
            {
                if (entry.Kind != RegistryKind.Object) throw Missing("candidate_Object_kind");
                var facility = new ObjectReference(entry.PhysicalSlot);
                var reference = Definition(facility);
                var prepared = TrapCandidates.Prepare(State, Current, facility);
                if (prepared is null) continue;
                var nearby = Current.CreateNearbyTemporary();
                try
                {
                    var definition = Current.ReadFacilityEffectDefinition(reference) ?? throw Missing("trigger_definition_fields");
                    if (!TrapCandidates.IsTriggered(State, Current, prepared, definition, nearby)) continue;
                    bool worked = ApplyFacility(facility);
                    if (worked)
                    {
                        int kind = Need(reference.PhysicalSlot, "trigger_definition_identity");
                        Feedback.FacilityVisual(FacilityTrapPresentationOperation.ActivatedVisual, facility, kind, Position(facility));
                        Feedback.FacilityVisual(FacilityTrapPresentationOperation.ActivatedSmallMapNotice, facility, kind, Position(facility));
                    }
                    State = package.RemoveTriggeredTrap(State, Current, facility, Feedback.Requests);
                    //The caller reaches this District getter even if countered.
                    var district = Feedback.FacilityDistrict(prepared.Center);
                    if (worked && Feedback.PlayerForce(prepared.Force) &&
                        Need(Current.ReadDistrictNativeValidity(district), "caller_District_validity") &&
                        Feedback.RulerDistrict(district))
                        Feedback.TriggeredReport(facility, Need(reference.PhysicalSlot, "caller_definition_identity"), prepared.Force, prepared.Center);
                }
                finally { Current.ClearNearbyTemporary(nearby, true); }
            }
            Aggregate();
        }
        finally { Current.ClearNearbyTemporary(candidates, true); }
        State = ArmyReturnImagePublication.Publish(State, Current, []);
        PublishPeople();
        State = Feedback.Publish(State);
        State = OwnedArmyLifecycleTargets.PublishCurrent(State.WithArmyLifecycle(
            OwnedArmyDestructorFlags.PublishCurrent(State.ReadArmyLifecycle()!, Current) with { Indexes = pool.ExportOwnedState() }), Current);
        return State with { ArmyMapOccupancy = Current.ExportArmyMapOccupancy(),
            PendingCombatSettlements = Current.OwnedDestructionQueue ?? throw Missing("manager140_publication") };
    }

    private bool ApplyFacility(ObjectReference facility)
    {
        var channels = new List<HexListHandle>();
        try
        {
            channels.Add(Current.CreateNearbyTemporary()); channels.Add(Current.CreateNearbyTemporary());
            if (!Need(Current.ReadObjectNativeValidity(facility), "effect_Object_validity")) return false;
            var reference = Definition(facility);
            if (!Need(Current.ReadObjectDefinitionValidity(reference), "effect_definition_validity")) return false;
            int kind = Need(reference.PhysicalSlot, "effect_definition_identity");
            var definition = Current.ReadFacilityEffectDefinition(reference) ?? throw Missing("effect_definition_fields");
            FacilityEffectTargets.Fill(State, Current, facility, definition, kind, channels.ToArray(), category: 4);
            if (channels.All(x => Need(Current.ReadTemporaryHexListCount(x), "effect_count") == 0)) return false;
            //190F40 independently reloads the definition after collecting.
            reference = Definition(facility);
            if (!Need(Current.ReadObjectDefinitionValidity(reference), "effect_reloaded_definition_validity")) return false;
            kind = Need(reference.PhysicalSlot, "effect_reloaded_definition_identity");
            definition = Current.ReadFacilityEffectDefinition(reference) ?? throw Missing("effect_reloaded_definition");
            if (Need(Current.ReadObjectDefinitionRaw2C(reference), "effect_category") == 4 &&
                Countered(facility, kind, definition, channels.ToArray())) return false;
            PrepareVisuals(facility, channels.ToArray());
            victimForces = Current.CreateNearbyTemporary();
            try
            {
                if (Need(Current.ReadObjectNativeValidity(facility), "effect_ForceList_Object_validity"))
                    Current.ClearNearbyTemporary(victimForces, false);
                for (int channel = 0; channel < 2; channel++)
                {
                    if (Need(Current.ReadTemporaryHexListCount(channels[channel]), "dispatch_count") == 0) continue;
                    var copy = Current.CreateNearbyTemporary();
                    try
                    {
                        foreach (var hex in FacilityEffectTargets.ReadHexes(Current, channels[channel])) Current.AppendNearbyHexTemporary(copy, hex);
                        if (!Need(Current.ReadObjectNativeValidity(facility), "dispatch_Object_validity")) continue;
                        var dispatchReference = Definition(facility);
                        if (!Need(Current.ReadObjectDefinitionValidity(dispatchReference), "dispatch_definition_validity")) continue;
                        var dispatchDefinition = Current.ReadFacilityEffectDefinition(dispatchReference) ?? throw Missing("dispatch_definition_fields");
                        int dispatchKind = Need(dispatchReference.PhysicalSlot, "dispatch_definition_identity");
                        switch (dispatchDefinition.Channel(channel).Effect)
                        {
                            case 1: ApplyDamage(facility, dispatchKind, channel, copy); break;
                            case 2: ApplyAbnormal(facility, dispatchKind, channel, copy, 0); break;
                            case 3: ApplyAbnormal(facility, dispatchKind, channel, copy, 1); break;
                            case 17: ApplyFire(facility, channel, copy); break;
                            default: throw Missing("class4_effect" + dispatchDefinition.Channel(channel).Effect);
                        }
                    }
                    finally { Current.ClearNearbyTemporary(copy, true); }
                }
                ApplyRelations(facility);
            }
            finally { Current.ClearNearbyTemporary(victimForces, true); victimForces = null; }
            return true;
        }
        finally { for (int index = channels.Count - 1; index >= 0; index--) Current.ClearNearbyTemporary(channels[index], true); }
    }

    private ObjectDefinitionReference Definition(ObjectReference facility)
    {
        var record = Current.ReadPhysicalObject(Need(facility.PhysicalSlot, "Object_pointer")) ?? throw Missing("Object_fields");
        return Current.ReadObjectDefinitionReference(record.RawField12 <= 30 ? record.RawField12 : 0) ?? throw Missing("definition_pointer");
    }
    private int Position(ObjectReference facility)
    {
        ushort raw = Need(Current.ReadObjectHexIndex(facility), "Object_Hex10");
        var hex = Current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw Missing("Object_Hex_pointer");
        return Need(Current.ReadHexPhysicalSlot(hex), "Object_Hex_identity");
    }
    private UnitState Image(ArmyUnitReference army)
    {
        int at = Need(army.PhysicalSlot, "Army_physical");
        var arena = State.ReadArmyLifecycle() ?? throw Missing("Army_lifecycle");
        var receiver = arena.Receivers.Find(arena.Slots[at].ReceiverId) ?? throw Missing("Army_receiver");
        return receiver.ActiveUnitId is int id ? State.Units.GetValueOrDefault(id) ?? throw Missing("Army_active_image") :
            receiver.PendingImage ?? throw Missing("Army_pending_image");
    }
    private void WriteImage(ArmyUnitReference army, UnitState image)
    {
        var arena = State.ReadArmyLifecycle() ?? throw Missing("image_lifecycle");
        var receiver = arena.Receivers.Find(arena.Slots[Need(army.PhysicalSlot, "image_Army_physical")].ReceiverId)
            ?? throw Missing("image_receiver");
        if (receiver.ActiveUnitId is int id) State = State with { Units = new(State.Units) { [id] = image } };
        else State = State.WithArmyLifecycle(arena with { Receivers = arena.Receivers.With(receiver with { PendingImage = image }) });
    }
    private BattleRemakeCurrentFieldObjectTargetReceipt TargetStats(UnitState target) => package.TrapTargetStats(State, Current, target);
    private BattleDamageSourceParameters DamageParameters() => package.TrapDamageParameters(State, Current);
    private int Abilities(int person, int dimension) => package.TrapAbility(State, Current, person, dimension);
    private TacticSourceSamplerWorld SamplerWorld() =>
        moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.FacilityEffects
            ? package.ReadCurrentEnvironmentSampler(State, moment, (byte)NativeArmyProgressStage.FacilityEffects)
            : package.TrapSampler(State, moment);
    private int SamplerHour() => moment.PreWriterThirdDate.Hour;
    private int ForcePolicy(ForceReference force, int effect)
    {
        if (!Need(Current.ReadForceNativeValidity(force), "policy_Force_validity")) return 0;
        int at = Need(Current.ReadForcePhysicalSlot(force), "policy_Force_identity");
        var policies = State.Forces.GetValueOrDefault(at)?.PolicyEffects ?? throw Missing("policy_Force_bitmaps");
        if (!Need(policies.ReadActivation(effect), "policy_active1CB")) return 0;
        if (Need(policies.ReadSuppression(effect), "policy_suppressed1D5")) return 0;
        var owner=State.Forces.GetValueOrDefault(at)??throw Missing("policy_Force_owner");
        return Need(BattleRemakeCurrentForcePolicies.ReadForceEffect(State,owner,effect,Current),"policy_signed36");
    }
    private void RecordGross(ArmyUnitReference army, int amount)
    {
        int at = Need(army.PhysicalSlot, "gross_Army_pointer");
        grossDamage[at] = unchecked(grossDamage.GetValueOrDefault(at) + amount);
    }
    private void BindQueue() => Current.BindOwnedDestructionQueue(State.PendingCombatSettlements ?? throw Missing("manager140"), State);
    private void ApplyObjectDamage(ArmyUnitReference army, int amount, ObjectReference facility)
    {
        _ = actorlessDamage.Apply(army, amount, target =>
            Feedback.ArmyVisual(ArmyTrapPresentationOperation.TargetViewRefresh, target, facility,
                Need(Definition(facility).PhysicalSlot, "refresh_definition"), Need(Current.ReadArmyUnitRawHex2A(target), "refresh_Hex2A")));
    }
    private void PrepareVisuals(ObjectReference facility, HexListHandle[] channels)
    {
        if (!Need(Current.ReadObjectNativeValidity(facility), "prepare_Object_validity")) return;
        byte rawKind = (Current.ReadPhysicalObject(Need(facility.PhysicalSlot, "prepare_Object_pointer"))
            ?? throw Missing("prepare_Object_fields")).RawField12;
        if (rawKind is >= 11 and <= 13)
        {
            var definition = Current.ReadObjectDefinitionReference(rawKind) ?? throw Missing("prepare_definition_pointer");
            if (!Need(Current.ReadObjectDefinitionValidity(definition), "prepare_definition_validity")) return;
            int kind = Need(definition.PhysicalSlot, "prepare_definition_identity");
            float range = (Current.ReadFacilityEffectDefinition(definition) ?? throw Missing("prepare_range_fields")).MaximumFor(State, kind);
            Feedback.FacilityVisual(FacilityTrapPresentationOperation.FireRangeVisual, facility, kind, Position(facility), range);
            if (rawKind == 11) Feedback.FacilityVisual(FacilityTrapPresentationOperation.FireSmallMapNotice, facility, kind,
                Need(Current.ReadObjectHexIndex(facility), "prepare_notice_Hex10"));
            else if (rawKind == 12) Feedback.FacilityVisual(FacilityTrapPresentationOperation.StrongFireSmallMapNotice, facility, kind,
                Need(Current.ReadObjectHexIndex(facility), "prepare_notice_Hex10"));
        }
        else if (rawKind is 14 or 17)
        {
            int kind = Need(Definition(facility).PhysicalSlot, "prepare_target_definition");
            foreach (var channel in channels)
                foreach (var hex in FacilityEffectTargets.ReadHexes(Current, channel))
                {
                    int id = Need(Current.ReadHexPhysicalSlot(hex), "prepare_target_Hex_identity");
                    var indexed = Current.ReadHexTableReference(id < 48400 ? id : 0) ?? throw Missing("prepare_target_Hex_pointer");
                    if (!indexed.PhysicalSlot.HasValue) continue;
                    int raw = unchecked((short)Need(Current.ReadHexRaw1C(indexed), "prepare_target_signed1C"));
                    if (raw is < 1 or > 500) continue;
                    raw = unchecked((short)Need(Current.ReadHexRaw1C(hex), "prepare_target_signed1C_again"));
                    if (raw < 0) throw Missing("prepare_negative_Army_pointer");
                    var army = Current.ReadArmyUnitTableReference(raw <= 500 ? raw : 0) ?? throw Missing("prepare_target_Army_pointer");
                    if (!Need(Current.ReadArmyUnitNativeValidity(army), "prepare_target_Army_validity")) continue;
                    if (Need(Current.ReadArmyUnitNativeValidity(army), "prepare_target_Army_revalidation"))
                        Feedback.ArmyVisual(ArmyTrapPresentationOperation.TargetPreparationVisual, army, facility, kind,
                            Need(Current.ReadArmyUnitRawHex2A(army), "prepare_target_view_Hex2A"));
                    Feedback.ArmyVisual(ArmyTrapPresentationOperation.TargetPreparationSmallMapNotice, army, facility, kind,
                        Need(Current.ReadArmyUnitRawHex2A(army), "prepare_target_notice_Hex2A"));
                }
        }
    }
    private void ApplyFire(ObjectReference facility, int channel, HexListHandle targets)
    {
        if (!Need(Current.ReadObjectNativeValidity(facility), "fire_source_Object_validity")) return;
        int duration = 0;
        if (Need(Current.ReadObjectNativeValidity(facility), "fire_amount_Object_validity"))
        {
            var definition = Definition(facility);
            if (Need(Current.ReadObjectDefinitionValidity(definition), "fire_amount_definition_validity"))
                duration = (Current.ReadFacilityEffectDefinition(definition) ?? throw Missing("fire_amount_definition_fields")).Channel(channel).Amount;
        }
        foreach (var hex in FacilityEffectTargets.ReadHexes(Current, targets))
        {
            int at = Need(Current.ReadHexPhysicalSlot(hex), "fire_Hex_identity");
            State = package.IgniteTrapHex(State, Current, at, duration, moment);
            int raw = unchecked((short)Need(Current.ReadHexRaw1C(hex), "fire_signed1C"));
            if (raw == 0) continue;
            raw = unchecked((short)Need(Current.ReadHexRaw1C(hex), "fire_signed1C_again"));
            if (raw < 0) throw Missing("fire_negative_Army_pointer");
            var army = Current.ReadArmyUnitTableReference(raw <= 500 ? raw : 0) ?? throw Missing("fire_Army_pointer");
            if (Need(Current.ReadArmyUnitNativeValidity(army), "fire_Army_validity")) AddVictimForce(Current.ReadArmyUnitForceReference(army));
        }
    }
    private void AddVictimForce(ForceReference? force)
    {
        if (force is null) throw Missing("victim_Force_pointer");
        var list = victimForces ?? throw Missing("victim_Force_descriptor");
        //Each handler searches the actual chain before append. A suppressed
        //node allocation must be retried by a later target, hence no side set.
        if (Entries(list).Any(entry => entry.Kind == RegistryKind.Force && entry.PhysicalSlot == force.PhysicalSlot)) return;
        Current.AppendTrapRegistryTemporary(list, new(RegistryKind.Force, force.PhysicalSlot));
    }
    private void ApplyRelations(ObjectReference facility, int category = 4)
    {
        var copy = Current.CreateNearbyTemporary();
        try
        {
            foreach (var entry in Entries(victimForces ?? throw Missing("diplomacy_victim_list")))
                Current.AppendTrapRegistryTemporary(copy, entry);
            var seen = new HashSet<RegistryObjectReference>();
            var nodeRef = Current.ReadRegistryHead(new(copy.Identity)) ?? throw Missing("diplomacy_copy_head");
            while (nodeRef.Identity.HasValue)
            {
                var next = Current.ReadRegistryNodeNext(nodeRef) ?? throw Missing("diplomacy_copy_next");
                var entry = Current.ReadRegistryNodePayload(nodeRef) ?? throw Missing("diplomacy_copy_payload");
                if (!seen.Add(entry)) Current.RemoveFacilityHexTemporary(copy, new(nodeRef.Identity));
                nodeRef = next;
            }
            int raw = Need(Current.ReadObjectForceSlot13(facility), "diplomacy_Object13");
            var source = Current.ReadForceTableReference(raw <= 51 ? raw : 0) ?? throw Missing("diplomacy_source_Force");
            if (!Need(Current.ReadForceNativeValidity(source), "diplomacy_source_validity")) return;
            foreach (var entry in Entries(copy))
            {
                if (entry.Kind != RegistryKind.Force) throw Missing("diplomacy_Force_kind");
                var target = new ForceReference(entry.PhysicalSlot);
                if (!Need(Current.ReadForceNativeValidity(target), "diplomacy_target_validity")) continue;
                int other = Need(Current.ReadForcePhysicalSlot(target), "diplomacy_target_identity");
                if (other == 51) continue;
                other = Need(Current.ReadForcePhysicalSlot(target), "diplomacy_target_identity_again");
                if (other is >= 46 and <= 50) continue;
                int own = Need(Current.ReadForcePhysicalSlot(source), "diplomacy_source_identity");
                other = Need(Current.ReadForcePhysicalSlot(target), "diplomacy_target_identity_compare");
                if (own == other) continue;
                other = Need(Current.ReadForcePhysicalSlot(target), "diplomacy_target_relation_index");
                var relation = State.NativeArmySimulation?.Forces.GetValueOrDefault(own)?.NormalAftermath?.Relations13
                    ?? throw Missing("diplomacy_Relations13");
                int before = other < 52 ? relation[other] : 0;
                //190F40 chooses these coefficients after the actual Force
                //filters. Empty and ineligible victim lists read neither key.
                string ceiling = category == 4 ? RelationCeiling : FacilityObjectRelationCeiling;
                string adjustment = category == 4 ? RelationAdjustment : FacilityObjectRelationAdjustment;
                int Parameter(string key)=>category==4?Read(State,key):OrdinaryFacilityRelationParameter(key);
                if (before > Parameter(ceiling))
                    State = ForceRelationChangeCurrent.Apply(State, Current, own, other,
                        () => unchecked(Parameter(ceiling) - before), key => Read(State, key));
                State = ForceRelationChangeCurrent.Apply(State, Current, own, other,
                    () => Parameter(adjustment), key => Read(State, key));
            }
        }
        finally { Current.ClearNearbyTemporary(copy, true); }
    }
    private void Aggregate()
    {
        foreach (var (physical, gross) in grossDamage)
        {
            var army = new ArmyUnitReference(physical);
            var runtime = Image(army).NativeTacticRuntime ?? throw Missing("aggregate_Army4D_owner");
            if (runtime.StatModifiers4A is not { Length: 6 }) throw Missing("aggregate_Army4D");
            int at = Need(Current.ReadArmyUnitRawHex2A(army), "aggregate_Army2A");
            Feedback.Damage(ArmyFacilityDamagePresentationOperation.ConditionalFloatingNumber, army, at, gross, runtime.StatModifiers4A[3]);
            at = Need(Current.ReadArmyUnitRawHex2A(army), "aggregate_notice_Army2A");
            Feedback.Damage(gross < Read(State, DamageNoticeThreshold) ? ArmyFacilityDamagePresentationOperation.SmallMapNotice :
                ArmyFacilityDamagePresentationOperation.LargeMapNotice, army, at, gross);
        }
    }
    private IEnumerable<RegistryObjectReference> Entries(HexListHandle list)
    {
        var node = Current.ReadRegistryHead(new(list.Identity)) ?? throw Missing("list_head");
        while (node.Identity.HasValue)
        {
            yield return Current.ReadRegistryNodePayload(node) ?? throw Missing("list_payload");
            node = Current.ReadRegistryNodeNext(node) ?? throw Missing("list_next");
        }
    }
    private void PublishPeople()
    {
        if (changedPeople.Count == 0) return;
        var people = new Dictionary<int, OfficerState>(State.Officers);
        foreach (int physical in changedPeople)
        {
            int id = Need(Current.ReadPersonIdentity(physical), "publication_Person_identity");
            var person = people.GetValueOrDefault(id) ?? throw Missing("publication_Person_owner");
            if (person.TraitActivation is not { DurationSlotCount: 9, ActiveSlots: not null }) throw Missing("publication_full9_activation_owner");
            var activation = OfficerTraitActivationSourceAdapter.Read(Current, physical);
            if (activation is not { DurationSlotCount: 9, ActiveSlots: not null }) throw Missing("publication_lossless_activation");
            people[id] = person with { TraitActivation = activation };
        }
        State = State with { Officers = people };
    }
}

public sealed partial class SourceWorldContext
{
    internal void AppendTrapRegistryTemporary(HexListHandle list, RegistryObjectReference entry)
    {
        ConditionalRegistryEnter();
        try
        {
            var node = AllocateRegistryNode(new(list.Identity)) ?? throw TrapParameters.Missing("registry_append_node");
            if (node.Identity.HasValue) WriteRegistryNodePayload(node, entry);
        }
        finally { ConditionalRegistryExit(); }
    }
}
