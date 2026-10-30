using Godot;
using San14.Data;
using San14.Data.Formats;

internal partial class RegisteredOfficerCityPickerView : Control
{
    private UiAssets assets=null!;
    private MapFont font=null!;
    private Shader shader=null!;
    private Control canvas=null!;
    private UiSourceText hint=null!;
    private Action? returned;
    internal void Configure(GameResourceRepository repository,PreviewText text,UiSourceHeaderStack headers,Action returned)
    {
        this.returned=returned;Name="RegisteredOfficerCityPickerHeader";ZIndex=6;MouseFilter=MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        assets=new(repository);font=new(repository,text.SourceLocale);shader=UiSourceSprite.CreateShader();
        canvas=new(){Size=new(1920,1080),MouseFilter=MouseFilterEnum.Ignore};AddChild(canvas);
        var header=new UiSourceHeader();canvas.AddChild(header);
        string title=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1].Records[0x206].Text
            ??throw new InvalidDataException("registered_officers.city_picker.header");
        header.Configure(repository,assets,shader,font,title);header.Publish(headers);
        var footer=new UiSourceLayout(repository,0xD8913AB2);Vector2 mount=new(0,904);
        var back=new UiSourceRoundButton{Position=footer.Rectangle(1,mount).Position,ActionMode=BaseButton.ActionModeEnum.Release};
        canvas.AddChild(back);back.Configure(repository,assets,shader,text.SourceLocale,"000_cmn_txt_decision_modoru",
            ()=>this.returned?.Invoke(),UiAudioCue.Cancel);
        var source=new UiSourceLayout(repository,0xAB6ADAA7);Vector2 hintMount=footer.Rectangle(19,mount).Position;
        foreach(var node in source.Nodes.Where(node=>node.Type is 1 or 3))
            source.Art(canvas,node.Index,source.Rectangle(node.Index,hintMount),assets,shader);
        hint=source.Text(canvas,source.Nodes.Single(node=>node.Type==2).Index,hintMount,
            RegisteredOfficerNewGameMessages.Format(repository,text.SourceLocale,0x27E7),font);
        Resized+=Fit;Fit();
    }
    internal void ApplyNotice(string message)=>hint.Text=message;
    internal void Abandon(){returned=null;Hide();}
    private void Fit()
    {
        float scale=Math.Min(Size.X/1920,Size.Y/1080);canvas.Scale=Vector2.One*scale;
        canvas.Position=(Size-new Vector2(1920,1080)*scale)/2;
    }
    public override void _ExitTree(){font?.Dispose();assets?.Dispose();shader?.Dispose();}
}
