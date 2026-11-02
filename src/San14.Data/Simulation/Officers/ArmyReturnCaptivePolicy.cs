using San14.Core;

namespace San14.Data;

public enum ArmyReturnCaptiveTreatment { Keep, Recruit, Release, Execute }

/// <summary>Policies select an intention. The return operation owns admission,
/// the current receiver and all writes; a policy cannot supply a receipt.</summary>
public sealed record ArmyReturnCaptivePolicyContext(GameState State, int CaptiveOfficerId,
    int CaptorForceId, int CaptorRulerOfficerId, bool GuardShortage,
    Func<ArmyReturnCaptiveTreatment> OriginalDecision);
public interface IArmyReturnCaptivePolicy
{
    ArmyReturnCaptiveTreatment Select(ArmyReturnCaptivePolicyContext context);
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Optional replaceable disposition policy. Null uses the original
    /// ordinary return decision; both paths use the same treatment kernel.</summary>
    public IArmyReturnCaptivePolicy? ArmyReturnCaptivePolicy { get; init; }

    internal ArmyReturnCaptiveTreatment SelectReturnCaptiveCurrent(GameState state,
        SourceWorldContext current, int person, int ruler, ForceReference captor,
        bool shortage, ushort commander, Func<TacticSourceSamplerWorld> sampler)
    {
        int Id(int p) => CaptiveNeed(current.ReadPersonIdentity(p), "Person10");
        ArmyReturnCaptiveTreatment Original() => ReadOriginalReturnCaptiveDecisionCurrent(
            state, current, person, ruler, captor, shortage, commander, sampler);
        return ArmyReturnCaptivePolicy?.Select(new(state, Id(person),
            CaptiveNeed(captor.PhysicalSlot, "captor_Force"), Id(ruler), shortage, Original)) ?? Original();
    }

