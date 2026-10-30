using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private StrategicSelectionPage ReadStrategicSelectorCurrent(StrategicCommandRules rules,
        StrategicSelectionRole role,int? target,int? second,StrategicCommandPageSelection selection)
    {
        bool executor=role==StrategicSelectionRole.Executor;
        IReadOnlyList<StrategicCommandTargetChoice> choices=executor?
            selection.Executors.Select(row=>new StrategicCommandTargetChoice(row.OfficerId,row.Name,
                null,null,null,row.Enabled,row.DisabledReason,row.UnresolvedOwner)).ToArray():
            role==StrategicSelectionRole.SecondForce?selection.SecondaryForces:selection.Targets;
        var eligibility=choices.ToDictionary(row=>row.Id);
        int[] ids=choices.Select(row=>row.Id).ToArray();
        int title=executor?0x58F:role==StrategicSelectionRole.SecondForce?0x590:0x58D;
        var page=new StrategicSelectionPage(rules.Kind,role,title,StrategicSelectorGuide(role,rules.Kind),eligibility);
        if(executor||rules.Kind is 12 or 24)
        {
            IReadOnlyDictionary<int,StrategicCommandCandidate>? temporary=null;
            if(executor)
            {
                // Refresh already prepared this exact key and source order.
                // Rendering/sorting must not rerun recommendation or App advice.
                if(!rules.ExecutorViews.TryGetValue((target,second),out var prepared))
                    throw StrategyMissing("selection.executor_preparation");
                temporary=prepared.ToDictionary(row=>row.Id);
            }
            var reader=new OfficerInformationCurrent(this,rules.State,rules.ForceId,
                rowPaletteEnabled:executor,retainedCurrent:rules.Current);
            var publication=reader.StrategicPages(ids,rules.Kind,executor,temporary);
            return page with{Officers=publication,AlteredHealth=reader.StrategicHealth(ids)};
        }
        var map=CreateStrategicTargetMapQueryCurrent(rules.State,rules.Current,rules.ForceId,
            rules.Kind,role==StrategicSelectionRole.SecondForce,choices);
        if(rules.Kind==10)
            return page with{Forces=new ForceInformationCurrent(this,rules.State,rules.ForceId,rules.Current).StrategicPages(ids),MapSelection=map};
        if(rules.Kind==11)
            return page with{Cities=new CityInformationCurrent(this,rules.State,rules.ForceId,rules.Current).StrategicPages(ids),MapSelection=map};
        var areas=new AreaInformationCurrent(this,rules.State,rules.ForceId,rules.Current);
        var areaPages=areas.StrategicPages(ids);
        // The Area selector applies its leading terrain header once on entry.
        var initialSort=areas.SortStrategicTerrain(areaPages,ids,true);
        if(!initialSort.Available)
            throw new InvalidDataException(initialSort.UnresolvedInput??"strategic.selection.initial_terrain_sort");
        return page with{Areas=areaPages,LeadingAreaIcons=areas.StrategicIcons(ids),
            SortLeadingAreaIcon=ascending=>areas.SortStrategicTerrain(areaPages,ids,ascending),MapSelection=map};
    }

    private string StrategicSelectorGuide(StrategicSelectionRole role,int kind)
    {
        var messages=new NarrationResourceMessages(_repository.LoadMessages(Seed.Language,Formats.MessageKind.Game).Messages);
        if(role==StrategicSelectionRole.Executor||kind is 12 or 24)
            return messages.ResolvePersonSelectorHint(ReadHistoryDataText(1,0x1FD));
        int message=kind switch
        {
            10=>role==StrategicSelectionRole.SecondForce?0x5A3F:0x5A3E,
            11=>0x5A5A,13=>0x5A4F,14=>0x5A49,20=>0x5C04,
            _=>throw StrategyMissing("selection.guide_kind")
        };
        return messages.ResolveStrategicSelectionGuide(message);
    }
}
