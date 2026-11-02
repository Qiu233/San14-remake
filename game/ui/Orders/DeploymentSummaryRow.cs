using System.Globalization;
using Godot;
using San14.Data;

internal sealed record DeploymentSummaryRowContent(int OfficerId,string ArmyName,int FormationId,int Troops,
    IReadOnlyList<string> TacticNames,LoadedUiSpriteSource? Portrait,DeploymentDraftFigures? Figures);

/// <summary>An original nonempty summary row. Values come from the unpublished
/// local draft and current figure queries, never from an invented Army.</summary>
internal partial class DeploymentSummaryRow : UiSourceButton
{
    private Action<int,DeploymentDraftFigures?,IReadOnlyList<string>?>? applyTroops;
    internal void ApplyTroops(int troops,DeploymentDraftFigures? figures,IReadOnlyList<string>? tacticNames=null)=>
        applyTroops?.Invoke(troops,figures,tacticNames);
    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,
        DeploymentSummaryRowContent content,Action selected,Action removed)
    {
        if (content.TacticNames.Count != 3) throw new ArgumentException("A summary owns exactly three tactic slots.",nameof(content));
        var source = new UiSourceLayout(repository,0xF2FBFA11);
        Size = new(716,116);
        var presets = MapTextPresets.Create();
        var states = new Control[5];
        for (int i=0;i<5;i++)
            states[i]=source.Art(this,3,source.Rectangle(3),assets,shader,
                commonSpriteOverride:$"000_cmn_btn_hanyo_l_{Math.Min(i,3)}");
        if (content.Portrait is { } portrait)
        {
            // 92F830 truncates the authored node17 scaled dimensions before
            // mounting Face4; the original portrait may extend beyond row X0.
            var face = new UiSourcePortrait {Name="SummaryPortrait",Position=new(-17,3),Size=new(158,164)};
            AddChild(face); face.Configure(repository,assets,shader,portrait);
        }
        source.Art(this,19,source.Rectangle(19),assets,shader);
        var labels = new List<(UiSourceText Text,MapFontProfile Profile)>();
        Label(20,content.ArmyName);
        Label(35,content.Troops.ToString(CultureInfo.InvariantCulture));
        for (int i=0;i<3;i++) Label(36+i,content.TacticNames[i]);
        void Label(int node,string value)
        {
            labels.Add((source.Text(this,node,default,value,font),MapFontProfile.FromNode(source.Nodes[node],source.Source.Source.ResourceUri)));
        }
        int formation = content.FormationId switch { >=1 and <=13 => content.FormationId,14=>15,>=17 and <=20=>14,_=>1 };
        var icons = new[]
        {
            source.Art(this,21,source.Rectangle(21),assets,shader,commonSpriteOverride:$"000_cmn_icon_jin_{formation}",
                expansionCommonSprite:formation==15),
            source.Art(this,34,source.Rectangle(34),assets,shader)
        };
        var markerParts=new Dictionary<int,Control>();
        {
            var palette = MapSpritePalettes.Create()[48];
            foreach (int node in new[]{23,24,25,26,27,28})
                markerParts[node]=source.Art(this,node,source.Rectangle(node),assets,shader,node==23?null:palette);
        }
        source.Art(this,30,source.Rectangle(30),assets,shader);
        var radarProfile=DeploymentRadarPresentation.ForLocale(font.Source.Locale);var max=radarProfile.Maximums;
        int[] maxima=[max.Mobility,max.FieldAttack,max.SiegeTroopAttack,max.WallAttack,max.Defense];
        var withRadar=Radar(31,"SummaryWithModifiers",radarProfile.SummaryWithModifiersPalette);
        var withoutRadar=Radar(32,"SummaryWithoutModifiers",radarProfile.SummaryWithoutModifiersPalette);
        source.Art(this,33,source.Rectangle(33),assets,shader);
        var close = new UiSourceFunctionButton {Name="RemoveSummary",Position=source.Rectangle(39).Position};
        AddChild(close); close.Configure(repository,assets,shader,"000_cmn_icon_close",removed);
        ConfigureStates(states,state=>
        {
            var preset = presets[3+Math.Min(state,3)];
            foreach (var (text,profile) in labels)
            { text.Configure(font,profile.WithPreset(preset)); text.QueueRedraw(); }
            var color = new MapCornerColors(preset.LT,preset.RT,preset.LB,preset.RB);
            foreach (var icon in icons)
                foreach (var part in icon.GetChildren().OfType<UiSourceSprite>()) part.SetColors(color);
        });
        Pressed += () => { if (!Disabled) selected(); };
        SetMeta("source_layout",source.Source.Source.ResourceUri);
        SetMeta("officer_id",content.OfficerId);
        SetMeta("portrait_known",content.Portrait is not null);
        applyTroops=(troops,figures,tacticNames)=>
        {
            if(tacticNames is not null)
            {
                if(tacticNames.Count!=3)throw new ArgumentException("A summary owns three tactic captions.",nameof(tacticNames));
                for(int index=0;index<3;index++)labels[index+2].Text.Text=tacticNames[index];
            }
            labels[1].Text.Text=troops.ToString(CultureInfo.InvariantCulture);
            var marker=figures?.TroopMarker;
            foreach(var (node,part) in markerParts)part.Visible=marker.HasValue&&(node<=24||
                node<=26&&marker!=DeploymentTroopMarker.BelowPrimaryThreshold||
                node>=27&&marker==DeploymentTroopMarker.AtSecondaryThreshold);
            withRadar.ApplyValues(Axes(figures?.WithConditionalModifiers),maxima);
            withoutRadar.ApplyValues(Axes(figures?.WithoutConditionalModifiers),maxima);
        };
        ApplyTroops(content.Troops,content.Figures);

        UiSourceRadarMesh Radar(int node,string name,int palette)
        {
            var mesh=new UiSourceRadarMesh {Name=name};AddChild(mesh);mesh.Configure(source,node,MapSpritePalettes.Create()[palette].LT);
            return mesh;
        }
        static int[]? Axes(DeploymentCombatAxes? axes)=>axes is null?null:
            [axes.Mobility,axes.FieldAttack,axes.SiegeTroopAttack,axes.WallAttack,axes.Defense];
    }
}
