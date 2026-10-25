using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>TC2A2760(from,to,1). The normal Turn owns this operation's
    /// current world and pool, including any outer Force candidate temporary.</summary>
    internal GameState ApplyNormalTurnForceAbsorptionCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, ForceReference from, ForceReference to)
    {
        if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
            throw ForceAbsorptionMissing("current_pool");
        var working = state;
        int original = AbsorptionK(current.ReadForcePhysicalSlot(from), "source_Force");
        int recipient = AbsorptionK(current.ReadForcePhysicalSlot(to), "recipient_Force");
        var date = working.NativeMapSeason?.ThirdDate ?? throw ForceAbsorptionMissing("source_date");
        var effects = new List<CampaignEffectRequest>();
        TacticSourceSamplerWorld Sampler() => ReadArmyDestructionSampler(working, current)
            ?? throw ForceAbsorptionMissing("source_World450_45C");
        int Day() => Sampler().Day450;

        //The route view has the actual registry/pool, but deliberately omits
        //several saved personnel/base input windows. Bind them once for the
        //reached business operation, without recapturing or restoring its pool.
        _personDestructionInputs.BindCurrent(working, current);
        _personRelationInputs.BindCurrent(() => working, current);
        _force164Inputs.BindCurrent(working, current);
        _moraleWaveInputs.BindCurrent(working, current);
        PersonArmyCaptureInputs.BindCurrent(working, current);
        RulerCaptureReleaseSource.BindCurrent(working, current);
        CaptureBaseThreatInputs.BindCurrent(working, current);
        _personAssignmentPrecallInputs.BindCurrent(working, current);
        _baseCapacityInputs.BindCurrent(working, current);
        PersonAdvisorResetSource.BindCurrent(working, current);
        ForceUnregisterInputs.BindCurrent(working, current);
        BaseCaptureCurrentInputs.BindCurrent(working, current);
        OwnedArmyReturnServiceInputs.BindCurrent(working, current);
        OwnedArmyLifecycleDetachedInputs.BindCurrent(working, current);
        if (current.ReadOwnedPersonCommandManager() is null) PersonReturnCommands.BindCurrent(working, current);
        current.BaseForcePolicyEffectReader = (force, effect) =>
            ForcePolicyScratchCurrent.ReadBeforeFreedom(working, current, force, effect, true).Value;

        //The recipient primary is captured before any source District work.
        var primary = ForcePrimaryDistrictSource.Resolve(current, to);
        HexListHandle? districts = null, bases = null;
        try
        {
            districts = current.CreatePersonInjuryTemporary();
            foreach (var entry in current.ReadArmyReturnRegisteredDistricts() ?? throw ForceAbsorptionMissing("District_registry"))
            {
                var district = entry ?? throw ForceAbsorptionMissing("District_pointer");
                int physical = AbsorptionK(district.PhysicalSlot, "District_physical");
                if (original is < 1 or > 51 || AbsorptionK(current.ReadDistrictForceId(physical), "District_Force") != original) continue;
                int leader = AbsorptionPerson(current, AbsorptionK(current.ReadDistrictLeader(physical), "District_leader"));
                if (AbsorptionK(current.ReadPersonNativeValidity(leader), "District_leader_validity") &&
                    AbsorptionK(current.ReadPersonStatus(leader), "District_leader_status") == 1) continue;
                AbsorptionAppend(current, districts, new(RegistryKind.District, physical));
            }
            //The original nonempty-list branch jumps over the293490 loop:
            //TC2A28FF CMP head,0;2A2904 JNZ2A295E. An empty list has no
            //iterations. Retain the real list lifetime; do not invent merges.

            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Force) ?? throw ForceAbsorptionMissing("Force_registry"))
            {
                int other = AbsorptionK(entry?.PhysicalSlot, "Force_payload");
                if (other == original) continue;
                if (original < 52) working = AbsorptionWriteRelation(working, current, other, original, false, 0);
                if (other < 52) working = AbsorptionWriteRelation(working, current, original, other, false, 0);
                //2EBF90(other,from,0): bilateral low terms clear first;
                //only the actual player's warning bit is then cleared.
                var otherForce = current.ReadForceTableReference(other) ?? throw ForceAbsorptionMissing("other_Force_pointer");
                if (original < 52) working = AbsorptionWriteRelation(working, current, other, original, true,
                    (byte)(ReadReportForceRelationByte(working, otherForce, original, ForceAbsorptionMissing) & 0x80));
                if (other < 52) working = AbsorptionWriteRelation(working, current, original, other, true,
                    (byte)(ReadReportForceRelationByte(working, from, other, ForceAbsorptionMissing) & 0x80));
                if (AbsorptionPlayer(current, working, otherForce))
                {
                    if (original < 52) working = AbsorptionWriteRelation(working, current, other, original, true, 0);
                }
                else if (AbsorptionPlayer(current, working, from) && other < 52)
                    working = AbsorptionWriteRelation(working, current, original, other, true, 0);
                var owner = working.Forces.GetValueOrDefault(other) ?? throw ForceAbsorptionMissing("request_Force_owner");
                var objective = owner.CombatObjective ?? throw ForceAbsorptionMissing("request_owner");
                bool clear = AbsorptionK(objective.RequestingForceId, "request_requester") == original;
                if (!clear)
                {
                    var target = BaseLookup.Find(AbsorptionK(objective.TargetLocationId, "request_target"), current);
                    clear = AbsorptionK(current.ReadBaseForceSlot(target), "request_target_Force") == original;
                }
                if (clear) working = working with { Forces = new(working.Forces) { [other] = owner with
                { CombatObjective = objective with { RequestingForceId = 0, TargetLocationId = 0, RemainingMonths = 0, Progress = 0 } } } };
            }

            int oldRuler = AbsorptionK(current.ReadForceRecord(original)?.RawRuler10, "source_ruler");
            int newRuler = AbsorptionK(current.ReadMoraleWaveRuler22EAC0(to), "recipient_ruler");
            //The item iterator does not require native validity or re-register
            //an already held item. Its writer clears4F bit3, then replaces4A.
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Item) ?? throw ForceAbsorptionMissing("Item_registry"))
            {
                int item = AbsorptionK(entry?.PhysicalSlot, "Item_pointer");
                if (oldRuler is < 1 or > 6000 || AbsorptionK(current.ReadCaptureItemOwner4A(item), "Item_owner") != oldRuler) continue;
                working = DWriteDiplomaticItem(working, current, item, Math.Clamp(newRuler, 0, 6000), true);
            }
            //Native lookup order includes aliases and all3001 object slots.
            for (int index = 0; index <= 3000; index++)
            {
                var obj = current.ReadObjectTableReference(index) ?? throw ForceAbsorptionMissing("Object_table");
                if (!AbsorptionK(current.ReadObjectNativeValidity(obj), "Object_validity") ||
                    AbsorptionK(current.ReadObjectForceSlot13(obj), "Object_Force") != original) continue;
                var definition = FacilityConstructionLegality.DefinitionOf(current, obj);
                if (!AbsorptionK(current.ReadObjectDefinitionValidity(definition), "Object_definition_validity") ||
                    AbsorptionK(current.ReadObjectDefinitionRaw2C(definition), "Object_category") is not (2 or 4)) continue;
                RemoveForceUnregisterObject(working, current, obj, value => working = value, effects.Add);
            }

            //Release every captive held in a source Base before changing any
            //Base ownership. The iterator follows the live Person chain.
            foreach (int person in AbsorptionForceIndependentPeople(current))
            {
                if (AbsorptionK(current.ReadPersonStatus(person), "captive_status") != 6) continue;
                var holder = PersonPositionRepair.DecodeSecondLocation(AbsorptionK(current.ReadPersonSecondLocation(person), "captive_position"));
                if (holder.Kind is not (5 or 6)) continue;
                var home = holder.Kind == 5 ? current.ReadCityTableReference(holder.Index <= 51 ? holder.Index : 0)
                    : current.ReadGateTableReference(holder.Index <= 10 ? holder.Index : 0);
                if (home is null) throw ForceAbsorptionMissing("captive_holder");
                if (AbsorptionK(current.ReadBaseForceSlot(home), "captive_holder_Force") == original)
                    working = ApplyCaptiveReleaseCurrent(working, current, person, date, Day, effects);
            }

            foreach (int person in AbsorptionForcePeople(current, original))
            {
                if (AbsorptionK(current.ReadPersonStatus(person), "Army_owner_status") is not (>= 1 and <= 4)) continue;
                var army = AbsorptionPersonArmy(current, person);
                if (!AbsorptionK(current.ReadArmyUnitNativeValidity(army), "Army_validity")) continue;
                var removed = new OwnedArmyDestructionStage(this).ApplyAbsorptionCommanderCause4Current(
                    working, current, pool, AbsorptionK(army.PhysicalSlot, "Army_pointer"), date, effects);
                working = removed.State ?? throw ForceAbsorptionMissing(removed.UnresolvedOwner ?? "Army_cause4");
            }
            working = ConsumeArmyLifecyclePendingCurrent(working, current, pool) ?? throw ForceAbsorptionMissing("Army_pending_cleanup");

            bases = current.CreatePersonInjuryTemporary();
            foreach (var entry in current.ReadRegisteredBases() ?? throw ForceAbsorptionMissing("Base_registry"))
            {
                var target = entry ?? throw ForceAbsorptionMissing("Base_pointer");
                if (original is < 1 or > 51 || AbsorptionK(current.ReadBaseForceSlot(target), "Base_Force") != original) continue;
                AbsorptionAppendBase(current, bases, target);
                var ruler = current.ReadPersonLookupReference(newRuler <= 6000 ? newRuler : 0)
                    ?? throw ForceAbsorptionMissing("recipient_ruler_pointer");
                working = ApplyForceAbsorptionBaseTransferCurrent(working, current, pool, target, primary, ruler, date, effects);
            }

            //Two ordered full Hex traversals are native business writers, not
            //online correctness scans. Their distinct timing preserves a live
            //third-party Army's later restoration of its occupied cells.
            for (int hex = 0; hex < 48400; hex++)
                if (AbsorptionK(current.ReadHexForceSlot(new(hex)), "source_Hex_Force") == original)
                    working = TransferAbsorptionHexCurrent(working, current, new(hex), recipient, 0);
            for (int hex = 0; hex < 48400; hex++)
            {
                var tile = new HexReference(hex);
                if (AbsorptionK(current.ReadHexForceSlot(tile), "recipient_Hex_Force") != recipient) continue;
                int rawArmy = AbsorptionK(current.ReadHexRaw1C(tile), "Hex_Army");
                var army = current.ReadArmyUnitTableReference(rawArmy is >= 1 and <= 500 ? rawArmy : 0)
                    ?? throw ForceAbsorptionMissing("Hex_Army_pointer");
                if (!AbsorptionK(current.ReadArmyUnitNativeValidity(army), "foreign_Army_validity")) continue;
                int physical = AbsorptionK(army.PhysicalSlot, "foreign_Army_pointer");
                if (AbsorptionK(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(working, current, physical), "foreign_Army_excluded")) continue;
                var force = current.ReadArmyUnitForceReference(army);
                int other = AbsorptionK(current.ReadForcePhysicalSlot(force), "foreign_Army_Force");
                if (!AbsorptionK(current.ReadForceNativeValidity(force), "foreign_Force_validity") || other == original || other == recipient ||
                    new DiplomacyCommandSource(working, current).HasTreaty(recipient, other)) continue;
                var mapped = CurrentGateMapQueries.FindCity(current.FindArmyUnitCurrentBase(army), current);
                working = TransferAbsorptionHexCurrent(working, current, tile, other,
                    AbsorptionK(mapped.PhysicalSlot, "foreign_Army_mapped_City"));
            }

            foreach (int person in AbsorptionForcePeople(current, original))
            {
                byte status = AbsorptionK(current.ReadPersonStatus(person), "transfer_Person_status");
                if (status != 6)
                {
                    if (status is not (>= 1 and <= 4)) continue;
                    if ((AbsorptionK(current.ReadOwnedReturnPersonFlags(person), "transfer_Person_flags") & 0x4000) == 0)
                    {
                        var home = BaseLookup.Find(AbsorptionK(current.ReadPersonFirstLocation(person), "transfer_Person_home"), current);
                        working = ApplyForceAbsorptionPersonJoinCurrent(working, current, person, primary, home, date, Day, effects).State;
                    }
                    else working = ApplyCaptiveFreedomCurrent(working, current, pool, person, date, Day, Sampler, effects);
                }
                else
                {
                    current.WriteFormerForceKnowledge(person, 0);
                    var assignments = new List<PersonAssignmentPrecallResult>();
                    PersonDistrictTransfer.Apply(person, 0, ArmyOrdinaryReturnRuntime.CreateCurrentPersonDistrictTransfer(current,
                        assignments, result => working = ApplyPersonAssociationRemovalReports(working, current, person, result, Day, effects)));
                    if (assignments.Count != 0) working = PersonAssignmentPrecallInputs.PublishDeathTouched(working, current, person, assignments);
                    byte loyalty = (AbsorptionK(current.ReadOwnedReturnPersonFlags(person), "captive_loyalty_flags") & 0x2000) != 0 ? (byte)255 : (byte)0;
                    current.SetPersonDestructionLoyaltyE8(person, loyalty);
                    working = ArmyDestructionLoyalty.PublishCurrentRawE8(working, current, person, loyalty);
                    var native = working.NativeArmySimulation ?? throw ForceAbsorptionMissing("captive_service_year_owner");
                    working = working with { NativeArmySimulation = native with { Parameters = native.Parameters with
                    { Int32Values = new(native.Parameters.Int32Values) { [OwnedArmyGovernorComparatorInputs.PersonWord166Key(person)] = 0 } } } };
                    working = current.PublishOfficerAftermath(working, AbsorptionK(current.ReadPersonIdentity(person), "captive_identity"));
                }
            }
            foreach (var target in AbsorptionBases(current, bases))
            {
                int location = AbsorptionK(current.ReadBaseObjectLink(target), "tail_Base_location");
                var governor = current.ReadArmyReturnBaseGovernor2E(target) ?? throw ForceAbsorptionMissing("tail_Base_governor");
                int person = AbsorptionPerson(current, governor);
                if (AbsorptionK(current.ReadPersonNativeValidity(person), "tail_governor_validity")) current.WritePersonStatus(person, 3);
            }
            working = BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true);
            var unregistered = new ForceUnregisterSource(this).TryApplyAbsorptionBorrowedCurrent(working, original, current, pool);
            working = unregistered.State ?? throw ForceAbsorptionMissing(unregistered.UnresolvedOwner ?? "Force_unregister");
            working = BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true);
            //Non-serving people may retain the old E0 when293BD0 resets its
            //District. Their derived Force changes even though E0 did not.
            Dictionary<int, OfficerState>? derivedPeople = null;
            foreach (var person in working.Officers.Values)
            {
                if (person.ForceId != original) continue;
                int physical = current.ResolveOfficerSource(person.Id) ?? throw ForceAbsorptionMissing("tail_Person_pointer");
                var actual = current.ReadPersonForce(physical) ?? throw ForceAbsorptionMissing("tail_Person_Force");
                int slot = AbsorptionK(current.ReadForcePhysicalSlot(actual), "tail_Person_Force_slot");
                int? forceId = slot == 0 ? null : slot;
                if (forceId == person.ForceId) continue;
                derivedPeople ??= new(working.Officers);
                derivedPeople[person.Id] = person with { ForceId = forceId };
            }
            if (derivedPeople is not null) working = working with { Officers = derivedPeople };
            if (effects.Count != 0) working = working with
            {
                CampaignEffectRequests = (working.CampaignEffectRequests ?? throw ForceAbsorptionMissing("effect_queue"))
                    .Append(CampaignEffectSource.MonthlyMaintenance,
                        working.NativeClock?.UpdateOrdinal ?? throw ForceAbsorptionMissing("effect_revision"), effects)
            };
            return working;
        }
        finally
        {
            if (bases is not null) current.ClearPersonInjuryTemporary(bases, true);
            if (districts is not null) current.ClearPersonInjuryTemporary(districts, true);
        }
    }

    private static IEnumerable<int> AbsorptionForceIndependentPeople(SourceWorldContext current)
    {
        foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw ForceAbsorptionMissing("Person_registry"))
            yield return AbsorptionK(entry, "Person_pointer");
    }
    private static IEnumerable<int> AbsorptionForcePeople(SourceWorldContext current, int force)
    {
        foreach (int person in AbsorptionForceIndependentPeople(current))
            if (force is >= 1 and <= 51 && AbsorptionK(current.ReadPersonOldForceSlot(person), "Person_Force") == force) yield return person;
    }
    private static ArmyUnitReference AbsorptionPersonArmy(SourceWorldContext current, int person)
    {
        ushort location = AbsorptionK(current.ReadPersonSecondLocation(person), "Person_Army_location");
        var army = current.ReadArmyUnitTableReference(location is >= 62 and <= 561 ? location - 61 : 0)
            ?? throw ForceAbsorptionMissing("Person_Army_pointer");
        return AbsorptionK(current.ReadArmyUnitCommanderId(army), "Person_Army_commander") ==
            AbsorptionK(current.ReadPersonIdentity(person), "Person_identity") ? army
            : current.ReadArmyUnitTableReference(0) ?? throw ForceAbsorptionMissing("Person_Army0");
    }
    private static int AbsorptionPerson(SourceWorldContext current, int raw) => AbsorptionK((raw <= 6000
        ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_lookup");
    private static bool AbsorptionPlayer(SourceWorldContext current, GameState state, ForceReference force)
    {
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw ForceAbsorptionMissing("player_pointer");
        int id = AbsorptionK(current.ReadForcePhysicalSlot(player), "player_ordinal");
        return id != 0 && id == AbsorptionK(current.ReadForcePhysicalSlot(force), "compared_Force");
    }
    private static void AbsorptionAppend(SourceWorldContext current, HexListHandle list, RegistryObjectReference payload)
    {
        if (AbsorptionK(current.ReadRegistryLockEnabled(), "append_lock")) current.EnterRegistryLock();
        try { var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw ForceAbsorptionMissing("append_node");
            if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, payload); }
        finally { if (AbsorptionK(current.ReadRegistryLockEnabled(), "append_unlock")) current.ExitRegistryLock(); }
    }
    private static void AbsorptionAppendBase(SourceWorldContext current, HexListHandle list, BaseReference target)
    {
        if (AbsorptionK(current.ReadRegistryLockEnabled(), "Base_append_lock")) current.EnterRegistryLock();
        try { var node = current.AllocateBaseRegistryNode(new(list.Identity)) ?? throw ForceAbsorptionMissing("Base_append_node");
            if (node.Identity.HasValue) current.WriteBaseRegistryNodePayload(node, target); }
        finally { if (AbsorptionK(current.ReadRegistryLockEnabled(), "Base_append_unlock")) current.ExitRegistryLock(); }
    }
    private static IEnumerable<BaseReference> AbsorptionBases(SourceWorldContext current, HexListHandle list)
    {
        var node = current.ReadRegistryHead(new(list.Identity)) ?? throw ForceAbsorptionMissing("Base_list_head");
        while (node.Identity.HasValue)
        {
            yield return current.ReadBaseRegistryNodePayload(node) ?? throw ForceAbsorptionMissing("Base_list_payload");
            node = current.ReadRegistryNodeNext(node) ?? throw ForceAbsorptionMissing("Base_list_next");
        }
    }
    private static T AbsorptionK<T>(T? value, string field) where T : struct => value ?? throw ForceAbsorptionMissing(field);
    private static InvalidDataException ForceAbsorptionMissing(string field) => new("normal.turn.force-absorption." + field);
}
