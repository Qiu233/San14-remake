using San14.Core;

namespace San14.Data;

internal sealed record TalentRecruitJoinResult(GameState State, int NativeReturn);

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState ApplyRecruitInitialLoyaltyCurrent(GameState state,
        SourceWorldContext current, int personPhysical, TransferPersonReference ruler)
    {
        var pool = current.ReadOwnedCurrentListPool() ?? throw RecruitMissing("initial_loyalty_current_pool");
        return new PersonAppearanceSource(this, state, current, pool).ApplyInitialLoyaltyCurrent(personPhysical, ruler);
    }

    //271C20 with param4=0,param5=0,param7=1, as called by Recruit/Search.
    //Reached unsupported special owners remain explicit unresolved branches.
    //The caller retains the live current/pool and publishes it once at its tail.
    internal TalentRecruitJoinResult ApplyRecruitJoinCurrent(GameState input,
        SourceWorldContext current, int personPhysical, DistrictReference destinationDistrict,
        BaseReference destinationHome, NativeMapSeasonDate sourceDate,
        Func<int> readDay450, List<CampaignEffectRequest> sink)
    {
        T Need<T>(T? value, string field) where T : struct => value ?? throw RecruitMissing("join_" + field);
        if (Seed.Language != "TC") throw RecruitMissing("join_SC_owner");
        if (!Need(current.ReadPersonNativeValidity(personPhysical), "Person_validity")) return new(input, 0);
        byte originalStatus = Need(current.ReadPersonStatus(personPhysical), "PersonE6");
        if (originalStatus == 1) return new(input, 0);
        //The real old-home lookup precedes232460's serving-person branch.
        _ = BaseLookup.Find(Need(current.ReadPersonFirstLocation(personPhysical), "old_HomeE2"), current);
        var working = input;
        if (originalStatus is >= 2 and <= 4)
            working = ApplyRecruitServingPrefixCurrent(working, current, personPhysical,
                destinationDistrict, destinationHome, sourceDate, readDay450, sink);
        var oldForce = current.ReadPersonForce(personPhysical) ?? throw RecruitMissing("join_old_Force22CB40");
        byte rawOldDistrict = Need(current.ReadPersonDistrictSlot(personPhysical), "old_DistrictE0");
        var oldDistrict = current.ReadDistrictTableReference(rawOldDistrict < 52 ? rawOldDistrict : 0)
            ?? throw RecruitMissing("join_old_District_pointer");
        int districtPhysical = Need(destinationDistrict.PhysicalSlot, "destination_District_pointer");
        byte rawNewForce = Need(current.ReadDistrictForceId(districtPhysical), "destination_District10");
        var newForce = current.ReadForceTableReference(rawNewForce <= 51 ? rawNewForce : 0)
            ?? throw RecruitMissing("join_destination_Force_table");
        int forcePhysical = Need(current.ReadForcePhysicalSlot(newForce), "destination_Force60");
        ushort rulerId = Need(current.ReadMoraleWaveRuler22EAC0(newForce), "destination_Force_ruler22EAC0");
        var aftermath = current.ReadOfficerAftermath(personPhysical) ?? throw RecruitMissing("join_resentment_owner");
        int resentment = aftermath.Resentment?.TargetOfficerId ?? aftermath.SourceKnowledge?.UnmappedResentmentTarget
            ?? throw RecruitMissing("join_resentment162");
        if (resentment == rulerId)
        {
            current.SetOfficerAftermath(personPhysical, aftermath with
            {
                Resentment = new(0, 0),
                SourceKnowledge = aftermath.SourceKnowledge is { } residue ? residue with { UnmappedResentmentTarget = null } : null
            });
            working = current.PublishOfficerAftermath(working, Need(current.ReadPersonIdentity(personPhysical), "Person10"));
        }
        if (Need(current.ReadForceNativeValidity(oldForce), "old_Force_validity"))
        {
            ushort rawRuler = Need(current.ReadForceRecord(Need(current.ReadForcePhysicalSlot(oldForce), "old_Force60"))?.RawRuler10, "old_Force_ruler10");
            var before = current.ReadOfficerAftermath(personPhysical) ?? throw RecruitMissing("join_old_resentment_owner");
            int oldRuler = Math.Clamp((int)rawRuler, 0, 6000);
            //Raw identity may have no owned Officer. Preserve it in the
            //existing residue, rather than inventing an Officer reference.
            bool mapped = working.Officers.ContainsKey(oldRuler);
            current.SetOfficerAftermath(personPhysical, before with
            {
                Resentment = mapped ? new(oldRuler, 12) : new(null, 12),
                SourceKnowledge = (before.SourceKnowledge ?? new(Need(current.ReadPersonIdentity(personPhysical), "resentment_ID"), null, null, null)) with
                { UnmappedResentmentTarget = mapped ? null : oldRuler }
            });
            working = current.PublishOfficerAftermath(working, Need(current.ReadPersonIdentity(personPhysical), "resentment_Person10"));
            int office = Need(current.ReadMoraleWavePersonPostE7(personPhysical), "join_office_E7");
            int rank = Need(OwnedArmyMoraleWaveInputs.ReadPost37(working, office), "join_office37");
            int penalty = Math.Clamp(unchecked(TalentRecruitParameters.Read(working, "join-diplomacy-office-base") - rank), 0, 10);
            working = ForceRelationChangeCurrent.ApplyWithoutPresentation(working, current, forcePhysical,
                Need(current.ReadForcePhysicalSlot(oldForce), "relation_old_Force60"), -penalty,
                key => working.NativeArmySimulation?.Parameters.Int32Values.TryGetValue(key, out int value) == true
                    ? value : throw RecruitMissing("join_relation_parameter_" + key));
        }
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(working, current)
            ?? throw RecruitMissing("join_player_Force");
        int playerPhysical = Need(current.ReadForcePhysicalSlot(player), "player_Force60");
        if (playerPhysical != 0 && Need(current.ReadForcePhysicalSlot(oldForce), "old_Force60") == playerPhysical)
            working = ApplyRecruitOldForceHistoryCurrent(working, current, personPhysical,
                oldForce, destinationDistrict, readDay450, sink);

        int stage = 2;
        if (Need(current.ReadPersonStatus(personPhysical), "credit_statusE6") == 6)
        {
            int selector = Need(BattleRemakeCurrentSettings.ReadSelectorE58(working), "credit_selector");
            if (Need(BattleRemakeCurrentSettings.ReadInt32(working, selector == 0 ? 0x4C4 : 0x128), "surrender_rank_option") != 1) stage = 1;
        }
        var ranks = _scenario.Snapshot.CreditRankParameters?.Ranks;
        if (ranks is not { Count: 7 }) throw RecruitMissing("join_credit_rank_thresholds");
        int credit = ranks[stage].SignedThreshold;
        int upper = ranks[6].SignedThreshold;
        int rankSelector = Need(BattleRemakeCurrentSettings.ReadSelectorE58(working), "credit_bound_selector");
        int rankOption = Need(BattleRemakeCurrentSettings.ReadInt32(working, rankSelector == 0 ? 0x4C4 : 0x128), "credit_lower_option");
        int lower = ranks[rankOption == 1 ? 2 : 1].SignedThreshold;
        int bounded = lower <= credit ? Math.Min(credit, upper) : lower;
        ushort identity = Need(current.ReadPersonIdentity(personPhysical), "credit_Person10");
        if (current.ResolveOfficerSource(identity) != personPhysical || !working.Officers.TryGetValue(identity, out var owner))
            throw RecruitMissing("join_owned_Person");
        //Native1C4/1BC/1BE/1C0/1C2 are credit channels here. They must not
        //be written through the separately modeled trait-mask1BC interface.
        working = working with { Officers = new(working.Officers) { [identity] = owner with
        { MeritState = new(unchecked((short)bounded), 0, stage, 0, 0, 0) } } };
        current.WritePersonStatus(personPhysical, 4);
        owner = working.Officers[identity];
        working = working with { Officers = new(working.Officers) { [identity] = owner with
        { ServiceStatus = OfficerServiceStatus.ServingOfficer, Role = OfficerRole.Officer } } };
        var associations = new List<PersonAssignmentPrecallResult>();
        var transfer = ArmyOrdinaryReturnRuntime.CreateCurrentPersonDistrictTransfer(current, associations, result =>
        {
            working = ApplyPersonAssociationRemovalReports(working, current, personPhysical, result, readDay450, sink);
        });
        PersonDistrictTransfer.Apply(personPhysical, districtPhysical, transfer);
        if (associations.Count != 0)
            working = PersonAssignmentPrecallInputs.PublishDeathTouched(working, current, personPhysical, associations);
        ushort destination = (ushort)Math.Min(Need(current.ReadBaseObjectLink(destinationHome), "destination_Base4E"), (ushort)61);
        current.WritePersonFirstLocation(personPhysical, destination);
        current.SetMoraleWavePersonPostE7(personPhysical, 73);
        working = PublishPerson(working);
        ushort freshRulerId = Need(current.ReadForceRecord(forcePhysical)?.RawRuler10, "late_destination_Force_ruler10");
        var ruler = (freshRulerId <= 6000 ? current.ReadPersonLookupReference(freshRulerId) : current.ReadTransferPersonTableReference(0))
            ?? throw RecruitMissing("join_destination_ruler_lookup");
        working = ApplyRecruitInitialLoyaltyCurrent(working, current, personPhysical, ruler);
        if (working.NativeArmySimulation is not { } native ||
            native.Parameters.Int32Values.GetValueOrDefault(OwnedArmyGovernorComparatorInputs.VersionKey) != 1)
            throw RecruitMissing("join_year166_saved_owner");
        var values = new Dictionary<string, int>(native.Parameters.Int32Values)
        { [OwnedArmyGovernorComparatorInputs.PersonWord166Key(personPhysical)] = Math.Clamp((int)sourceDate.Year, 0, 350) };
        working = working with { NativeArmySimulation = native with { Parameters = native.Parameters with { Int32Values = values } } };
        current.WriteFormerForceKnowledge(personPhysical, 0);
        ushort flags = Need(current.ReadArmyReturnPersonFlags196(personPhysical), "final_flags15E");
        current.WriteArmyReturnPersonFlags196(personPhysical, (ushort)((flags & 0xFFFE) | 0x80));
        working = PublishPerson(working);
        working = current.PublishOfficerAftermath(working, identity);
        if (Need(current.ReadDistrictNativeValidity(oldDistrict), "old_District_validity"))
        {
            var districtQueries = CreateCaptureDistrictOperationQueries(current, () => working,
                next => working = next, sourceDate, sink);
            ArmyReturnCommanderHomeUpdater.ApplyPrimaryOwned(oldDistrict, current, districtQueries);
            working = OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
            working = PersonReturnDistrictReorganizationSource.PublishCurrent(working, current, districtQueries);
            working = PersonReturnDistrictTransferSource.PublishAreaAssignments(working, current, [], districtQueries.ChangedGates);
        }
        return new(working, 1);

        GameState PublishPerson(GameState state)
        {
            ushort id = Need(current.ReadPersonIdentity(personPhysical), "publish_Person10");
            var original = state.Officers.GetValueOrDefault(id) ?? throw RecruitMissing("join_publish_person");
            if (state.NativeArmySimulation is not { } simulation ||
                simulation.Parameters.Int32Values.GetValueOrDefault(OwnedArmyLifecycleDetachedInputs.VersionKey) != 1)
                throw RecruitMissing("join_publish_detached_owner");
            byte district = Need(current.ReadPersonDistrictSlot(personPhysical), "publish_E0");
            var force = current.ReadPersonForce(personPhysical) ?? throw RecruitMissing("join_publish_Force");
            int forceId = Need(current.ReadForcePhysicalSlot(force), "publish_Force60");
            ushort home = Need(current.ReadPersonFirstLocation(personPhysical), "publish_E2");
            var fields = new Dictionary<string, int>(simulation.Parameters.Int32Values)
            {
                [OwnedArmyLifecycleDetachedInputs.Key("Person", personPhysical, "E0")] = district,
                [OwnedArmyLifecycleDetachedInputs.Key("Person", personPhysical, "E2")] = home
            };
            var published = state with { Officers = new(state.Officers) { [id] = original with
            {
                ForceId = forceId == 0 ? null : forceId, DistrictId = district == 0 ? null : district,
                LocationId = home == 0 ? null : home, LocationState = current.ReadOfficerLocation(id)
                    ?? throw RecruitMissing("join_publish_location"),
                Appointment = OfficerAppointment.Synchronize(73), Behavior = current.ReadPersonBehavior(personPhysical)
            } }, NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = fields } } };
            return OfficerLocationSourceAdapter.PublishPosition(published, id, current);
        }
    }
}
