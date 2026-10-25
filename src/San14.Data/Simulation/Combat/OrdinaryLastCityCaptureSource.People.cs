using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //293BD0 first walks222FD0/223250. The middle Province and278090
    //association stages belong to the caller, before the later Person walk.
    internal GameState ApplyOrdinaryLastCityArmyCleanupCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, ForceReference defeated,
        NativeMapSeasonDate date, List<CampaignEffectRequest> effects)
    {
        RequireLastCityPeoplePool(current, pool);
        int force = LastCityPeopleNeed(current.ReadForcePhysicalSlot(defeated), "Army_Force60");
        var descriptor = current.ReadRegistryDescriptor(RegistryKind.Army)
            ?? throw LastCityPeopleMissing("Army_manager60");
        var node = current.ReadRegistryHead(descriptor) ?? throw LastCityPeopleMissing("Army_head");
        while (node.Identity.HasValue)
        {
            var entry = current.ReadRegistryNodePayload(node) ?? throw LastCityPeopleMissing("Army_payload");
            if (entry is not { Kind: RegistryKind.Army, PhysicalSlot: int physical })
                throw LastCityPeopleMissing("Army_receiver");
            //334740 has no additional Army native-validity test: first the
            //Force ordinal, then the actual233560 exclusion at this node.
            if (force is >= 1 and <= 51 &&
                LastCityPeopleNeed(current.ReadForcePhysicalSlot(current.ReadArmyUnitForceReference(new(physical))), "Army_Force22CD80") == force &&
                !LastCityPeopleNeed(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical), "Army233560"))
            {
                var result = new OwnedArmyDestructionStage(this).ApplyAbsorptionCommanderCause4Current(
                    state, current, pool, physical, date, effects);
                state = result.State ?? throw LastCityPeopleMissing(result.UnresolvedOwner ?? "259360_cause4");
            }
            node = current.ReadRegistryNodeNext(node) ?? throw LastCityPeopleMissing("Army_next");
        }
        return state;
    }

    //293BD0's later2349D0(filter4) loop observes the post-Army/association
    //current. Filter4 admits every status; only its1..4 branch calls265250.
    internal GameState ApplyOrdinaryLastCityRemainingPeopleCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, ForceReference defeated,
        ForceReference itemRecipient, NativeMapSeasonDate date, List<CampaignEffectRequest> effects)
    {
        RequireLastCityPeoplePool(current, pool);
        int force = LastCityPeopleNeed(current.ReadForcePhysicalSlot(defeated), "Person_Force60");
        var descriptor = current.ReadRegistryDescriptor(RegistryKind.Person)
            ?? throw LastCityPeopleMissing("Person_manager100");
        var node = current.ReadRegistryHead(descriptor) ?? throw LastCityPeopleMissing("Person_head");
        while (node.Identity.HasValue)
        {
            var entry = current.ReadRegistryNodePayload(node) ?? throw LastCityPeopleMissing("Person_payload");
            if (entry is not { Kind: RegistryKind.Person, PhysicalSlot: int physical })
                throw LastCityPeopleMissing("Person_receiver");
            if (force is >= 1 and <= 51 &&
                LastCityPeopleNeed(current.ReadPersonOldForceSlot(physical), "Person_Force22CF60") == force)
            {
                //2EACB0 is still reached for nonserving people. It queries
                //Person/recipient validity before the ruler and Item chain.
                if (LastCityPeopleNeed(current.ReadPersonNativeValidity(physical), "item_Person_validity") &&
                    LastCityPeopleNeed(current.ReadForceNativeValidity(itemRecipient), "item_Force_validity"))
                {
                    //22EAC0 returns the raw Force10 identity; it performs no
                    //Person lookup or ruler-native-validity query here.
                    int recipient = LastCityPeopleNeed(current.ReadForcePhysicalSlot(itemRecipient), "item_Force60");
                    int ruler = recipient == 0 ? 5014 : LastCityPeopleNeed(
                        current.ReadForceRecord(recipient)?.RawRuler10, "item_recipient_Force10");
                    if (ruler is >= 1 and <= 6000)
                        state = TransferLastCityPersonItemsCurrent(state, current, physical, (ushort)ruler);
                }
                byte status = LastCityPeopleNeed(current.ReadPersonStatus(physical), "PersonE6");
                if (status == 6) state = ResetOrdinaryLastCityCaptiveCurrent(state, current, physical, effects);
                else if (status is >= 1 and <= 4)
                    state = ApplyCaptiveFreedomCurrent(state, current, pool, physical, date,
                        Day, Sampler, effects);
            }
            node = current.ReadRegistryNodeNext(node) ?? throw LastCityPeopleMissing("Person_next");
        }
        return state;

        TacticSourceSamplerWorld Sampler() => ReadArmyDestructionSampler(state, current)
            ?? throw LastCityPeopleMissing("freedom_World450_45C");
        int Day() => Sampler().Day450;
    }

    private GameState ResetOrdinaryLastCityCaptiveCurrent(GameState state,
        SourceWorldContext current, int person, List<CampaignEffectRequest> effects)
    {
        //240DB0(...0,0) keeps its actual2783B0 association/report callback.
        var assignments = new List<PersonAssignmentPrecallResult>();
        PersonDistrictTransfer.Apply(person, 0,
            ArmyOrdinaryReturnRuntime.CreateCurrentPersonDistrictTransfer(current, assignments,
                result => state = ApplyPersonAssociationRemovalReports(state, current, person, result,
                    () => (ReadArmyDestructionSampler(state, current)
                        ?? throw LastCityPeopleMissing("association_World450")).Day450, effects)));
        if (assignments.Count != 0)
            state = PersonAssignmentPrecallInputs.PublishDeathTouched(state, current, person, assignments);
        //165 is capture-reset knowledge;19D former-Force knowledge has no
        //store in this branch. Captivity, Home and actual position survive.
        current.ClearCaptureResetKnowledge(person);
        current.SetPersonDestructionLoyaltyE8(person, 0);
        byte loyalty = (LastCityPeopleNeed(current.ReadArmyReturnPersonFlags196(person), "captive_Person15E") & 0x2000) != 0
            ? (byte)255 : (byte)0;
        current.SetPersonDestructionLoyaltyE8(person, loyalty);
        state = ArmyDestructionLoyalty.PublishCurrentRawE8(state, current, person, loyalty);
        var ranks = _scenario.Snapshot.CreditRankParameters?.Ranks;
        if (ranks is not { Count: 7 }) throw LastCityPeopleMissing("captive_credit_rank_definitions");
        int initial = ranks[0].SignedThreshold;
        //22B1C0 precedes22B1E0; only the latter consumes SurrenderedRank.
        int upper = ranks[6].SignedThreshold;
        int selector = LastCityPeopleNeed(BattleRemakeCurrentSettings.ReadSelectorE58(state), "captive_settings_selector");
        int option = LastCityPeopleNeed(BattleRemakeCurrentSettings.ReadInt32(state,
            selector == 0 ? 0x4C4 : 0x128), "captive_SurrenderedRank");
        int lower = ranks[option == 1 ? 2 : 1].SignedThreshold;
        int merit = initial < lower ? lower : Math.Min(initial, upper);
        int id = LastCityPeopleNeed(current.ReadPersonIdentity(person), "captive_Person10");
        var officer = state.Officers.GetValueOrDefault(id) ?? throw LastCityPeopleMissing("captive_domain_owner");
        var location = current.ReadOfficerLocation(id) ?? throw LastCityPeopleMissing("captive_location_owner");
        state = state with { Officers = new(state.Officers) { [id] = officer with
        {
            ForceId = null, DistrictId = null,
            LocationState = location,
            MeritState = OfficerMeritRules.ResetAfterRelease(merit)
        } } };
        return current.PublishOfficerAftermath(state, id);
    }

    private static void RequireLastCityPeoplePool(SourceWorldContext current, WorldIndexContext pool)
    {
        if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
            throw LastCityPeopleMissing("shared_current_pool");
    }
    private static T LastCityPeopleNeed<T>(T? value, string field) where T : struct
        => value ?? throw LastCityPeopleMissing(field);
    private static InvalidDataException LastCityPeopleMissing(string field)
        => new("ordinary-last-City.people." + field);
}
