using System.Collections.Immutable;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //212/213 borrow the same actual conditionRuntime City list used by203..214.
    //The single-Base149 writer's inner26DA10/3DF910 is shared, not its outer tail.
    private (GameState State, int? NativeReturn) ApplyStoryCityListOwnershipCurrent(GameState state,
        SourceWorldContext current, int code, IReadOnlyList<int> arguments, ForceReference? selectedForce,
        int? selectedPerson, IReadOnlyList<S14EventConditionCityPointer> cities, Action<int, int>? recordJoined,
        AutomaticEmperorCaptureInputs? emperorInputs = null)
    {
        var pool = current.ReadOwnedCurrentListPool() ?? throw StoryCityArmyMissing("City_transfer_pool");
        if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
            throw StoryCityArmyMissing("City_transfer_pool_binding");
        var working = state;
        var effects = new List<CampaignEffectRequest>();
        if (code == 212)
        {
            var force = selectedForce ?? throw StoryCityArmyMissing("City_transfer_Force");
            int person = selectedPerson ?? throw StoryCityArmyMissing("City_transfer_Person");
            if (StoryCityArmyNeed(current.ReadForceNativeValidity(force), "City_transfer_Force_validity") &&
                StoryCityArmyNeed(current.ReadPersonNativeValidity(person), "City_transfer_Person_validity"))
            {
                BindOperation();
                //22E930/22DD20 is captured once, before any City mutates its
                //former Force. It is not the selected executor's District.
                var destination = ForcePrimaryDistrictSource.Resolve(current, force);
                bool manager = StoryCityArmyNeed(current.ReadEventManagerPresent(), "City_transfer_manager_presence");
                if (manager) SetGate(true);
                for (int cityIndex = 0; cityIndex < cities.Count; cityIndex++)
                {
                    var city = Receiver(cities[cityIndex]);
                    int before = StoryCityArmyNeed(current.ReadBaseForceSlot(city), "City_transfer_old_Force");
                    working = ApplyForceBaseTransferCurrent(working, current, pool, city, destination, new(person), Date(), effects,
                        preserveResources: false, suppressMaintenance: false, fullCentralRadius: true,
                        transferDistrict: operation => TransferDistrictAdministrationBaseCurrent(current, city, destination, operation, new(person)),
                        unregisterFormerForce: true, automaticEmperorInputs: emperorInputs, allowAutomaticEmperorPause: true, eventPlaybackPresent: true);
                    if (working.ForceTransferEmperorTail is not null)
                    {
                        working = working with { StoryForceTransferContinuation = new(212,
                            StoryCityArmyNeed(city.PhysicalSlot, "Emperor_list_City"), before, person,
                            StoryCityArmyNeed(destination.PhysicalSlot, "Emperor_list_District"),
                            cities.Select(at => at.PhysicalSlot ?? throw StoryCityArmyMissing("Emperor_list_pointer")).ToImmutableArray(),
                            cityIndex, manager) };
                        Flush();
                        return (working, null);
                    }
                    working = CompleteStoryFootholdTransferCurrent(working, current, city, before, effects, recordJoined);
                    working = BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true);
                }
                if (manager) SetGate(false);
                //212 runs this reached maintenance before its unconditional
                //33E2E0, including the valid-but-empty list branch.
                Flush();
                working = MaintainEditedDeathArmyCommandsCurrent(working, current);
                RefreshCombatAndFacilities();
            }
            working = RefreshEditedEventRegionStatistics(working, current);
        }
        else if (code == 213)
        {
            BindOperation();
            foreach (var pointer in cities)
            {
                var city = Receiver(pointer);
                int before = StoryCityArmyNeed(current.ReadBaseForceSlot(city), "blank_City_old_Force");
                working = ApplyNeutralBaseCaptureTerritoryCurrent(working, current, pool, city, Date(), effects,
                    eagerAdvice: false, stopOnTerminal: false, literalNullExecutor: true, eventPlaybackPresent: true).State;
                working = CompleteStoryFootholdTransferCurrent(working, current, city, before, effects, recordJoined);
                //234920(mode1): the City's current captive Home iterator,
                //not all captives of its former Force or the old Army holder.
                int home = StoryCityArmyNeed(current.ReadBaseObjectLink(city), "blank_City_Home4E");
                if (home is >= 1 and <= 61)
                {
                    var descriptor = current.ReadRegistryDescriptor(RegistryKind.Person) ?? throw StoryCityArmyMissing("blank_Person100");
                    var node = current.ReadRegistryHead(descriptor) ?? throw StoryCityArmyMissing("blank_Person_head");
                    while (node.Identity.HasValue)
                    {
                        var payload = current.ReadRegistryNodePayload(node) ?? throw StoryCityArmyMissing("blank_Person_payload");
                        if (payload is not { Kind: RegistryKind.Person, PhysicalSlot: int captive }) throw StoryCityArmyMissing("blank_Person_pointer");
                        if (StoryCityArmyNeed(current.ReadPersonFirstLocation(captive), "blank_captive_E2") == home &&
                            StoryCityArmyNeed(current.ReadPersonStatus(captive), "blank_captive_E6") == 6)
                            working = ApplyCaptiveReleaseCurrent(working, current, captive, Date(),
                                () => (ReadArmyDestructionSampler(working, current) ?? throw StoryCityArmyMissing("blank_release_Day450")).Day450,
                                effects, ReadArmyDestructionSampler(working, current), reportRelease: false);
                        node = current.ReadRegistryNodeNext(node) ?? throw StoryCityArmyMissing("blank_Person_next");
                    }
                }
                working = BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true);
            }
            Flush();
            //213's tail has the opposite first-two order to212 and is
            //unconditional, even when the actual City list was empty.
            working = RefreshEditedEventRegionStatistics(working, current);
            working = MaintainEditedDeathArmyCommandsCurrent(working, current);
            RefreshCombatAndFacilities();
        }
        else throw StoryCityArmyMissing("City_transfer_opcode");
        working = OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
        working = OwnedArmyLifecycleDetachedInputs.PublishCurrent(working, current);
        working = PersonAssignmentPrecallInputs.PublishCurrent(working, current);
        working = working.WithArmyLifecycle((working.ReadArmyLifecycle() ?? throw StoryCityArmyMissing("City_transfer_lifecycle"))
            with { Indexes = pool.ProjectOwnedRoles() });
        Flush();
        return (working, 1);

        BaseReference Receiver(S14EventConditionCityPointer pointer) => new(BaseKind.City,
            pointer.PhysicalSlot ?? throw StoryCityArmyMissing("City_transfer_list_pointer"));
        NativeMapSeasonDate Date() => working.NativeMapSeason?.ThirdDate ?? throw StoryCityArmyMissing("City_transfer_date");
        void BindOperation()
        {
            BindStoryForceLifecycleCurrent(working, current);
            _personRelationInputs.BindCurrent(() => working, current);
            current.BindArmyMapOccupancy(working);
            current.BaseForcePolicyEffectReader = (force, effect) => ForcePolicyScratchCurrent.ReadBeforeFreedom(working, current, force, effect, true).Value;
        }
        void SetGate(bool blocked)
        {
            current.ReportBlockerState = new(true, blocked ? 1 : 0);
            working = working with { CampaignEventEligibility = (working.CampaignEventEligibility
                ?? throw StoryCityArmyMissing("City_transfer_manager_owner")) with { HistoryRecordingSuppressed = blocked } };
        }
        void RefreshCombatAndFacilities()
        {
            var combat = TryRefreshNormalCombatEngagementsCurrent(working, current);
            working = combat.State ?? throw StoryCityArmyMissing(combat.UnresolvedOwner ?? "City_transfer_combat_refresh");
            working = ApplyStoryFacilityAuraRefreshCurrent(working, current);
            var nearby = RefreshBaseNearbyArmiesCurrent(working, current);
            working = nearby.State ?? throw StoryCityArmyMissing(nearby.UnresolvedOwner ?? "City_transfer_nearby_refresh");
        }
        void Flush()
        {
            if (effects.Count == 0) return;
            working = working with { CampaignEffectRequests = (working.CampaignEffectRequests ?? throw StoryCityArmyMissing("City_transfer_effect_queue"))
                .Append(CampaignEffectSource.MonthlyMaintenance, working.NativeClock?.UpdateOrdinal
                    ?? throw StoryCityArmyMissing("City_transfer_effect_revision"), effects) };
            effects.Clear();
        }
    }
}
