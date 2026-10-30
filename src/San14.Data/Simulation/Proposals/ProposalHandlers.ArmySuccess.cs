using San14.Core;

namespace San14.Data;

internal sealed partial class ProposalLifecycleSource
{
    private int ProposalDifficulty() => K(BattleRemakeCurrentSettings.ReadInt32(State,
        K(BattleRemakeCurrentSettings.ReadSelectorE58(State), "difficulty_selector") == 0 ? 0x134 : 0x28), "difficulty");

    // Each row remains the native five-operand trait predicate, interpreted by
    // the shared personality evaluator. The score is stored with the offer;
    // adopting the offer never rerolls this pair's trial.
    private int ProposalArmySuccessScore(ProposalKind kind, int proposer, ArmyUnitReference army)
    {
        int difficulty = ProposalDifficulty();
        if (difficulty == 0) return 1000;
        int commander = ProposalArmyCommander(army);
        int intelligence = PersonAbility(proposer, 2);
        int bonus = difficulty == 1 ? 150 : 0;
        int Own(params int[] instructions) => PersonalityExpression(proposer, instructions);
        int Other(params int[] instructions) => PersonalityExpression(commander, instructions);
        if (kind == ProposalKind.BefriendAlly)
        {
            var runtime = ProposalArmyImage(K(army.PhysicalSlot, "success_army")).NativeTacticRuntime ?? throw Missing("success_runtime");
            byte[] active = [runtime.Abnormal23, runtime.Abnormal24, runtime.Abnormal25];
            int score = unchecked((((intelligence + PersonAbility(commander, 0)) / 200) * 2 - active.Count(x => x != 0) + 5) * 100 +
                Own(98, 0, 0, 0, 400, 65, 0, 0, 0, 200, 50, 0, 0, 0, 100, 9, 1, 0, 0, 400) +
                Other(9, 1, 0, 0, 300, 9, -1, 1, 0, -100) + bonus);
            for (int index = 0; index < active.Length; index++)
                if (active[index] != 0)
                    score = unchecked(score + Own(10 + index, 1, 0, 0, 150) +
                        Other(10 + index, -1, 1, 0, -100, 10 + index, 1, 0, 0, 100));
            return score;
        }
        if (kind == ProposalKind.ExtinguishFriendlyFire)
            return unchecked(((intelligence + PersonAbility(commander, 0)) / 200) * 300 + 400 + bonus +
                Own(98, 0, 0, 0, 200, 65, 0, 0, 0, 100, 50, 0, 0, 0, 50,
                    25, 0, 0, 0, 2000, 26, 1, 0, 0, 400, 102, 0, 0, 0, 400) +
                Other(98, 0, 0, 0, 200, 25, 0, 0, 0, 2000, 26, 1, 0, 0, 200,
                    56, 0, 0, 0, -200, 102, 0, 0, 0, 400));
        int defenderIntelligence = PersonAbility(commander, 2);
        int disadvantage = kind == ProposalKind.ConfuseEnemy ? -300 : -400;
        int difference = intelligence < defenderIntelligence
            ? unchecked((defenderIntelligence - intelligence) / intelligence * disadvantage)
            : unchecked((intelligence - defenderIntelligence) / intelligence * 100);
        if (kind == ProposalKind.BurnEnemy)
            return unchecked(400 + difference + bonus +
                Own(98, 0, 0, 0, 200, 65, 0, 0, 0, 100, 50, 0, 0, 0, 50,
                    25, 0, 0, 0, 2000, 26, 1, 0, 0, 400) +
                Other(98, 0, 0, 0, -200, 26, -1, 1, 0, -200, 56, 0, 0, 0, 200));
        bool confusion = kind == ProposalKind.ConfuseEnemy;
        int baseScore = confusion ? 300 : 400;
        int specialEffect = confusion ? 10 : 11;
        int secondary = confusion ? 150 : 200;
        return unchecked(baseScore + difference + bonus +
            Own(98, 0, 0, 0, baseScore, 65, 0, 0, 0, secondary, 50, 0, 0, 0, secondary / 2,
                9, -1, 0, 10, baseScore, specialEffect, -1, 0, 10, secondary) +
            Other(9, 1, 0, 0, -baseScore, 9, -1, 1, 0, secondary,
                specialEffect, 1, 0, 0, secondary, specialEffect, -1, 1, 0, -secondary));
    }

    private void AdjustArmyMorale(int physical, int adjustment)
    {
        var effects = new List<CampaignEffectRequest>();
        var people = new HashSet<int>();
        var applied = ArmyMoraleZeroRoutingCurrent.ApplyImmediate(State, Current, ProposalArmy(physical), adjustment,
            Package.TrapMobility(), Immediate);
        State = applied.State ?? throw Missing(applied.UnresolvedOwner ?? "morale_application");
        PublishPeople();
        if (effects.Count != 0)
        {
            var requests = State.CampaignEffectRequests ?? throw Missing("morale_effect_owner");
            long revision = State.NativeMapSeason?.AdvancedCalendarSteps ?? throw Missing("morale_calendar_revision");
            State = State with { CampaignEffectRequests = requests.Append(CampaignEffectSource.ArmyBattleDefeat, revision, effects) };
        }

        GameState Immediate(GameState value, UnitDefeatRequest request)
        {
            State = value;
            PublishPeople();
            if (Current.ReadOwnedPersonCommandManager() is null) PersonReturnCommands.BindCurrent(State, Current);
            State = State with { PersonnelCommands = Current.ReadOwnedPersonCommandManager() ?? throw Missing("morale_personnel_manager") };
            var pool = Current.ReadOwnedCurrentListPool() ?? throw Missing("morale_list_pool");
            Package.BindActorlessDestructionInputs(State, Current);
            State = new OwnedArmyDestructionStage(Package).ApplyBattleDefeatCurrent(State, Current, pool, request,
                Package.ReadArmyDestructionSampler(State, Current) ?? throw Missing("morale_sampler"),
                State.NativeMapSeason?.ThirdDate ?? throw Missing("morale_date"), effects,
                receiver => effects.Add(new(null, null) { ArmyTraitViewRefresh = new(ArmyDestructionQueue.ResolveUnitId(State, receiver.PhysicalSlot) ?? throw Missing("morale_refresh_army")) }),
                person => people.Add(person));
            PublishPeople();
            return State;
        }
        void PublishPeople()
        {
            if (people.Count == 0) return;
            var owners = new Dictionary<int, OfficerState>(State.Officers);
            foreach (int person in people)
            {
                int id = PersonIdentity(person);
                var owner = owners.GetValueOrDefault(id) ?? throw Missing("morale_person_owner");
                var activation = OfficerTraitActivationSourceAdapter.Read(Current, person);
                if (activation is not { DurationSlotCount: 9, ActiveSlots: not null }) throw Missing("morale_person_activation");
                owners[id] = owner with { TraitActivation = activation };
            }
            State = State with { Officers = owners };
            people.Clear();
        }
    }
}
