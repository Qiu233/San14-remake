using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record MapLocationCommandMenuEntry(string Name,int Payload,int CaptionId,
    AreaAdministrationQuery<bool> SourcePresent,AreaAdministrationQuery<bool> SourceEnabled);

/// <summary>The shared leaf-only CCityButtonTreeMenu for location commands.</summary>
internal partial class MapLocationCommandMenu : Control
{
    private UiAssets assets=null!;
    private MapFont font=null!;
    private Shader shader=null!;
    private Control canvas=null!;
    private Func<bool>? admitted;
    private Action? returned;
    private readonly List<UiSourceListCheckboxButton> buttons=[];
    private bool abandoned, ownsResources;

    internal void Configure(GameResourceRepository repository, PreviewText text, Vector2 canonicalMouse,
        IReadOnlyList<MapLocationCommandMenuEntry> rows, Func<bool> current, Action<int> command, Action close, AdministrationPageChrome.Resources? resources = null)
    {
        ZIndex=4;MouseFilter=MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);admitted=current;returned=close;
        ownsResources=resources is null;
        assets=resources?.Assets??new(repository);font=resources?.Font??new(repository,text.SourceLocale);
        shader=resources?.SpriteShader??UiSourceSprite.CreateShader();
        canvas=new(){Name="SourceCanvas",Size=new(1920,1080),MouseFilter=MouseFilterEnum.Ignore};AddChild(canvas);
        var source=assets.SourceLayout(0x26D980DF);
        //The constructor omits descriptors whose structural flag is false.
        //Its updater only enables controls that were actually constructed.
        var entries=rows.Where(entry=>entry.SourcePresent.Available&&entry.SourcePresent.Value).ToArray();
        float span=source.Rectangle(17).End.Y-source.Rectangle(3).Position.Y;
        int height=(int)source.Rectangle(2).Size.Y-(int)span+entries.Length*((int)span/8);
        Vector2 size=new(source.Rectangle(2).Size.X,height);
        Vector2 desired=canonicalMouse+new Vector2(10,-50);
        Vector2 mount=new(Math.Clamp(desired.X,0,Math.Max(0,1920-size.X)),
            Math.Clamp(desired.Y,0,Math.Max(0,1080-size.Y)));
        if(entries.Length!=0)source.Art(canvas,2,new(mount+source.Rectangle(2).Position,size),assets,shader);
        var captions=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1].Records;
        for(int index=0;index<entries.Length;index++)
        {
            var entry=entries[index];var rectangle=source.Rectangle(3+index*2,mount);
            var button=new UiSourceListCheckboxButton{Name=entry.Name,Position=rectangle.Position,
                InputAdmission=ReadAdmission,Disabled=!entry.SourceEnabled.Available||!entry.SourceEnabled.Value};
            canvas.AddChild(button);
            button.Configure(repository,assets,shader,font,rectangle.Size,captions[entry.CaptionId].Text??
                throw new InvalidDataException("Missing location command menu caption."),()=>
                {if(ReadAdmission()&&entry.SourceEnabled.Available&&entry.SourceEnabled.Value)command(entry.Payload);});
            button.SetMeta("source_unknown",entry.SourceEnabled.UnresolvedInput??"");buttons.Add(button);
        }
        SetMeta("source_layout",source.Source.Source.ResourceUri);
        SetMeta("source_unknown_structure",rows.Where(entry=>!entry.SourcePresent.Available)
            .Select(entry=>entry.SourcePresent.UnresolvedInput??entry.Name).ToArray());
        Resized+=Fit;Fit();
    }
    private bool ReadAdmission()=>!abandoned&&IsVisibleInTree()&&(admitted?.Invoke()??false);
    internal void RequestBack(){if(ReadAdmission())returned?.Invoke();}
    internal void Abandon(){abandoned=true;admitted=null;returned=null;foreach(var button in buttons)button.Abandon();Hide();}
    private void Fit(){if(canvas is null)return;float scale=Math.Min(Size.X/1920,Size.Y/1080);
        canvas.Scale=Vector2.One*scale;canvas.Position=(Size-new Vector2(1920,1080)*scale)/2;}
    public override void _ExitTree(){Abandon();if(ownsResources){font?.Dispose();assets?.Dispose();shader?.Dispose();}}
}
