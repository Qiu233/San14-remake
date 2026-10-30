using Godot;
using San14.Data;
using San14.Data.Formats;
using System.Globalization;

internal partial class EnvironmentSettingsView
{
    private readonly List<(UiSourceRadioButton Button,GameWindowMode Mode)> displayButtons=[];
    private readonly List<(UiSourceRadioButton Button,CgQuality Quality)> cgQualityButtons=[];
    private IReadOnlyList<CgQuality> supportedCgQualities=[];
    private Func<CgQuality,bool>? previewCgQuality;
    internal CgQuality? CgQualityDraft { get; private set; }
    private readonly List<(UiSourceRadioButton Button,GameAudioSpeakerLayout Layout)> speakerLayoutButtons=[];
    private bool speakerLayoutEditable;
    private Func<GameAudioSpeakerLayout,SpeakerLayoutAvailability>? speakerLayoutAvailability;
    internal GameAudioSpeakerLayout? SpeakerLayoutDraft { get; private set; }
    internal bool SpeakerLayoutEdited { get; private set; }
    private UiSourceFunctionButton? displayResolutionButton;
    private UiSourceText? displayResolutionText;
    private UiSourcePulldownMenu? displayResolutionMenu;
    private IReadOnlyList<DisplayResolution> displayResolutions=[];
    private Action<DisplayPreferences>? previewDisplay;
    private GameResourceRepository displayRepository=null!;
    private PreviewText displayText=null!;
    private Rect2 displayMenuBounds;
    private string resolutionFormat="";
    internal DisplayPreferences? DisplayDraft { get; private set; }
    internal ApplicationPresentationOptions PresentationDraft { get; private set; }=new();

    private void ConfigureDisplayPage(GameResourceRepository repository,PreviewText text,
        UiSourceLayout source,UiSourceLayout rowSource,Func<int,int,string> data)
    {
        displayRepository=repository;displayText=text;
        var page=pages[1];
        var narration=new AdministrationMessages(repository,text.SourceLocale);
        int[] modes=[0x6E,0x6F,0x70],nodes=[4,7,10];
        var mount=source.Rectangle(4).Position;
        rowSource.Art(page,2,rowSource.Rectangle(2,mount),chrome.Assets,chrome.SpriteShader);
        rowSource.Text(page,3,mount,data(1,0x4CF),chrome.Font);
        for(int index=0;index<modes.Length;index++)
        {
            var mode=(GameWindowMode)index;
            var box=rowSource.Rectangle(nodes[index],mount);
            var button=new UiSourceRadioButton{Name="EnvironmentWindowMode"+index,
                Position=box.Position,Size=box.Size,Disabled=true};page.AddChild(button);
            button.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,
                index==0?UiSourceRadioSegment.Left:index==2?UiSourceRadioSegment.Right:UiSourceRadioSegment.Centre,
                data(0,modes[index]),()=>
                {
                    if(abandoned||busy||KeyboardModal||displayResolutionMenu is not null||DisplayDraft is not {} draft)return;
                    ApplyDisplayOptions(draft with{Mode=mode});
                });
            string hint=narration.Format(0x2747);
            button.MouseEntered+=()=>{if(!abandoned&&!busy)ApplyHelp(hint);};
            button.MouseExited+=()=>{if(!abandoned&&!busy)ApplyHelp(CurrentPageHelp);};
            displayButtons.Add((button,mode));
        }
        var resolution=new UiSourceLayout(repository,0x07D4EEEE);
        mount=source.Rectangle(5).Position;
        resolution.Art(page,2,resolution.Rectangle(2,mount),chrome.Assets,chrome.SpriteShader);
        resolution.Text(page,3,mount,data(1,0x4D0),chrome.Font);
        resolution.Art(page,4,resolution.Rectangle(4,mount),chrome.Assets,chrome.SpriteShader);
        displayResolutionText=resolution.Text(page,5,mount,"",chrome.Font);
        resolutionFormat=data(1,0x61C);
        displayMenuBounds=resolution.Rectangle(11,mount);
        displayResolutionButton=new UiSourceFunctionButton{Name="EnvironmentResolution",
            Position=resolution.Rectangle(6,mount).Position,Disabled=true};page.AddChild(displayResolutionButton);
        displayResolutionButton.Configure(repository,chrome.Assets,chrome.SpriteShader,"000_cmn_icon_sort_kou",OpenDisplayResolutions);
        string resolutionHint=narration.Format(0x2748);
        displayResolutionButton.MouseEntered+=()=>{if(!abandoned&&!busy)ApplyHelp(resolutionHint);};
        displayResolutionButton.MouseExited+=()=>{if(!abandoned&&!busy)ApplyHelp(CurrentPageHelp);};

