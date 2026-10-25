using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //1745B0 returns0 for the full manager140. Its259360 call shares this
    //pair's current and leaves the full queue itself completely unchanged.
    internal GameState ApplyNormalCombatImmediateDefeatCurrent(GameState state, SourceWorldContext current,
        UnitDefeatRequest request, TacticSourceSamplerWorld sampler, NativeMapSeasonDate date,
        long revision, List<CampaignEffectRequest> precedingEffects)
    {
        state = FlushNormalCombatEffects(state, revision, precedingEffects);
        var pool = current.ReadOwnedCurrentListPool() ?? throw ImmediateCombatMissing("shared_pool");
        if (current.ReadOwnedPersonCommandManager() is null)
        {
            if (state.PersonnelCommands is null) throw ImmediateCombatMissing("personnel_owner");
            PersonReturnCommands.BindCurrent(state, current);
        }
        state = state with { PersonnelCommands = current.ReadOwnedPersonCommandManager()! };
        BindActorlessDestructionInputs(state, current);
        var effects = new List<CampaignEffectRequest>();
        var changed = new HashSet<int>();
        var result = new OwnedArmyDestructionStage(this).ApplyBattleDefeatCurrent(state, current, pool,
            request, sampler, date, effects, Refresh, person => changed.Add(person), allowPlayerRulerPause: true);
        if (changed.Count != 0)
        {
            var people = new Dictionary<int, OfficerState>(result.Officers);
            foreach (int physical in changed)
            {
                int id = current.ReadPersonIdentity(physical) ?? throw ImmediateCombatMissing("changed_Person10");
                var person = people.GetValueOrDefault(id) ?? throw ImmediateCombatMissing("changed_Person_owner");
                var activation = OfficerTraitActivationSourceAdapter.Read(current, physical);
                if (activation is not { DurationSlotCount: 9, ActiveSlots: not null })
                    throw ImmediateCombatMissing("changed_trait_activation");
                people[id] = person with { TraitActivation = activation };
            }
            result = result with { Officers = people };
        }
        return AppendImmediateCombatEffects(result, revision, effects);

        void Refresh(ArmyUnitReference army) => effects.Add(new(null, null)
        { ArmyTraitViewRefresh = new(ArmyDestructionQueue.ResolveUnitId(state, army.PhysicalSlot)
            ?? throw ImmediateCombatMissing("trait_Army_identity")) });
    }

    private GameState ResumeNormalCombatImmediateDefeatCurrent(GameState state, SourceWorldContext current,
        NativeMapSeasonDate date, long revision)
    {
        var pool = current.ReadOwnedCurrentListPool() ?? throw ImmediateCombatMissing("resume_pool");
        var effects = new List<CampaignEffectRequest>();
        state = new OwnedArmyDestructionStage(this).ResumeImmediateDefeatCurrent(state, current, pool, date, effects);
        return AppendImmediateCombatEffects(state, revision, effects);
    }

    private static GameState AppendImmediateCombatEffects(GameState state, long revision,
        List<CampaignEffectRequest> effects) => effects.Count == 0 ? state : state with
        { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw ImmediateCombatMissing("effect_owner"))
            .Append(CampaignEffectSource.ArmyBattleDefeat, revision, effects) };

    private static GameState FlushNormalCombatEffects(GameState state, long revision,
        List<CampaignEffectRequest> effects)
    {
        if (effects.Count == 0) return state;
        state = state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw ImmediateCombatMissing("effect_owner"))
            .Append(CampaignEffectSource.NormalCombatApplication, revision, effects) };
        effects.Clear();
        return state;
    }

    private static InvalidDataException ImmediateCombatMissing(string owner) => new("normal-combat.immediate." + owner);
}
