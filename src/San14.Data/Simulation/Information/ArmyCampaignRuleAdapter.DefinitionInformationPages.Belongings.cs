using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public AreaAdministrationQuery<BelongingsInformationPage> PrepareBelongingsInformationPages(GameState state,int playerForceId)
    {
        ArgumentNullException.ThrowIfNull(state);
        try{return AreaAdministrationQuery<BelongingsInformationPage>.Known(new DefinitionInformationCurrent(this,state,playerForceId).BelongingsPages());}
        catch(InvalidDataException error){return AreaAdministrationQuery<BelongingsInformationPage>.Unknown(error.Message);}
    }
    private sealed partial class DefinitionInformationCurrent
    {
        private PersonnelLearningBookInformation? belongingsGems;
        internal BelongingsInformationPage BelongingsPages()
        {
            // Both retained lists borrow this same current. Their candidate
            // notices and mutable selection/order stay independent.
            candidateIssues.Clear();
            var items=Pages(InformationListKind.Items,BelongingsItemCandidates(),selectInitial:false);
            candidateIssues.Clear();
            var gems=Pages(InformationListKind.GemBooks,BelongingsGemCandidates(),selectInitial:false);
            return new(Data(1,BelongingsInformationColumnsSource.TitleCaptionId),
                Data(1,BelongingsInformationColumnsSource.ItemTabCaptionId),Data(1,BelongingsInformationColumnsSource.GemTabCaptionId),items,gems);
        }
        private PreparedCell BelongingsGemCell(int id,int column)
        {
            belongingsGems??=new(rules,state,current);
            var display=belongingsGems.ReadBelongingsCell(id,column);
            return new(new(display.Text,display.PresetIndex,display.UnresolvedOwner)
                {MarkerColour=display.MarkerColour,PresetUnresolvedOwner=display.PresetUnresolvedOwner},
                new BelongingsGemKey(belongingsGems,id,column),belongingsGems.ReadBelongingsSortIssue(id,column));
        }
        private sealed record BelongingsGemKey(PersonnelLearningBookInformation Reader,int Id,int Column):SortKey
        {
            internal override bool Before(SortKey other,bool ascending)=>
                Reader.BeforeBelongings(Id,((BelongingsGemKey)other).Id,Column,ascending);
        }
    }
}
