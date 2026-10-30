using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //1F5570's real dispatch tail. This is not the preparation page's cached
    //advice and is not a second independent trial used only to fill a field.
    internal (GameState State,int Outcome) ReadDiplomacyAdvisorOutcomeAtDispatchCurrent(
        GameState state,SourceWorldContext current,PrepareDiplomaticOrder order)
    {
        if(order.Kind==DiplomaticMissionKind.DemandSurrender)
            return ReadDemandSurrenderAdvisorOutcomeAtDispatchCurrent(state,current,order);
        if(order.Kind==DiplomaticMissionKind.Exchange)
            return ReadExchangeAdvisorOutcomeAtDispatchCurrent(state,current,order);
        var advice=ReadDiplomacyAdvisorCoreCurrent(state,current,order.ForceId,order.ExecutorOfficerId,
            order.TargetForceId,order.Kind,order.AllianceMonths,order.TargetCityId,
            new(order.GiftGold,order.GiftSupplies,order.GiftItemId,order.GiftAreaId),order.RequestedCaptiveOfficerIds,out int outcome);
        return(advice.State,outcome);
    }

    private GameState CompleteExchangeDiplomacyCurrent(GameState state,SourceWorldContext current,
        PersonnelDiplomacyTask task,bool accepted,NativeMapSeasonDate date,List<CampaignEffectRequest> effects,
        DiplomacyOffer? finalSenderOffer=null)
    {
        state=ApplyDiplomacyNumericCurrent(state,current,task,finalSenderOffer??task.InitialOffer,accepted,effects,date);
        state=WriteDiplomacyHistoryCurrent(state,current,task,accepted,date,effects);
        int actor=DiplomacyLookupPerson(current,task.MemberIds[0]);
        state=ApplyTalentCommonRewardsCurrent(state,current,actor,19,accepted?1:2);
        state=SetDiplomacyTaskCurrent(state,current,task with {AwaitingNegotiation=false});
        return FinishDiplomacyReturnCurrent(state,current,task,date);
    }

    private void RequireExchangeCounterOfferCurrent(GameState state,SourceWorldContext current,
        PersonnelDiplomacyTask task,DiplomacyOffer offer)
    {
        RequireExchangeSingleCondition(offer);
        RequireDiplomacyItemOfferCurrent(state,current,task,offer);
        RequireDiplomacyAreaOfferCurrent(state,current,task,offer);
        if(offer.Gold!=0 && task.RequestedOffer.Gold!=0 ||
            offer.Supplies!=0 && task.RequestedOffer.Supplies!=0)
            throw new ArgumentException("An exchange cannot request and provide the same currency.");
        if(offer.Gold!=0 || offer.Supplies!=0)
        {
            bool supplies=offer.Supplies!=0;
            var range=ReadDiplomacyNegotiationAmountCurrent(state,current,task,offer,supplies,task.TargetForceId);
            if(!range.Available)throw DiplomacyMissing(range.UnresolvedInput??"exchange_reply_amount");
            int value=supplies?offer.Supplies:offer.Gold;
            if(range.Value is not {Allowed:true} selected || value<selected.Minimum || value>selected.Maximum)
                throw new ArgumentException("The exchange sender payment is outside the available treasury.");
        }
    }

    private GameState CompleteDirectDiplomacyCurrent(GameState state,SourceWorldContext current,
        PersonnelDiplomacyTask task,bool accepted,NativeMapSeasonDate date,List<CampaignEffectRequest> effects)
        =>task.NativeKind switch
        {
            7=>CompleteDemandSurrenderCurrent(state,current,task,accepted,date,effects),
            19=>CompleteExchangeDiplomacyCurrent(state,current,task,accepted,date,effects),
            _=>throw DiplomacyMissing("direct_settlement_kind")
        };
}
