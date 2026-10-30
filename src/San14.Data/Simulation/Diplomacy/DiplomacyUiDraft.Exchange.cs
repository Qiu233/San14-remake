using San14.Core;

namespace San14.Data;

public sealed partial class DiplomacyUiDraft
{
    public DiplomacyOffer RequestedOffer { get; private set; }=new(0,0);
    public bool? CanChooseRequestedGold { get; private set; }
    public bool? CanChooseRequestedSupplies { get; private set; }
    public bool? CanChooseRequestedItem { get; private set; }
    public bool? CanChooseRequestedArea { get; private set; }
    public string? RequestedOfferUnresolvedInput { get; private set; }
    public AreaAdministrationQuery<IReadOnlyList<DiplomacyPreparationGiftChoice>> RequestedItemChoices { get; private set; }=
        AreaAdministrationQuery<IReadOnlyList<DiplomacyPreparationGiftChoice>>.Known([]);
    public AreaAdministrationQuery<IReadOnlyList<DiplomacyPreparationGiftChoice>> RequestedAreaChoices { get; private set; }=
        AreaAdministrationQuery<IReadOnlyList<DiplomacyPreparationGiftChoice>>.Known([]);
    public AreaAdministrationQuery<DiplomacyNegotiationAmountSelection> RequestedGoldSelection { get; private set; }=UnopenedAmount();
    public AreaAdministrationQuery<DiplomacyNegotiationAmountSelection> RequestedSuppliesSelection { get; private set; }=UnopenedAmount();
    public AreaAdministrationQuery<DiplomacyNegotiationAmountSelection> OpenRequestedGoldSelection()=>
        RequestedGoldSelection=ReadRequestedAmount(false);
    public AreaAdministrationQuery<DiplomacyNegotiationAmountSelection> OpenRequestedSuppliesSelection()=>
        RequestedSuppliesSelection=ReadRequestedAmount(true);

    public void SelectRequestedGold(int gold)
    {
        var range=ReadRequestedAmount(false);
        if(range.Value is not {Allowed:true} value || gold<value.Minimum || gold>value.Maximum)
            throw new ArgumentOutOfRangeException(nameof(gold));
        RequestedOffer=new(gold,0);RefreshSubmission();RefreshAdvice();
    }
    public void SelectRequestedSupplies(int supplies)
    {
        var range=ReadRequestedAmount(true);
        if(range.Value is not {Allowed:true} value || supplies<value.Minimum || supplies>value.Maximum)
            throw new ArgumentOutOfRangeException(nameof(supplies));
        RequestedOffer=new(0,supplies);RefreshSubmission();RefreshAdvice();
    }
    public void SelectRequestedItem(int id)
    {
        RequireChoice(RequestedItemChoices,id,CanChooseRequestedItem);
        RequestedOffer=new(0,0,id);RefreshSubmission();RefreshAdvice();
    }
    public void SelectRequestedArea(int id)
    {
        RequireChoice(RequestedAreaChoices,id,CanChooseRequestedArea);
        RequestedOffer=new(0,0,null,id);RefreshSubmission();RefreshAdvice();
    }
    public void ResetRequested()
    {RequestedOffer=new(0,0);RefreshRequestedAdmission();RefreshSubmission();RefreshAdvice();}