        mount=source.Rectangle(6).Position;
        rowSource.Art(page,2,rowSource.Rectangle(2,mount),chrome.Assets,chrome.SpriteShader);
        rowSource.Text(page,3,mount,data(1,0x4D1),chrome.Font);
        int[] qualities=[0x7D,0x7E,0x7F];
        for(int index=0;index<qualities.Length;index++)
        {
            var quality=(CgQuality)index;
            var box=rowSource.Rectangle(nodes[index],mount);
            var button=new UiSourceRadioButton{Name="EnvironmentCgQuality"+index,
                Position=box.Position,Size=box.Size,Disabled=true};page.AddChild(button);
            button.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,
                index==0?UiSourceRadioSegment.Left:index==2?UiSourceRadioSegment.Right:UiSourceRadioSegment.Centre,
                data(0,qualities[index]),()=>
                {
                    if(!abandoned&&!busy&&!KeyboardModal&&displayResolutionMenu is null)
                        ApplyCgQualityOptions(quality);
                });
            string hint=narration.Format(0x2749);
            button.MouseEntered+=()=>{if(!abandoned&&!busy)ApplyHelp(hint);};
            button.MouseExited+=()=>{if(!abandoned&&!busy)ApplyHelp(CurrentPageHelp);};
            cgQualityButtons.Add((button,quality));
        }
        mount=source.Rectangle(7).Position;
        rowSource.Art(page,2,rowSource.Rectangle(2,mount),chrome.Assets,chrome.SpriteShader);
        rowSource.Text(page,3,mount,data(1,0x6D1),chrome.Font);
        int[] speakers=[0x6D2,0x6D3];
        for(int index=0;index<speakers.Length;index++)
        {
            var layout=(GameAudioSpeakerLayout)index;
            var box=rowSource.Rectangle(nodes[index],mount);
            var button=new UiSourceRadioButton{Name="EnvironmentSpeakerLayout"+index,
                Position=box.Position,Size=box.Size,Disabled=true};page.AddChild(button);
            button.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,
                index==0?UiSourceRadioSegment.Left:UiSourceRadioSegment.Right,data(1,speakers[index]),()=>
                {
                    if(!abandoned&&!busy&&!KeyboardModal&&displayResolutionMenu is null&&speakerLayoutEditable)
                        ApplySpeakerLayoutOptions(layout, true);
                });
            string hint=narration.Format(speakerLayoutEditable?0x274D:0x274E);
            button.MouseEntered+=()=>{if(!abandoned&&!busy)ApplyHelp(hint);};
            button.MouseExited+=()=>{if(!abandoned&&!busy)ApplyHelp(CurrentPageHelp);};
            speakerLayoutButtons.Add((button,layout));
        }
        RefreshDisplayAdmission(); RefreshCgQualityAdmission(); RefreshSpeakerLayoutAdmission();
    }

    internal void ApplyPresentationOptions(ApplicationPresentationOptions value)
    {
        if(abandoned)return;
        PresentationDraft=value;
        foreach(var row in choices.Where(row=>row.Row is >=4 and <=6))
        {
            bool enabled=row.Row switch{4=>value.TacticDirectionsEnabled,5=>value.MoviesEnabled,_=>value.DuelsEnabled};
            row.Button.Selected=row.Index==(enabled?1:0);
        }
    }

    internal void ApplyDisplayOptions(DisplayPreferences? value,bool preview=true)
    {
        if(abandoned)return;
        value?.Validate();DisplayDraft=value;
        if(displayResolutionText is {} label)
            label.Text=value is null?displayText.Get("detail_unknown"):ResolutionCaption(value.Width,value.Height);
        foreach(var row in displayButtons)row.Button.Selected=value?.Mode==row.Mode;
        RefreshDisplayAdmission();
        if(preview&&value is not null)previewDisplay?.Invoke(value);
    }

    internal bool ApplySpeakerLayoutOptions(GameAudioSpeakerLayout? value, bool userSelection=false)
    {
        if(abandoned)return false;
        if(value is {} layout && speakerLayoutEditable &&
            !(speakerLayoutAvailability?.Invoke(layout).Available??false))return false;
        SpeakerLayoutDraft=value; if(userSelection)SpeakerLayoutEdited=true;
        foreach(var row in speakerLayoutButtons)row.Button.Selected=value==row.Layout;
        RefreshSpeakerLayoutAdmission();return true;
    }

    private void RefreshSpeakerLayoutAdmission()
    {
        foreach(var row in speakerLayoutButtons)
        {
            var availability=abandoned?null:speakerLayoutAvailability?.Invoke(row.Layout);
            row.Button.Disabled=abandoned||busy||KeyboardModal||displayResolutionMenu is not null||
                !speakerLayoutEditable||availability?.Available!=true;
            row.Button.SetMeta("unresolved_owner",availability?.UnresolvedOwner??
                (speakerLayoutEditable?"":"environment.speaker_layout.in_campaign"));
        }
    }

    internal bool ApplyCgQualityOptions(CgQuality? value,bool preview=true)
    {
        if(abandoned)return false;
        if(value is {} quality&&preview&&!(previewCgQuality?.Invoke(quality)??false))return false;
        CgQualityDraft=value;
        foreach(var row in cgQualityButtons)row.Button.Selected=value==row.Quality;
        RefreshCgQualityAdmission();return true;
    }

    private void RefreshCgQualityAdmission()
    {
        foreach(var row in cgQualityButtons)
            row.Button.Disabled=abandoned||busy||KeyboardModal||displayResolutionMenu is not null||
                previewCgQuality is null||!supportedCgQualities.Contains(row.Quality);
    }

    private string ResolutionCaption(int width,int height)
    {
        string text=resolutionFormat;
        int at=text.IndexOf("%d",StringComparison.Ordinal);
        if(at<0)throw new InvalidDataException("environment.resolution.width_format");
        text=text[..at]+width.ToString(CultureInfo.InvariantCulture)+text[(at+2)..];
        at=text.IndexOf("%d",StringComparison.Ordinal);
        if(at<0)throw new InvalidDataException("environment.resolution.height_format");
        return text[..at]+height.ToString(CultureInfo.InvariantCulture)+text[(at+2)..];
    }

    private void RefreshDisplayAdmission()
    {
        bool blocked=abandoned||busy||KeyboardModal||displayResolutionMenu is not null;
        foreach(var row in displayButtons)
            row.Button.Disabled=blocked||previewDisplay is null||DisplayDraft is not {} draft||
                !displayResolutions.Any(value=>value.Width==draft.Width&&value.Height==draft.Height&&value.Supports(row.Mode));
        if(displayResolutionButton is {} button)
            button.Disabled=blocked||previewDisplay is null||DisplayDraft is not {} current||
                !displayResolutions.Any(value=>value.Supports(current.Mode));
    }

    private void OpenDisplayResolutions()
    {
        if(abandoned||busy||KeyboardModal||displayResolutionMenu is not null||DisplayDraft is not {} draft)return;
        var items=displayResolutions.Select(row=>new UiSourcePulldownItem(
            ResolutionCaption(row.Width,row.Height),row.Supports(draft.Mode))).ToArray();
        if(items.Length==0)return;
        var menu=new UiSourcePulldownMenu{Name="EnvironmentResolutionChoices",ZIndex=15,ClipContents=true};
        displayResolutionMenu=menu;chrome.Canvas.AddChild(menu);
        try
        {
            var bounds=displayMenuBounds;
            bounds.Size=new(bounds.Size.X,Math.Min(bounds.Size.Y,items.Length*38+2));
            menu.Configure(displayRepository,displayText.SourceLocale,bounds,items,
                UiSourcePulldownOutsideClick.ConfirmConsumed,selected=>
                {
                    if(abandoned||displayResolutionMenu!=menu)return;
                    CloseDisplayResolutions();
                    if(selected is int index&&DisplayDraft is {} current)
                    {
                        var row=displayResolutions[index];
                        ApplyDisplayOptions(current with{Width=row.Width,Height=row.Height});
                    }
                });
            int offset=0;
            var rows=menu.GetChildren().OfType<UiSourcePulldownButton>().ToArray();
            void Scroll(InputEvent input)
            {
                if(input is not InputEventMouseButton{Pressed:true} mouse||
                    mouse.ButtonIndex is not(MouseButton.WheelUp or MouseButton.WheelDown))return;
                offset=Math.Clamp(offset+(mouse.ButtonIndex==MouseButton.WheelUp?-38:38),0,
                    Math.Max(0,items.Length*38+2-(int)bounds.Size.Y));
                for(int index=0;index<rows.Length;index++)rows[index].Position=new(0,index*38-offset);
                GetViewport().SetInputAsHandled();
            }
            menu.GuiInput+=Scroll;
            foreach(var row in rows)row.GuiInput+=Scroll;
            RefreshDisplayAdmission(); RefreshCgQualityAdmission(); RefreshSpeakerLayoutAdmission();
        }
        catch{CloseDisplayResolutions();throw;}
    }

    private bool CloseDisplayResolutions()
    {
        var menu=displayResolutionMenu;displayResolutionMenu=null;
        if(menu is null)return false;
        menu.Abandon();menu.QueueFree();RefreshDisplayAdmission();RefreshCgQualityAdmission();RefreshSpeakerLayoutAdmission();return true;
    }
}
