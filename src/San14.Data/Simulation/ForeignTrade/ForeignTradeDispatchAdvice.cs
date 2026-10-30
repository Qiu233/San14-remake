using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //The final1F4630 call to2F39D0(kind17) has country and zero salts.
    //Its formatted text can consume App32 even though the caller discards
    //the text. Only an accuracy100 adviser preserves the source trial.
    private TalentAdvisorSampleResult PrepareForeignTradeDispatchAdviceCurrent(GameState state,
        SourceWorldContext current, int forceId, DispatchForeignTradeOrder order)
    {
        var force = current.ReadForceTableReference(forceId) ?? throw ForeignTradeCommandMissing("advice_Force");
        ushort rawAdvisor = ForeignTradeNeed(current.ReadAssignmentForceAdvisor52(forceId), "advice_Force52");
        var reference = rawAdvisor <= 6000 ? current.ReadPersonLookupReference(rawAdvisor) : current.ReadTransferPersonTableReference(0);
        int advisor = ForeignTradeNeed(reference?.PhysicalSlot, "advice_advisor_pointer");
        if (!ForeignTradeNeed(current.ReadPersonNativeValidity(advisor), "advice_advisor_validity")) return new(state, -1);
        int advisorId = ForeignTradeNeed(current.ReadPersonIdentity(advisor), "advice_advisor_identity");
        var abilities = SearchAbilities(current);
        int intelligence = ForeignTradeNeed(abilities.ResolveDimension(state, advisorId, 2,
            (owner, effect) => _baseCapacityInputs.ReadPanelModifier(state, owner, effect)), "advice_advisor_Intelligence");
        int accuracy = Math.Clamp(unchecked(60 + Math.Max(0, intelligence - 70) + Math.Max(0, intelligence - 90)), 0, 100);
        var sampler = ReadArmyDestructionSampler(state, current) ?? throw ForeignTradeCommandMissing("advice_sampler");
        bool accurate = TacticEffectSourceSamplerMath.Sample(100, 10, order.CountryId, 0, 0, 0, sampler) < accuracy;
        int trial = ResolveForeignTradeTrialCurrent(state, current, order.ExecutorOfficerIds, order.CountryId,
            order.Gold, order.Supplies, order.Troops, order.ItemId ?? 0);
        int root = (trial != 0) == accurate ? 0x3366 : 0x3371;
        var formatted = FormatForeignTradeMessage(state, current, root, new(Person0: advisorId,
            Person1: TalentRulerCurrent(current, force), Person2: order.ExecutorOfficerIds[0],
            String0: ReadForeignTradeCountry(order.CountryId).Name, Integers: [order.ExecutorOfficerIds.Length]));
        return new(formatted.State, accuracy == 100 ? trial : -1)
            { AdviceText = formatted.Text, AdvisorOfficerId = advisorId, PositiveAdvice = root == 0x3366 };
    }
}
