using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class DiplomacyUiDraft
{
    public IReadOnlyList<DiplomacyCaptiveChoice> CaptiveChoices { get; private set; }=[];
    public string? CaptiveChoicesUnresolvedInput { get; private set; }
    public IReadOnlyList<int> SelectedCaptiveOfficerIds { get; private set; }=[];
    public int? CaptiveRansomGold { get; private set; }

    public void SelectCaptives(IReadOnlyList<int> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if(Kind!=DiplomaticMissionKind.ReturnCaptives || SelectedTargetForceId is null ||
            ids.Count>3 || ids.Distinct().Count()!=ids.Count || ids.Any(id=>!CaptiveChoices.Any(row=>row.OfficerId==id)))
            throw new ArgumentOutOfRangeException(nameof(ids));
        //75DAD0 preserves an existing nonempty selection when the actual
        //confirmed output is empty. Keep that output distinct from the saved
        //parent selection for74E4D0's subsequent automatic-executor gate.
        var confirmed=ids.ToImmutableArray();
        if(SelectedCaptiveOfficerIds.Count==0 || confirmed.Length!=0)
        {
            SelectedCaptiveOfficerIds=confirmed;
            int gold=0;
            foreach(int id in confirmed)gold=unchecked(gold+CaptiveChoices.First(row=>row.OfficerId==id).RansomGold);
            CaptiveRansomGold=gold;GiftGold=gold;GiftSupplies=0;GiftItemId=GiftAreaId=null;
        }
        int? preferred=confirmed.Length==0?null:source.PreferredExecutor(SelectedTargetForceId.Value,
            AllianceMonths,SelectedTargetCityId,new(GiftGold,GiftSupplies,GiftItemId,GiftAreaId),RequestedOffer,
            SelectedCaptiveOfficerIds);
        if(preferred is int actor)
        {
            //Even the same actor has undergone this confirmation's complete
            //6B97D0 batch. Do not use SelectExecutor's same-ID early return.
            SelectedExecutorOfficerId=actor;SourceCityId=source.Home(actor);
        }
        else if(SelectedExecutorOfficerId is int previous && !source.ExecutorNativeValid(previous))
        {SelectedExecutorOfficerId=null;SourceCityId=null;}
        RefreshGiftAdmission();RefreshTravel();RefreshSubmission();RefreshAdvice();
    }

    private void RefreshCaptiveChoices()
    {
        if(Kind!=DiplomaticMissionKind.ReturnCaptives)return;
        CaptiveChoices=[];CaptiveChoicesUnresolvedInput=null;SelectedCaptiveOfficerIds=[];CaptiveRansomGold=null;
        GiftGold=GiftSupplies=0;GiftItemId=GiftAreaId=null;
        if(SelectedTargetForceId is not int target)return;
        try { CaptiveChoices=source.Captives(target); }
        catch(InvalidDataException error){CaptiveChoicesUnresolvedInput=error.Message;}
    }
}

internal sealed partial class DiplomacyUiSource
{
    private readonly Dictionary<int,IReadOnlyList<DiplomacyCaptiveChoice>> captiveChoices=[];
    private readonly Dictionary<int,int> captiveCounts=[];
    internal int CaptiveCount(int target)
    {
        if(captiveCounts.TryGetValue(target,out int count))return count;
        return captiveCounts[target]=adapter.ReadDiplomacyCaptiveCountCurrent(current,ForceId,target);
    }
    internal IReadOnlyList<DiplomacyCaptiveChoice> Captives(int target)
    {
        if(captiveChoices.TryGetValue(target,out var saved))return saved;
        return captiveChoices[target]=adapter.ReadDiplomacyCaptivesCurrent(State,current,ForceId,target);
    }
}
