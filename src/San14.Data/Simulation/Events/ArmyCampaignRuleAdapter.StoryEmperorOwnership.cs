using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>382 resolves its City before its Person. An invalid City
    /// returns0 without touching the second selector or running the tail.</summary>
    internal StoryEmperorApplication ApplyStoryEmperorOwnershipCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, BaseReference selectedCity,
        int? personPhysical, int treatment, Action<int, int>? recordJoined,
        StoryForcedEmperorCaptureInputs? emperorInputs = null)
    {
        if (!StoryTerritoryNeed(current.ReadBaseNativeValidity(selectedCity), "forced_Emperor_City_validity"))
            return new(state, 0);
        if (selectedCity.Kind != BaseKind.City) throw StoryTerritoryMissing("forced_Emperor_expected_City");
        if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
            throw StoryTerritoryMissing("forced_Emperor_current_pool");
        int person = personPhysical ?? throw StoryTerritoryMissing("forced_Emperor_selected_Person");
        var target = BaseLookup.Find(StoryTerritoryNeed(current.ReadBaseObjectLink(selectedCity),
            "forced_Emperor_City4E"), current);
        var working = state;
        var effects = new List<CampaignEffectRequest>();
        _ = BorrowOwnedArmyReturnWorld(working, current, pool);
        _personRelationInputs.BindCurrent(() => working, current);
        current.BindArmyMapOccupancy(working);
        current.BaseForcePolicyEffectReader = (force, effect) =>
            ForcePolicyScratchCurrent.ReadBeforeFreedom(working, current, force, effect, true).Value;
        if (StoryTerritoryNeed(current.ReadBaseNativeValidity(target), "forced_Emperor_Base_validity"))
        {
            int before = StoryTerritoryNeed(current.ReadBaseForceSlot(target), "forced_Emperor_previous_Force");
            var date = working.NativeMapSeason?.ThirdDate ?? throw StoryTerritoryMissing("forced_Emperor_date");
            if (StoryTerritoryNeed(current.ReadPersonNativeValidity(person), "forced_Emperor_Person_validity"))
            {
                int raw = StoryTerritoryNeed(current.ReadPersonDistrictSlot(person), "forced_Emperor_PersonE0");
                var destination = current.ReadDistrictTableReference(raw < 52 ? raw : 0)
                    ?? throw StoryTerritoryMissing("forced_Emperor_destination_District");
                if (StoryTerritoryNeed(destination.PhysicalSlot, "forced_Emperor_District_identity") == 0)
                    working = ApplyNeutralBaseCaptureTerritoryCurrent(working, current, pool, target, date, effects,
                        eagerAdvice: false, stopOnTerminal: false, resourceExecutor: new(person), eventPlaybackPresent: true).State;
                else
                    working = ApplyForceBaseTransferCurrent(working, current, pool, target, destination, new(person), date, effects,
                        preserveResources: false, suppressMaintenance: false, fullCentralRadius: true,
                        transferDistrict: operation => TransferDistrictAdministrationBaseCurrent(current, target, destination,
                            operation, new(person)), unregisterFormerForce: true, forcedEmperorMode: treatment == 1 ? 1 : 2,
                        forcedEmperorInputs: emperorInputs, eventPlaybackPresent: true);
                working = CompleteStoryFootholdTransferCurrent(working, current, target, before, effects, recordJoined);
            }
            //The native wrapper rereads validity after the first transfer.
            if (!StoryTerritoryNeed(current.ReadPersonNativeValidity(person), "forced_Emperor_Person_validity_again"))
            {
                working = ApplyNeutralBaseCaptureTerritoryCurrent(working, current, pool, target, date, effects,
                    eagerAdvice: false, stopOnTerminal: false, literalNullExecutor: true, eventPlaybackPresent: true).State;
                working = CompleteStoryFootholdTransferCurrent(working, current, target, before, effects, recordJoined);
            }
            working = BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true);
        }
        if (effects.Count != 0)
            working = working with { CampaignEffectRequests = (working.CampaignEffectRequests
                ?? throw StoryTerritoryMissing("forced_Emperor_pending_owner")).Append(CampaignEffectSource.MonthlyMaintenance,
                    working.NativeClock?.UpdateOrdinal ?? throw StoryTerritoryMissing("forced_Emperor_clock"), effects.ToArray()) };
        return new(CompleteStoryFootholdRefreshCurrent(working, current), 1);
    }
}