    private void RefreshRequestedAdmission()
    {
        if(Kind!=DiplomaticMissionKind.Exchange)return;
        CanChooseRequestedGold=CanChooseRequestedSupplies=CanChooseRequestedItem=CanChooseRequestedArea=false;
        RequestedOfferUnresolvedInput=null;
        RequestedGoldSelection=RequestedSuppliesSelection=UnopenedAmount();
        if(SelectedTargetForceId is not int target)return;
        try
        {
            CanChooseGold=MaxGold>=1000 && RequestedOffer.Gold==0;
            CanChooseSupplies=MaxSupplies>=10000 && RequestedOffer.Supplies==0;
            CanChooseRequestedGold=GiftGold==0 && source.ExchangeResourceMaximum(target,false)>=1000;
            CanChooseRequestedSupplies=GiftSupplies==0 && source.ExchangeResourceMaximum(target,true)>=10000;
            RequestedItemChoices=source.ExchangeItems(target);RequestedAreaChoices=source.ExchangeAreas(target);
            CanChooseRequestedItem=RequestedItemChoices.Available?RequestedItemChoices.Value is {Count:>0}:null;
            CanChooseRequestedArea=RequestedAreaChoices.Available?RequestedAreaChoices.Value is {Count:>0}:null;
            RequestedOfferUnresolvedInput=RequestedItemChoices.UnresolvedInput??RequestedAreaChoices.UnresolvedInput;
        }
        catch(InvalidDataException error)
        {RequestedOfferUnresolvedInput=error.Message;CanChooseRequestedGold=CanChooseRequestedSupplies=null;}
    }
    private AreaAdministrationQuery<DiplomacyNegotiationAmountSelection> ReadRequestedAmount(bool supplies)
    {
        if(Kind!=DiplomaticMissionKind.Exchange || SelectedTargetForceId is not int target ||
            (supplies?CanChooseRequestedSupplies:CanChooseRequestedGold)!=true)
            return AreaAdministrationQuery<DiplomacyNegotiationAmountSelection>.Known(new(false,0,0,[]));
        try{return source.ExchangeAmount(target,supplies);}
        catch(InvalidDataException error){return AreaAdministrationQuery<DiplomacyNegotiationAmountSelection>.Unknown(error.Message);}
    }
}

internal sealed partial class DiplomacyUiSource
{
    private readonly Dictionary<(int Target,bool Supplies),int> exchangeResourceMaxima=[];
    private readonly Dictionary<int,AreaAdministrationQuery<IReadOnlyList<DiplomacyPreparationGiftChoice>>> exchangeItems=[],exchangeAreas=[];
    internal int ExchangeResourceMaximum(int target,bool supplies)
    {
        if(exchangeResourceMaxima.TryGetValue((target,supplies),out int saved))return saved;
        return exchangeResourceMaxima[(target,supplies)]=adapter.ReadExchangeRequestedResourceMaximumCurrent(State,current,target,supplies);
    }
    internal AreaAdministrationQuery<IReadOnlyList<DiplomacyPreparationGiftChoice>> ExchangeItems(int target)
    {
        if(exchangeItems.TryGetValue(target,out var saved))return saved;
        try{return exchangeItems[target]=AreaAdministrationQuery<IReadOnlyList<DiplomacyPreparationGiftChoice>>.Known(
            adapter.ReadExchangeRequestedItemChoicesCurrent(State,current,ForceId,target));}
        catch(InvalidDataException error){return exchangeItems[target]=AreaAdministrationQuery<IReadOnlyList<DiplomacyPreparationGiftChoice>>.Unknown(error.Message);}
    }
    internal AreaAdministrationQuery<IReadOnlyList<DiplomacyPreparationGiftChoice>> ExchangeAreas(int target)
    {
        if(exchangeAreas.TryGetValue(target,out var saved))return saved;
        try{return exchangeAreas[target]=AreaAdministrationQuery<IReadOnlyList<DiplomacyPreparationGiftChoice>>.Known(
            adapter.ReadExchangeRequestedAreaChoicesCurrent(State,current,ForceId,target));}
        catch(InvalidDataException error){return exchangeAreas[target]=AreaAdministrationQuery<IReadOnlyList<DiplomacyPreparationGiftChoice>>.Unknown(error.Message);}
    }
    internal AreaAdministrationQuery<DiplomacyNegotiationAmountSelection> ExchangeAmount(int target,bool supplies)
    {
        int step=supplies?10000:1000;
        int maximum=ExchangeResourceMaximum(target,supplies);
        //74A640/7488D0 kind19 open an integer calculator.
        //1000/10000 are minima, not quantization steps; there are no presets.
        return AreaAdministrationQuery<DiplomacyNegotiationAmountSelection>.Known(new(maximum>=step,step,maximum,[]));
    }
    internal DiplomacyAdvisorResult ExchangeAdvice(int actor,int target,DiplomacyOffer given,DiplomacyOffer requested)
    {
        var result=adapter.ReadExchangeAdvisorCurrent(State,current,ForceId,actor,target,given,requested);
        State=result.State;return result;
    }
}
