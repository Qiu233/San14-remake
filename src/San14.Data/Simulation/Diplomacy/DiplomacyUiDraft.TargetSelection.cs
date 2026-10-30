using San14.Core;
namespace San14.Data;
public sealed partial class DiplomacyUiDraft
{
    public DiplomacyTargetSelectionPage ReadTargetSelection(DiplomacyTargetSelectionKind kind,bool requested=false)
    {
        IReadOnlyList<int> ids;
        if(kind==DiplomacyTargetSelectionKind.Force)ids=TargetForceIds;
        else if(kind==DiplomacyTargetSelectionKind.City)ids=TargetCityIds;
        else
        {
            var query=kind==DiplomacyTargetSelectionKind.Item?
                requested?RequestedItemChoices:ItemChoices:
                kind==DiplomacyTargetSelectionKind.Area?requested?RequestedAreaChoices:AreaChoices:
                throw new ArgumentOutOfRangeException(nameof(kind));
            if(query.Value is not { } choices)throw new InvalidDataException(query.UnresolvedInput);
            ids=choices.Select(row=>row.Id).ToArray();
        }
        return source.TargetSelection(kind,ids);
    }
}
internal sealed partial class DiplomacyUiSource
{
    internal DiplomacyTargetSelectionPage TargetSelection(DiplomacyTargetSelectionKind kind,IReadOnlyList<int> ids)=>
        adapter.ReadDiplomacyTargetSelectionCurrent(State,current,ForceId,kind,ids,Kind==DiplomaticMissionKind.ReturnCaptives);
}
public sealed partial class DiplomacyNegotiationDraft
{
    public DiplomacyTargetSelectionPage ReadTargetSelection(DiplomacyTargetSelectionKind kind)
    {
        var query=kind switch
        {DiplomacyTargetSelectionKind.Item=>ItemChoices,DiplomacyTargetSelectionKind.Area=>AreaChoices,
            _=>throw new ArgumentOutOfRangeException(nameof(kind))};
        if(query.Value is not { } choices)throw new InvalidDataException(query.UnresolvedInput);
        return source.TargetSelection(kind,choices.Select(row=>row.Id).ToArray());
    }
}
