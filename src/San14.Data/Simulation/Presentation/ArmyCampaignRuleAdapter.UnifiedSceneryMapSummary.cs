using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal MapSummaryPage QueryUnifiedSceneryMapSummaryCurrent(GameState state, SourceWorldContext current, MapSummaryReceiver receiver)
    {
        var original = ReadSceneryMapSummaryCurrent(state,current,receiver.ReceiverId);
        var text = original.Text.Where(pair=>pair.Key!=30).ToDictionary(pair=>pair.Key,pair=>pair.Value);
        var visibility = new Dictionary<int,bool>();
        var colors = new Dictionary<int,MapCornerColors>();
        var images = new List<MapSummaryImage>();
        var missing = new Dictionary<int,string>();
        foreach(var caption in SceneryMapSummarySources.Captions) text[caption.Node]=ReadHistoryDataText(1,caption.Message);
        var presets=SceneryMapSummarySources.Captions.ToDictionary(c=>c.Node,_=>13);
        if(original.MaximumLevel is bool maximum) visibility[30]=maximum;
        else {visibility[30]=false;missing[30]="map-summary.Scenery_level";}
        if(original.EffectVisible is bool effect) visibility[34]=effect;
        else visibility[34]=false;
        if(original.EffectKind is int kind && SceneryInvestmentSources.EffectSprite(kind) is string sprite)
            images.Add(new(34,sprite));
        if(original.ImageIndex is int image) images.Add(new(14,SceneryInvestmentSources.ImageSprite(image),SceneryInvestmentSources.ImagePack(image)));
        else {visibility[14]=false;missing[14]="map-summary.Scenery_illustration";}
        if(original.ForceColorIndex is int index)
        {
            var fill=MapSpritePalettes.ForceFill(index); colors[10]=new(fill,fill,fill,fill);
            var letter=SummaryChildPalettes.ForceLetter(index);colors[11]=new(letter,letter,letter,letter);
        }
        else {visibility[10]=visibility[11]=false;missing[10]="map-summary.Scenery_Force_color";}
        foreach(int node in new[]{11,12,18,22,28,36,40}) if(!text.ContainsKey(node))visibility[node]=false;
        var gauges=new List<MapSummaryGauge>();
        if(original.ProgressCurrent is int progress && original.ProgressMaximum is int maximumProgress) gauges.Add(new(26,25,progress,maximumProgress));
        else visibility[26]=false;
        return new(receiver.Slot,receiver.ReceiverId,SceneryMapSummarySources.LayoutName,original.ReceiverValid,text)
        {TextPresets=presets,Visibility=visibility,Colors=colors,Images=images,Gauges=gauges,UnresolvedFields=missing,UnresolvedOwner=original.UnresolvedOwner};
    }
}