    //2F8F00's original nonplayer mode0 policy. It queries278900 before
    //guard shortage and retains the distinct hostility and numeric gates.
    private ArmyReturnCaptiveTreatment ReadOriginalReturnCaptiveDecisionCurrent(GameState state,
        SourceWorldContext current, int person, int ruler, ForceReference captor,
        bool shortage, ushort commander, Func<TacticSourceSamplerWorld> sampler)
    {
        int P(string name) => CaptiveTreatmentParameters.Read(state, name);
        int Id(int p) => CaptiveNeed(current.ReadPersonIdentity(p), "policy_Person10");
        short W(int p, int field) => CaptiveNeed(RulerCaptureReleaseInputs.ReadCurrentWord(state, p, field), "policy_word_" + field.ToString("X"));
        bool Valid(int p) => CaptiveNeed(current.ReadPersonNativeValidity(p), "policy_validity");
        ushort R(int p, int field) => CaptiveNeed(OwnedPersonRelationInputs.ReadCurrentRelationIdentity(state, p, field), "policy_relation_" + field.ToString("X"));
        bool List(int a, int b, int field) => OfficeRelationList(state, current, a, b, field);
        bool Kin(int a, int b, int field)
        {
            if (!Valid(a) || !Valid(b) || Id(a) == Id(b)) return false;
            ushort value = R(a, field);
            return value != 0 && value == (field == 0xBC ? R(b, field) : Id(b));
        }
        int Relation()
        {
            int other = CaptiveNeed(current.ReadPersonOldForceSlot(person), "policy_original_Force");
            if (other >= 52) return 0;
            int owner = CaptiveNeed(captor.PhysicalSlot, "policy_captor_Force");
            if (current.TryReadRulerReleaseRelation13(owner, other, out var value)) return CaptiveNeed(value, "policy_Force13");
            return state.NativeArmySimulation?.Forces.GetValueOrDefault(owner)?.NormalAftermath?.Relations13 is { Length: 52 } bytes
                ? bytes[other] : throw CaptiveMissing("policy_Force13_owner");
        }
        int AbilityMaximum()
        {
            var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
            int maximum = 0;
            for (int dimension = 0; dimension < 5; dimension++)
            {
                int value = CaptiveNeed(abilities.ResolveDimension(state, Id(person), dimension,
                    (f, effect) => _baseCapacityInputs.ReadPanelModifier(state, f, effect)), "policy_current_ability");
                if (maximum < value) maximum = value;
            }
            return maximum;
        }
        bool Protected() => CaptiveNeed(OfficerBehaviorRules.MustReleaseAfterAiCapture(current.ReadPersonBehavior(person)), "policy_bit9");
        int Frequency() => CaptiveNeed(BattleRemakeCurrentSettings.ReadBattleDeathFrequency(state), "policy_death_frequency");
        bool Advanced() => CaptiveNeed(BattleRemakeCurrentSettings.ReadAdvancedSettings(state), "policy_advanced");
        int selector = CaptiveNeed(BattleRemakeCurrentSettings.ReadSelectorE58(state), "policy_selector");
        bool hostilityGate = selector != 0 ? Frequency() != 3 && !Protected()
            : Advanced() && CaptiveNeed(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 1), "policy_advanced_execution") > 0 && !Protected();
        if (hostilityGate && (List(ruler, person, 0xD0) || Relation() < P("hostility-relation-floor") || HistoricalHostility()))
            return ArmyReturnCaptiveTreatment.Execute;
        if (TryRecruitCurrent(state, current, person, ruler, -1, sampler).Accepted)
            return ArmyReturnCaptiveTreatment.Recruit;
        if (shortage) return ArmyReturnCaptiveTreatment.Release;
        if (Frequency() != 3 && Relation() <= P("execution-relation-limit") &&
            !Kin(person, ruler, 0xBC) && !List(ruler, person, 0xC0) && !Protected() &&
            !Kin(ruler, person, 0xB4) && !Kin(ruler, person, 0xB6) && !Kin(ruler, person, 0xBA) &&
            (!PersonKinshipCurrent.Read(state, current, person, ruler) || List(ruler, person, 0xD0)) &&
            (W(ruler, 0x176) < P("execution-ruler-tendency-floor") ||
             unchecked(AbilityMaximum() + W(ruler, 0x176)) < P("execution-ability-and-ruler-limit")))
        {
            int ability = Math.Clamp(AbilityMaximum(), 30, 100);
            //231B50(...0) is an unused pure sum query; it writes no Person field.
            int female = state.Officers.GetValueOrDefault(Id(person))?.Sex switch
            { OfficerSex.Female => P("execution-female-reduction"), OfficerSex.Male => 0, _ => throw CaptiveMissing("policy_sexA9") };
            int addend = unchecked(P("execution-ability-rate-addend") - ability / 2 - female);
            int threshold = Math.Clamp(unchecked(P(Frequency() == 2 ? "execution-frequent-base-rate" : "execution-base-rate") + addend), 0, 15);
            if (Advanced())
                threshold = Math.Clamp(unchecked(CaptiveNeed(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 1), "policy_advanced_rate") *
                    unchecked(P("execution-base-rate") + addend)) / 100, 0, 15);
            int sample = TacticEffectSourceSamplerMath.Sample(100, 10, Id(person), commander, 0, 0, sampler());
            if (sample < threshold) return ArmyReturnCaptiveTreatment.Execute;
        }
        return ArmyReturnCaptiveTreatment.Keep;

        bool HistoricalHostility()
        {
            int id = Id(person), king = Id(ruler);
            if (id == 987 && king == 521 || id == 736 && king is 521 or 35 or 551 ||
                id == 521 && king is 736 or 987 || id == 35 && king == 736 || id == 932 && king == 266) return true;
            const ulong family = 0x210000000000001;
            int royal = unchecked((ushort)(king - 607));
            if (id == 132 && royal < 58 && ((family >> royal) & 1) != 0) return true;
            int victim = unchecked((ushort)(id - 607));
            return victim < 58 && ((family >> victim) & 1) != 0 && W(ruler, 0x17A) >= 0;
        }
    }
    private static T CaptiveNeed<T>(T? value, string field) where T : struct => value ?? throw CaptiveMissing(field);
    private static InvalidDataException CaptiveMissing(string field) => new("return.captive-treatment." + field);
}
