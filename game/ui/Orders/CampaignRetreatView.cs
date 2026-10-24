using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record CampaignRetreatChoice(int Id,string Caption,string Detail,bool Enabled,
    bool Selected,string? DisabledReason);

/// <summary>Multi-selection of the actual active campaign expeditions.
/// Disabled withdrawing campaigns remain visible with their real explanation.</summary>
internal partial class CampaignRetreatView : Control
{
    private AdministrationPageChrome chrome = null!;
    private Control rows = null!;
    private UiSourceScrollbar scroll = null!;
    private readonly List<(int Id,UiSourcePersonRow Row)> entries = [];
    private readonly List<int> selected = [];
    private Func<bool>? admitted;
    private Action? returned;
    private Action<IReadOnlyList<int>>? accepted;
    private bool abandoned,busy;
    internal IReadOnlyList<int> SelectedIds => selected.AsReadOnly();

    internal void Configure(GameResourceRepository repository,PreviewText text,
        IReadOnlyList<CampaignRetreatChoice> choices,string guide,Func<bool> admitted,
        Action returned,Action<IReadOnlyList<int>> accepted,Func<int> scrollSpeed)
    {
        this.admitted=admitted;this.returned=returned;this.accepted=accepted;
        Name="CampaignAllRetreat";ZIndex=3;MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome=new();AddChild(chrome);
        chrome.Configure(repository,text,0x059E,RequestBack,()=>
            {if(ReadAdmission()&&selected.Count>0)this.accepted?.Invoke(selected.ToArray());});
        chrome.ApplyDefaultHint(guide);
        var source=new UiSourceLayout(repository,0xA3B4A82E);
        source.Blur(chrome.Canvas,2,source.Rectangle(2,new(191,147)));
        source.Art(chrome.Canvas,3,source.Rectangle(3,new(191,147)),chrome.Assets,chrome.SpriteShader);
        rows=new(){Name="CampaignRows",Position=new(213,264),Size=new(1464,596),ClipContents=true,
            MouseFilter=MouseFilterEnum.Pass};chrome.Canvas.AddChild(rows);
        scroll=new(){Name="CampaignScrollbar",Position=new(1687,265),Size=new(16,597)};chrome.Canvas.AddChild(scroll);
        scroll.Configure(repository,chrome.Assets,chrome.SpriteShader,Math.Max(0,choices.Count*48-596),
            scrollSpeed(),_=>PositionRows(),currentScrollSpeed:scrollSpeed);
        rows.GuiInput+=input=>{if(ReadAdmission())scroll.HandleWheel(input);};
        string check=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1].Records[0x0628].Text
            ??throw new InvalidDataException("Missing campaign selection indicator.");
        DeploymentPersonColumn[] columns=[new(0,560,0,0x01,1,true),new(1,800,0,0x01,1)];
        foreach(var choice in choices)
        {
            int id=choice.Id;
            var row=new UiSourcePersonRow {Name="Campaign"+id,InputAdmission=ReadAdmission};rows.AddChild(row);
            row.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,check,columns,
                [new(choice.Caption),new(choice.Detail)]);
            row.Disabled=!choice.Enabled;row.TooltipText=choice.DisabledReason??"";
            entries.Add((id,row));if(choice.Enabled&&choice.Selected)selected.Add(id);
            row.Pressed+=()=>
            {
                if(!ReadAdmission()||row.Disabled)return;
                if(!selected.Remove(id))selected.Add(id);
                RefreshSelection();
            };
            row.GuiInput+=input=>{if(ReadAdmission())scroll.HandleWheel(input);};
        }
        SetMeta("original_campaign_columns_recovered",false);
        PositionRows();RefreshSelection();
    }

    private void PositionRows()
    {
        for(int index=0;index<entries.Count;index++)
        {
            var row=entries[index].Row;row.Position=new(0,index*48-scroll.Value);
            row.Visible=row.Position.Y>-48&&row.Position.Y<596;
        }
    }
    private void RefreshSelection()
    {
        foreach(var entry in entries)entry.Row.Selected=selected.Contains(entry.Id);
        chrome.ApplyDecision(!busy&&selected.Count>0,true);chrome.ApplyReturn(!busy);
    }
    private bool ReadAdmission()=>!abandoned&&!busy&&IsVisibleInTree()&&(admitted?.Invoke()??false);
    internal void RequestBack(){if(ReadAdmission())returned?.Invoke();}
    internal void SetBusy(bool value){if(!abandoned){busy=value;RefreshSelection();}}
    internal void ApplyNotice(string value){if(!abandoned)chrome.ApplyDefaultHint(value);}
    internal void Abandon()
    {abandoned=true;admitted=null;returned=null;accepted=null;foreach(var entry in entries){entry.Row.InputAdmission=null;entry.Row.Disabled=true;}scroll?.SetProcessInput(false);chrome?.Abandon();Hide();}
}
