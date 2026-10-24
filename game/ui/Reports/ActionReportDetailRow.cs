using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>The presenter resolves the current person name and portrait source,
/// including the original invalid-person portrait. The immutable log remains
/// the identity passed back to the report consumer.</summary>
internal sealed record ActionReportDetailRowContent(CampaignHistoryRecord Record,
    string PersonName,LoadedSprite Portrait);

/// <summary>The original detailed action-report row. It consumes a prepared
/// log snapshot and emits row events; map/details navigation belongs to its page.</summary>
internal partial class ActionReportDetailRow : Control
{
    private GameResourceRepository repository=null!;
    private UiAssets assets=null!;
    private Shader shader=null!;
    private UiSourceLayout source=null!;
    private string locale="";
    private UiSourceText personName=null!;
    private UiSourceParagraph body=null!;
    private UiSourceFunctionButton function=null!;
    private UiSourcePortrait? portrait;
    private LoadedSprite? portraitSource;
    private Action<ActionReportDetailRow,int>? dispatch;
    private int commandId;

    internal CampaignHistoryRecord? Record {get;private set;}
    internal string? GuideCaption {get;private set;}

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,
        MapFont font,string locale,Action<ActionReportDetailRow,int> dispatch)
    {
        this.repository=repository;this.assets=assets;this.shader=shader;
        this.locale=locale;this.dispatch=dispatch;
        source=new UiSourceLayout(repository,0x79D1D49E);
        Size=new(source.Nodes[0].Value44,source.Nodes[0].Value46);
        MouseFilter=MouseFilterEnum.Ignore;
        source.Art(this,1,source.Rectangle(1),assets,shader);
        source.Art(this,8,source.Rectangle(8),assets,shader);
        personName=source.Text(this,7,default,"",font);
        var box=source.Rectangle(9);
        var profile=MapFontProfile.FromNode(source.Nodes[9],source.Source.Source.ResourceUri)
            with {WidthFitMode=3,RawCharacterMode=2};
        body=new UiSourceParagraph{Name="ReportSupplementalText",Position=box.Position,Size=box.Size};
        AddChild(body);body.Configure(font,profile);
        function=new UiSourceFunctionButton{Name="ReportRowFunction",Position=source.Rectangle(10).Position};
        AddChild(function);
        function.Configure(repository,assets,shader,"000_cmn_icon_opt",()=>
        {
            if(function.Visible) this.dispatch?.Invoke(this,commandId);
        });
        // The cursor, pad placeholder and guide groups (2/12/15) are hidden by
        // the original initializer. A later input-guide owner is still separate.
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }

    internal void Apply(ActionReportDetailRowContent? content)
    {
        Record=content?.Record;
        // The source null binding changes the record pointer but leaves the
        // already mounted artwork intact. It does not synthesize an empty log.
        if(content is null)return;
        byte flags=content.Record.Flags;
        bool map=(flags&0x10)!=0,details=(flags&0x40)!=0;
        if(map||details)
        {
            commandId=map?0:1;
            function.SetIcon(map?"000_cmn_icon_map_mode":"000_cmn_icon_info");
            GuideCaption=repository.LoadMessages(locale,MessageKind.Data).Messages.Sections[1]
                .Records[map?0x329:0x26].Text??throw new InvalidDataException("Missing report row guide caption.");
        }
        function.Visible=map||details;
        var window=content.Record.SupplementalText;
        int length=window.IndexOf(0);
        if(length<0)length=window.Length;
        body.Apply(new string(window.Take(length).Select(code=>(char)code).ToArray()));
        personName.Text=content.PersonName;
        if(!ReferenceEquals(portraitSource,content.Portrait))
        {
            var node=source.Nodes[5];
            var replacement=new UiSourcePortrait{Name="ReportPersonPortrait",Position=source.Rectangle(5).Position,
                Size=new((int)(node.Value44*node.HeaderValues[7]),(int)(node.Value46*node.HeaderValues[8]))};
            AddChild(replacement);replacement.ConfigureFace2(repository,assets,shader,content.Portrait);
            MoveChild(replacement,personName.GetIndex());
            if(portrait is not null){RemoveChild(portrait);portrait.QueueFree();}
            portrait=replacement;portraitSource=content.Portrait;
        }
    }

    public override void _ExitTree()=>dispatch=null;
}
