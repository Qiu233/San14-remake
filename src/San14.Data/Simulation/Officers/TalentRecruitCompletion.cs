using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //The manager has already run the real22-hour/remaining-day and both
    //continuation guards. It retires this original task only after success.
    internal GameState CompleteTalentRecruitCurrent(GameState state, SourceWorldContext current,
        PersonnelTalentTask task, NativeMapSeasonDate date, List<CampaignEffectRequest> effects)
    {
        int executor = current.ResolveOfficerSource(task.MemberIds[0]) ?? throw TalentCommandMissing("Employ_executor");
        int target = current.ResolveOfficerSource(task.TargetOfficerId ?? throw TalentCommandMissing("Employ_target_ID"))
            ?? throw TalentCommandMissing("Employ_target");
        int executorId = TalentNeed(current.ReadPersonIdentity(executor), "Employ_executor_ID");
        int targetId = TalentNeed(current.ReadPersonIdentity(target), "Employ_target_ID");
        var district = current.ReadDistrictTableReference(TalentNeed(current.ReadPersonDistrictSlot(executor), "Employ_DistrictE0"))
            ?? throw TalentCommandMissing("Employ_District_pointer");
        var home = BaseLookup.Find(TalentNeed(current.ReadPersonFirstLocation(executor), "Employ_homeE2"), current);
        var force = current.ReadPersonForce(executor) ?? throw TalentCommandMissing("Employ_Force");
        int rulerId = TalentRulerCurrent(current, force);
        var ruler = (rulerId <= 6000 ? current.ReadPersonLookupReference(rulerId) : current.ReadTransferPersonTableReference(0))
            ?? throw TalentCommandMissing("Employ_ruler_pointer");
        var targetHome = BaseLookup.Find(task.TargetOriginalHomeLocationId ?? throw TalentCommandMissing("Employ_target_home7C"), current);
        if (!task.IsRecruitWait && task.RecruitMode > 0 &&
            TalentRecruitTargetStillAwayCurrent(current, target, targetHome))
            return StartTalentRecruitWaitCurrent(state, current, task, date, executor, target, targetHome, effects);

        var working = state;
        bool primary = TalentDistrictIsPlayerRulerCurrent(working, current, district);
        if (primary)
        {
            effects.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.SoundCue, 8, null, null, null, null) });
            var notice = FormatTalentReportCurrent(working, current, 0xC3B5, new(Person0: targetId, Person1: executorId));
            working = notice.State;
            effects.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.Announcement, null, null, null, null, notice.Text) });
        }
        TacticSourceSamplerWorld Sampler() => ReadArmyDestructionSampler(working, current) ?? throw TalentCommandMissing("Employ_sampler");
        var trial = TryRecruitCurrent(working, current, target, executor, task.SavedSample, Sampler);
        var targetForce = current.ReadPersonForce(target) ?? throw TalentCommandMissing("Employ_target_Force");
        bool executorPlayer = TalentForceIsPlayerCurrent(working, current, force);
        bool targetPlayer = TalentForceIsPlayerCurrent(working, current, targetForce);
        if (primary && !targetPlayer)
            working = AppendTalentRecruitConversationCurrent(working, current, executor, target, trial.Accepted, effects);
        else if (trial.Accepted && !executorPlayer)
        {
            var targetDistrict = current.ReadDistrictTableReference(TalentNeed(current.ReadPersonDistrictSlot(target), "target_DistrictE0"))
                ?? throw TalentCommandMissing("target_District_pointer");
            if (TalentDistrictIsPlayerRulerCurrent(working, current, targetDistrict))
            {
                effects.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.SoundCue, 6, null, null, null, null) });
                var notice = FormatTalentReportCurrent(working, current, 0xC3F3, new(Person0: targetId,
                    Force0: TalentNeed(current.ReadForcePhysicalSlot(current.ReadPersonForce(executor)
                        ?? throw TalentCommandMissing("Employ_fresh_Force")), "Employ_Force60")));
                working = notice.State;
                effects.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.OfficerText, null, 5012, null, null, notice.Text) });
            }
        }
        if (executorPlayer || trial.Accepted && targetPlayer)
            working = AppendTalentSearchHistoryCurrent(working, current, task, date, executor,
                trial.Accepted ? 0xC3F1 : 0xC3F2, trial.Accepted ? 0x51CE : 0x51D8,
                new(Person0: executorId, Person1: rulerId, Person2: targetId), effects,
                1, trial.Accepted ? 1 : 0, mainOperands: new(Person0: executorId, Person1: targetId));
        working = ApplyTalentCommonRewardsCurrent(working, current, executor, 2, trial.Accepted ? 1 : 2);
        int[] members = task.MemberIds;
        if (trial.Accepted)
        {
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
            int count = 0;
            for (int dimension = 0; dimension < 5; dimension++)
                if (TalentNeed(abilities.ResolveDimension(working, targetId, dimension,
                    (f, effect) => _baseCapacityInputs.ReadPanelModifier(working, f, effect)), "target_effective_ability") > 80) count++;
            int bonus = unchecked(TalentRecruitParameters.Read(working, "employ-success-ability-count-scale") * count);
            int merit = unchecked(unchecked(bonus + TalentRecruitParameters.Read(working, "employ-success-merit-base")) * 10);
            working = ApplyTalentPendingMeritCurrent(working, current, executor, merit);
            var joined = ApplyRecruitJoinCurrent(working, current, target, district, home, date, () => Sampler().Day450, effects);
            working = joined.State;
            //271C20 has its own first2EE160. This is the caller's actual
            //second call, including its original extra query ordering.
            working = ApplyRecruitInitialLoyaltyCurrent(working, current, target, ruler);
            members = members.Append(targetId).ToArray();
            var owner = current.ReadOwnedPersonCommandManager() ?? throw TalentCommandMissing("Employ_member_manager");
            owner = owner with { Talents = new(owner.Talents!) { [task.Id] = task with { MemberIds = members } } };
            current.SetOwnedPersonCommandManager(owner);
            current.SetLinkedEntityMemberIds(task.Id, members.Select(id => (uint?)id).ToArray());
            working = working with { PersonnelCommands = owner };
        }
        return ScheduleTalentCompletionReturnCurrent(working, current, task.Id, members, date, executorPlayer);
    }

    internal GameState ApplyTalentCommonRewardsCurrent(GameState state, SourceWorldContext current,
        int executorPhysical, int actionKind, int outcomeKind)
    {
        if (!TalentNeed(current.ReadPersonNativeValidity(executorPhysical), "reward_Person_validity")) return state;
        ushort home = TalentNeed(current.ReadPersonFirstLocation(executorPhysical), "reward_homeE2");
        if (home is >= 1 and <= 61 && ForceId() is >= 46 and <= 50) return state;
        int id = TalentNeed(current.ReadPersonIdentity(executorPhysical), "reward_Person10");
        bool specialBand = false;
        for (int i = 0; i < 5; i++) if (unchecked((uint)(id - (4111 + i * 20))) <= 9) { specialBand = true; break; }
        if (specialBand && TalentNeed(current.ReadPersonFirstLocation(executorPhysical), "reward_special_E2") is >= 1 and <= 61 && ForceId() is not (>= 46 and <= 50)) return state;
        if (outcomeKind is < 1 or > 6) return state;
        int dimension;
        string key;
        if (actionKind == 2) { dimension = 4; key = outcomeKind == 2 ? "employ-failure-growth" : "employ-success-growth"; }
        else if (actionKind == 3) { dimension = 3; key = outcomeKind == 2 ? "search-failure-growth" : "search-success-growth"; }
        else if (actionKind is 4 or 5 or 6 or 9)
        {
            dimension = 3;
            string prefix = actionKind switch { 4 => "goodwill", 5 => "alliance", 6 => "cancel", _ => "attack" };
            key = prefix + (outcomeKind == 2 ? "-failure-growth" : "-success-growth");
        }
        else throw TalentCommandMissing("reward_action_" + actionKind);
        int xp = actionKind is 2 or 3 ? TalentRecruitParameters.Read(state, key) :
            actionKind == 6 && outcomeKind == 2 ? 0 : ReadDiplomacyCommandRewardParameter(state, key);
        int merit = actionKind switch
        {
            4 when outcomeKind != 2 => unchecked(ReadDiplomacyCommandRewardParameter(state, "goodwill-success-merit") * 10),
            5 when outcomeKind != 2 => unchecked(ReadDiplomacyCommandRewardParameter(state, "alliance-success-merit") * 10),
            6 => unchecked(ReadDiplomacyCommandRewardParameter(state, "cancel-command-merit") * 10),
            9 when outcomeKind != 2 => unchecked(ReadDiplomacyCommandRewardParameter(state, "attack-success-merit") * 10),
            _ => 0
        };
        var working = state;
        //2B9810 initializes the remaining four dimension slots to zero, not
        //the sentinel5. Even requested0 can award trait75's minimum1.
        for (int i = 0; i < 5; i++)
        {
            var growth = _growthSource.TryApplyWithinArmyOperation(working, current, _growthQueries,
                id, i == 0 ? dimension : 0, i == 0 ? xp : 0, 0);
            if (growth.Outcome == PersonGrowthOutcome.CurrentInputsUnresolved)
                throw TalentCommandMissing("reward_2BA120_current_growth");
            working = growth.State;
        }
        return ApplyTalentPendingMeritCurrent(working, current, executorPhysical, merit);

        int ForceId() => TalentNeed(current.ReadForcePhysicalSlot(current.ReadPersonForce(executorPhysical)
            ?? throw TalentCommandMissing("reward_Force")), "reward_Force60");
    }

    internal GameState ApplyTalentPendingMeritCurrent(GameState state, SourceWorldContext current,
        int executorPhysical, int amount)
    {
        var merit = _pendingMerit.TryApplyWithinArmyOperation(state, current,
            TalentNeed(current.ReadPersonIdentity(executorPhysical), "merit_Person10"), amount);
        return merit.Outcome == PersonPendingMeritOutcome.CurrentInputsUnresolved
            ? throw TalentCommandMissing("1695C0_current_pending_merit") : merit.State;
    }

    internal GameState ScheduleTalentCompletionReturnCurrent(GameState state, SourceWorldContext current,
        int taskId, int[] members, NativeMapSeasonDate date, bool isPlayer)
        => PersonReturnCommands.ScheduleTalentCompletionReturnCurrent(state, current, taskId, members, date, isPlayer);

    private bool TalentDistrictIsPlayerRulerCurrent(GameState state, SourceWorldContext current, DistrictReference district)
    {
        int physical = TalentNeed(district.PhysicalSlot, "District_pointer");
        if (!TalentNeed(current.ReadDistrictNativeValidity(physical), "District_validity")) return false;
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw TalentCommandMissing("District_player_Force");
        if (!TalentNeed(current.ReadForceNativeValidity(player), "District_player_validity")) return false;
        if (TalentNeed(current.ReadDistrictForceId(physical), "District10") != TalentNeed(current.ReadForcePhysicalSlot(player), "District_player60")) return false;
        ushort leader = TalentNeed(current.ReadDistrictLeader(physical), "District12");
        var person = (leader <= 6000 ? current.ReadPersonLookupReference(leader) : current.ReadTransferPersonTableReference(0))
            ?? throw TalentCommandMissing("District_leader_lookup");
        int at = TalentNeed(person.PhysicalSlot, "District_leader_pointer");
        return TalentNeed(current.ReadPersonNativeValidity(at), "District_leader_validity") &&
            TalentNeed(current.ReadPersonStatus(at), "District_leader_E6") == 1;
    }
}

public static partial class PersonReturnCommands
{
    //2247F0: the original command remains associated while1F68B0 resolves
    //each member's real current position. Only the outer manager retires it.
    internal static GameState ScheduleTalentCompletionReturnCurrent(GameState state,
        SourceWorldContext current, int taskId, int[] members, NativeMapSeasonDate date, bool isPlayer)
    {
        var result = RescheduleEligibleMembers(state, current, members, isPlayer, date);
        return state with { PersonnelCommands = result.Owner };
    }
}
