using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>TC2CD1C0. The caller owns the ordered Person list and the live
    /// operation pool. Recall unregisters auxiliary personnel as status7;
    /// it does not run freedom placement or transfer their retained homes.</summary>
    internal GameState ApplySpecialPersonnelRecallCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, HexListHandle people,
        bool conditional, bool requireValidOriginalForce, bool suppressPrimary,
        NativeMapSeasonDate? date, List<CampaignEffectRequest> effects)
    {
        if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw SpecialRecallMissing("current_pool_binding");
        if (SpecialRecallNeed(current.ReadTemporaryHexListCount(people), "input_count") == 0) return state;
        var working = state;
        var districts = current.CreateTemporaryHexList() ?? throw SpecialRecallMissing("District_temporary");
        int count = 0, originalId = 0, specialId = 0;
        int Day() => (ReadArmyDestructionSampler(working, current) ?? throw SpecialRecallMissing("report_World450")).Day450;
        var oldAdvisor = current.AssignmentAdvisorResetCallbacks;
        var oldArea = current.AssignmentAreaOfficerRemoval;
        current.AssignmentAdvisorResetCallbacks = force =>
        {
            var reset = PersonAdvisorResetSource.ApplyWithinPersonCapture(working, current, pool, force);
            working = reset.State ?? throw SpecialRecallMissing(reset.UnresolvedOwner ?? "advisor_reset");
            BindPolicy(); return true;
        };
        current.AssignmentAreaOfficerRemoval = (area, officer) =>
        {
            var removed = PersonAssignmentPrecallSource.Apply2E01E0AreaRemoval(current, area, officer);
            if (removed.NativeReturn is null) throw SpecialRecallMissing(removed.UnresolvedOwner ?? "area_removal");
            if (removed.NativeReturn != 0 && removed.PersonPhysical is int touched)
                working = PersonAssignmentPrecallInputs.PublishDeathTouched(working, current, touched, [], publishVector: true);
            return removed.NativeReturn != 0;
        };
        try
        {
            foreach (var reference in Read(people))
            {
                if (reference is not { Kind: RegistryKind.Person, PhysicalSlot: int person })
                    throw SpecialRecallMissing("input_Person_pointer");
                if (!SpecialRecallNeed(current.ReadPersonNativeValidity(person), "Person_validity")) continue;
                var original = current.ReadPersonForce(person) ?? throw SpecialRecallMissing("Person_Force22CB40");
                bool originalValid = SpecialRecallNeed(current.ReadForceNativeValidity(original), "original_Force_validity");
                if (requireValidOriginalForce && !originalValid) continue;
                int ownId = SpecialRecallNeed(current.ReadForcePhysicalSlot(original), "original_Force60");
                if (originalId == 0) originalId = ownId;
                ushort id = SpecialRecallNeed(current.ReadPersonIdentity(person), "Person10");
                int family = id is >= 4001 and <= 4030 ? 46 + (id - 4001) / 6 :
                    id is >= 4101 and <= 4200 ? 46 + (id - 4101) / 20 : 0;
                if (family == 0) continue;
                var special = current.ReadForceTableReference(family) ?? throw SpecialRecallMissing("special_Force_pointer");
                if (!SpecialRecallNeed(current.ReadForceNativeValidity(special), "special_Force_validity")) continue;
                int tribe = SpecialRecallNeed(current.ReadForcePhysicalSlot(special), "special_Force60");
                if (specialId == 0) specialId = tribe;
                if (conditional)
                {
                    if (!originalValid) continue;
                    //The preceding2ADA50 is a pure relation-level query whose
                    //result is discarded.2C6550 also returns an unused value,
                    //but owns a real temporary allocation, so retain its call.
                    var eligibility = PersonReturnPathSource.ReadSpecialPersonnelEligibility(working, current,
                        original, tribe, ReadApplicationGameplay());
                    if (eligibility.Value is null) throw SpecialRecallMissing(eligibility.UnresolvedOwner ?? "2C6550");
                    if (SpecialRecallNeed(current.ReadConditionForceDependence(new(tribe)), "special_Force194") == ownId) continue;
                }
                int rawDistrict = SpecialRecallNeed(current.ReadPersonDistrictSlot(person), "old_DistrictE0");
                var district = current.ReadDistrictTableReference(rawDistrict < 52 ? rawDistrict : 0)
                    ?? throw SpecialRecallMissing("old_District_pointer");
                if (SpecialRecallNeed(current.ReadDistrictNativeValidity(district), "old_District_validity"))
                {
                    var payload = new RegistryObjectReference(RegistryKind.District,
                        SpecialRecallNeed(district.PhysicalSlot, "old_District_identity"));
                    if (!Read(districts).Contains(payload)) Append(districts, payload);
                }
                //22A200(Person,1) tests the actual Army12 identity, not its
                //domain officer field or a captive's custodian.
                ushort position = SpecialRecallNeed(current.ReadPersonSecondLocation(person), "own_Army_E4");
                var army = current.ReadArmyUnitTableReference(position is >= 62 and <= 561 ? position - 61 : 0)
                    ?? throw SpecialRecallMissing("own_Army_pointer");
                if (SpecialRecallNeed(current.ReadArmyUnitCommanderId(army), "own_Army12") != id)
                    army = current.ReadArmyUnitTableReference(0) ?? throw SpecialRecallMissing("own_Army_null");
                if (SpecialRecallNeed(current.ReadArmyUnitNativeValidity(army), "own_Army_validity") &&
                    !SpecialRecallNeed(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(working, current,
                        SpecialRecallNeed(army.PhysicalSlot, "own_Army_identity")), "own_Army233560"))
                {
                    var destroyed = new OwnedArmyDestructionStage(this).ApplySpecialPersonnelRecallCause4Current(
                        working, current, pool, SpecialRecallNeed(army.PhysicalSlot, "own_Army_identity"),
                        date ?? throw SpecialRecallMissing("cause4_operation_date"), effects);
                    working = destroyed.State ?? throw SpecialRecallMissing(destroyed.UnresolvedOwner ?? "cause4");
                }
                var linked = current.ReadPersonLinkedEntity(person) ?? throw SpecialRecallMissing("linked22AC00");
                if (SpecialRecallNeed(current.ReadLinkedEntityNativeValidity(linked), "linked_validity"))
                {
                    var detached = PersonReturnCommands.DetachPersonFromFirstAssociated(working, current, person);
                    working = working with { PersonnelCommands = detached.Owner
                        ?? throw SpecialRecallMissing(detached.UnresolvedOwner ?? "228890") };
                }
                current.WriteFormerForceKnowledge(person, 0);
                var assignments = new List<PersonAssignmentPrecallResult>();
                PersonDistrictTransfer.Apply(person, 0,
                    ArmyOrdinaryReturnRuntime.CreateCurrentPersonDistrictTransfer(current, assignments, result =>
                        working = ApplyPersonAssociationRemovalReports(working, current, person, result, Day, effects)));
                working = PersonAssignmentPrecallInputs.PublishDeathTouched(working, current, person, assignments);
                _ = current.RemoveAllRegisteredPersonReferences(new(RegistryKind.Person, person));
                current.WritePersonStatus(person, 7);
                current.SetOfficerHealth(id, OfficerHealth.Healthy);
                if (TalentForceIsPlayerCurrent(working, current, original))
                {
                    var removed = CommanderSelectionSource.RemoveCurrentOfficer(working, current, person);
                    working = removed.State ?? throw SpecialRecallMissing(removed.UnresolvedOwner ?? "29CCB0");
                }
                ushort flags = SpecialRecallNeed(current.ReadArmyReturnPersonFlags196(person), "Person15E");
                current.WriteArmyReturnPersonFlags196(person, (ushort)(flags & 0xFFFE));
                var owner = working.Officers.GetValueOrDefault(id) ?? throw SpecialRecallMissing("Person_owner");
                var status = OfficerServiceStatusSourceAdapter.Decode(7);
                var simulation = working.NativeArmySimulation ?? throw SpecialRecallMissing("Person_E0_saved_owner");
                working = working with { Officers = new(working.Officers) { [id] = owner with
                    { DistrictId = null, ForceId = null, ServiceStatus = status,
                        Role = OfficerServiceRules.PublishedRole(status, owner.Role), Health = OfficerHealth.Healthy,
                        LocationState = current.ReadOfficerLocation(id) ?? throw SpecialRecallMissing("Person_location_owner"),
                        Behavior = current.ReadPersonBehavior(person) } },
                    NativeArmySimulation = simulation with { Parameters = simulation.Parameters with
                    { Int32Values = new(simulation.Parameters.Int32Values)
                        { [OwnedArmyLifecycleDetachedInputs.Key("Person", person, "E0")] = 0 } } } };
                working = current.PublishOfficerAftermath(working, id);
                count++;
            }
            if (count > 0 && originalId != 0 && specialId != 0)
            {
                var original = current.ReadForceTableReference(originalId < 52 ? originalId : 0) ?? throw SpecialRecallMissing("report_original_Force");
                var special = current.ReadForceTableReference(specialId < 52 ? specialId : 0) ?? throw SpecialRecallMissing("report_special_Force");
                if (SpecialRecallNeed(current.ReadForceNativeValidity(original), "report_original_validity") &&
                    SpecialRecallNeed(current.ReadForceNativeValidity(special), "report_special_validity") &&
                    TalentForceIsPlayerCurrent(working, current, original))
                {
                    var formatted = FormatMonthlyMaintenanceReport(working, current, 0x324D,
                        new(Force0: specialId, Force1: originalId, Integers: [count]), next => working = next);
                    working = formatted.State;
                    effects.Add(new(null, null) { TalentCommandPresentation =
                        new(TalentCommandPresentationOperation.Announcement, null, null, null, null, formatted.Text) });
                }
            }
            if (!suppressPrimary)
            {
                var operation = CreateCaptureDistrictOperationQueries(current, () => working, next => working = next, date, effects);
                foreach (var reference in Read(districts))
                {
                    if (reference is not { Kind: RegistryKind.District, PhysicalSlot: int district }) throw SpecialRecallMissing("District_payload");
                    var pointer = current.ReadDistrictTableReference(district) ?? throw SpecialRecallMissing("District_pointer");
                    if (!SpecialRecallNeed(current.ReadDistrictNativeValidity(pointer), "District_validity")) continue;
                    var repaired = ArmyReturnPrimaryDistrictUpdater.ApplyCurrent(pointer, current, operation);
                    if (repaired.Packet is null) throw SpecialRecallMissing(repaired.UnresolvedOwner ?? "2E9140");
                }
                if (SpecialRecallNeed(current.ReadTemporaryHexListCount(districts), "District_count") != 0)
                    working = OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
                working = PersonReturnDistrictReorganizationSource.PublishCurrent(working, current, operation);
                working = PersonReturnDistrictTransferSource.PublishAreaAssignments(working, current,
                    operation.ChangedAreas, operation.ChangedGates);
            }
            return working;
        }
        finally
        {
            current.AssignmentAdvisorResetCallbacks = oldAdvisor;
            current.AssignmentAreaOfficerRemoval = oldArea;
            BindPolicy();
            current.ClearTemporaryHexList(districts); current.ReleaseTemporaryHexList(districts);
        }

        void BindPolicy() => current.BaseForcePolicyEffectReader = (force, effect) =>
            ForcePolicyScratchCurrent.ReadBeforeFreedom(working, current, force, effect, true).Value;
        IEnumerable<RegistryObjectReference> Read(HexListHandle list)
        {
            var node = current.ReadRegistryHead(new(list.Identity)) ?? throw SpecialRecallMissing("list_head");
            while (node.Identity.HasValue)
            {
                yield return current.ReadRegistryNodePayload(node) ?? throw SpecialRecallMissing("list_payload");
                node = current.ReadRegistryNodeNext(node) ?? throw SpecialRecallMissing("list_next");
            }
        }
        void Append(HexListHandle list, RegistryObjectReference payload)
        {
            bool locked = SpecialRecallNeed(current.ReadRegistryLockEnabled(), "list_lock");
            if (locked) current.EnterRegistryLock();
            try
            {
                var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw SpecialRecallMissing("list_node");
                if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, payload);
            }
            finally { if (locked) current.ExitRegistryLock(); }
        }
    }
    private static T SpecialRecallNeed<T>(T? value, string field) where T : struct => value ?? throw SpecialRecallMissing(field);
    private static InvalidDataException SpecialRecallMissing(string field) => new("special-personnel-recall." + field);
}
