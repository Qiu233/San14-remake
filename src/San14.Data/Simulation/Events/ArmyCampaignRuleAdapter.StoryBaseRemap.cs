using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    // Ordinary 141/150 share the current Area-City assignment kernel.
    // 251 borrows the same live pool and the exact single-Base 149 inner
    // transfer, but retains its own Force selection and maintenance tail.
    internal (GameState State, int NativeReturn) ApplyStoryBaseRemapCurrent(
        GameState state, SourceWorldContext current, WorldIndexContext pool,
        int code, IReadOnlyList<int> arguments, BaseReference? firstCity,
        BaseReference? secondCity, ForceReference? firstForce,
        ForceReference? secondForce, Action<int, int>? recordJoined)
    {
        if (arguments.Count != 8 || code is not (141 or 150 or 251))
            throw StoryBaseRemapMissing("operation");
        var working = state;
        if (code is 141 or 150)
        {
            var destination = code == 141 ? firstCity : secondCity;
            if (destination is null) throw StoryBaseRemapMissing("selected_destination_City");
            var changedAreas = new HashSet<int>();
            var changedPeople = new HashSet<int>();
            if (code == 141)
            {
                int raw = arguments[0];
                if (raw < 0) throw StoryBaseRemapMissing("negative_Area_table_index");
                var area = current.ReadAreaTableReference(raw < 501 ? raw : 0)
                    ?? throw StoryBaseRemapMissing("selected_Area_pointer");
                if (StoryBaseRemapNeed(current.ReadAreaNativeValidity(area), "selected_Area_validity") &&
                    StoryBaseRemapNeed(current.ReadBaseNativeValidity(destination), "destination_City_validity"))
                    Assign(area, destination);
            }
            else
            {
                var origin = firstCity ?? throw StoryBaseRemapMissing("selected_origin_City");
                if (StoryBaseRemapNeed(current.ReadBaseNativeValidity(origin), "origin_City_validity") &&
                    StoryBaseRemapNeed(current.ReadBaseNativeValidity(destination), "destination_City_validity"))
                    foreach (var gate in Gates())
                        if (CurrentGateMapQueries.FindCity(gate, current) == origin)
                        {
                            var area = CurrentGateMapQueries.FindArea(gate, current);
                            if (StoryBaseRemapNeed(current.ReadAreaNativeValidity(area), "Gate_Area_validity"))
                                Assign(area, destination);
                        }
            }
            if (changedAreas.Count == 0) return (working, 1);

            // Area35 is the persisted runtime parent. Gate district is derived
            // from this association; Gate ownership and Hex ownership do not
            // change merely because 141/150 reassigned a City association.
            var changedGates = new HashSet<int>();
            foreach (var gate in Gates())
                if (changedAreas.Contains(StoryBaseRemapNeed(CurrentGateMapQueries.FindArea(gate, current).PhysicalSlot,
                    "published_Gate_Area_identity")))
                    changedGates.Add(StoryBaseRemapNeed(gate.PhysicalSlot, "published_Gate_identity"));
            working = PersonReturnDistrictTransferSource.PublishAreaAssignments(working, current, changedAreas, changedGates);
            if (changedPeople.Count != 0)
            {
                var officers = new Dictionary<int, OfficerState>(working.Officers);
                foreach (int physical in changedPeople)
                {
                    int identity = StoryBaseRemapNeed(current.ReadPersonIdentity(physical), "assigned_Person_identity");
                    var before = officers.GetValueOrDefault(identity) ?? throw StoryBaseRemapMissing("assigned_Person_owner");
                    int home = StoryBaseRemapNeed(current.ReadPersonFirstLocation(physical), "assigned_Person_HomeE2");
                    officers[identity] = before with { LocationId = home == 0 ? null : home };
                }
                working = working with { Officers = officers };
            }
            return (working, 1);

            void Assign(AreaReference area, BaseReference city)
            {
                int selected = StoryBaseRemapNeed(city.PhysicalSlot, "destination_City_identity");
                var assignment = current.AssignAreaCity(area, selected);
                changedAreas.Add(StoryBaseRemapNeed(area.PhysicalSlot, "assigned_Area_identity"));
                foreach (int person in assignment.PersonPhysicalSlotsWritten) changedPeople.Add(person);
            }
        }

        var first = firstForce ?? throw StoryBaseRemapMissing("first_Force");
        var second = secondForce ?? throw StoryBaseRemapMissing("second_Force");
        if (StoryBaseRemapNeed(current.ReadForceNativeValidity(first), "first_Force_validity") &&
            StoryBaseRemapNeed(current.ReadForceNativeValidity(second), "second_Force_validity"))
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
                throw StoryBaseRemapMissing("current_pool_binding");
            BindStoryForceLifecycleCurrent(working, current);
            _personRelationInputs.BindCurrent(() => working, current);
            current.BindArmyMapOccupancy(working);
            current.BaseForcePolicyEffectReader = (force, effect) =>
                ForcePolicyScratchCurrent.ReadBeforeFreedom(working, current, force, effect, true).Value;
            var effects = new List<CampaignEffectRequest>();
            var candidates = current.CreateNearbyTemporary();
            try
            {
                // Both Force iterators are consumed before the first transfer.
                // Keep their manager order and duplicates, even when equal.
                Collect(first);
                Collect(second);
                var node = current.ReadRegistryHead(new(candidates.Identity))
                    ?? throw StoryBaseRemapMissing("candidate_head");
                while (node.Identity.HasValue)
                {
                    var target = current.ReadBaseRegistryNodePayload(node)
                        ?? throw StoryBaseRemapMissing("candidate_payload");
                    int before = StoryBaseRemapNeed(current.ReadBaseForceSlot(target), "old_Gate_Force");
                    var area = CurrentGateMapQueries.FindArea(target, current);
                    if (StoryBaseRemapNeed(current.ReadAreaNativeValidity(area), "candidate_Area_validity"))
                    {
                        int original = StoryBaseRemapNeed(current.ReadAreaValidityCode(area), "original_City34");
                        if (original is >= 1 and <= 51)
                        {
                            var city = current.ReadCityTableReference(original) ?? throw StoryBaseRemapMissing("original_City_pointer");
                            if (StoryBaseRemapNeed(current.ReadBaseNativeValidity(city), "original_City_validity"))
                            {
                                int owner = StoryBaseRemapNeed(current.ReadBaseForceSlot(city), "original_City_Force");
                                var force = current.ReadForceTableReference(owner < 52 ? owner : 0)
                                    ?? throw StoryBaseRemapMissing("original_City_Force_pointer");
                                if (force == first || force == second)
                                {
                                    var destination = ForcePrimaryDistrictSource.Resolve(current, force);
                                    if (StoryBaseRemapNeed(current.ReadDistrictNativeValidity(destination), "primary_District_validity"))
                                    {
                                        int district = StoryBaseRemapNeed(destination.PhysicalSlot, "primary_District_identity");
                                        int leaderId = StoryBaseRemapNeed(current.ReadDistrictLeader(district), "primary_District_leader12");
                                        var leader = (leaderId <= 6000 ? current.ReadPersonLookupReference(leaderId)
                                            : current.ReadTransferPersonTableReference(0)) ?? throw StoryBaseRemapMissing("primary_leader_pointer");
                                        int person = StoryBaseRemapNeed(leader.PhysicalSlot, "primary_leader_identity");
                                        var date = working.NativeMapSeason?.ThirdDate ?? throw StoryBaseRemapMissing("source_date");
                                        working = ApplyForceBaseTransferCurrent(working, current, pool, target, destination,
                                            new(person), date, effects, preserveResources: false, suppressMaintenance: false,
                                            fullCentralRadius: true,
                                            transferDistrict: operation => TransferDistrictAdministrationBaseCurrent(
                                                current, target, destination, operation, new(person)), unregisterFormerForce: true);
                                        working = CompleteStoryFootholdTransferCurrent(working, current, target, before, effects, recordJoined);
                                        working = BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true);
                                    }
                                }
                            }
                        }
                    }
                    node = current.ReadRegistryNodeNext(node) ?? throw StoryBaseRemapMissing("candidate_next");
                }
                Flush();
                working = MaintainEditedDeathArmyCommandsCurrent(working, current);
                var combat = TryRefreshNormalCombatEngagementsCurrent(working, current);
                working = combat.State ?? throw StoryBaseRemapMissing(combat.UnresolvedOwner ?? "combat_refresh");
                working = ApplyStoryFacilityAuraRefreshCurrent(working, current);
                var nearby = RefreshBaseNearbyArmiesCurrent(working, current);
                working = nearby.State ?? throw StoryBaseRemapMissing(nearby.UnresolvedOwner ?? "Base_nearby_refresh");
            }
            finally { current.ClearNearbyTemporary(candidates, true); }
            working = OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
            working = OwnedArmyLifecycleDetachedInputs.PublishCurrent(working, current);
            working = PersonAssignmentPrecallInputs.PublishCurrent(working, current);
            working = working.WithArmyLifecycle((working.ReadArmyLifecycle() ?? throw StoryBaseRemapMissing("publication_lifecycle"))
                with { Indexes = pool.ProjectOwnedRoles() });
            Flush();

            void Collect(ForceReference force)
            {
                int identity = StoryBaseRemapNeed(current.ReadForcePhysicalSlot(force), "iterator_Force_identity");
                if (identity is < 1 or > 51) return;
                foreach (var entry in current.ReadRegisteredBases() ?? throw StoryBaseRemapMissing("Base_registry80"))
                {
                    var target = entry ?? throw StoryBaseRemapMissing("Base_registry_payload");
                    if (StoryBaseRemapNeed(current.ReadBaseForceSlot(target), "iterator_Base_Force") != identity) continue;
                    int location = StoryBaseRemapNeed(current.ReadBaseObjectLink(target), "iterator_Base4E");
                    if (location is >= 52 and <= 61) _ = current.AppendCaptureBaseTemporary(candidates, target);
                }
            }
            void Flush()
            {
                if (effects.Count == 0) return;
                working = working with { CampaignEffectRequests = (working.CampaignEffectRequests
                    ?? throw StoryBaseRemapMissing("effect_queue")).Append(CampaignEffectSource.MonthlyMaintenance,
                        working.NativeClock?.UpdateOrdinal ?? throw StoryBaseRemapMissing("effect_revision"), effects) };
                effects.Clear();
            }
        }
        // 3B0500 reaches 33E2E0 even when either Force is invalid.
        return (RefreshEditedEventRegionStatistics(working, current), 1);

        IEnumerable<BaseReference> Gates()
        {
            foreach (var entry in current.ReadArmyReturnRegisteredGates() ?? throw StoryBaseRemapMissing("Gate_registryA0"))
                yield return entry ?? throw StoryBaseRemapMissing("Gate_registry_payload");
        }
    }

    private static T StoryBaseRemapNeed<T>(T? value, string field) where T : struct =>
        value ?? throw StoryBaseRemapMissing(field);
    private static InvalidDataException StoryBaseRemapMissing(string field) => new("story.Base_remap." + field);
}
