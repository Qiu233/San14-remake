using Godot;
using San14.Data;

/// <summary>The scenery state's actual three-leaf CCityButtonTreeMenu.</summary>
internal partial class SceneryHexCommandMenu : Control
{
    private UiAssets assets = null!;
    private MapFont font = null!;
    private Shader shader = null!;
    private Control canvas = null!;
    private readonly List<UiSourceListCheckboxButton> buttons = [];
    private IReadOnlyList<SceneryHexMenuItem> publication = [];
    private Func<bool>? admitted;
    private Action? closed;
    private bool abandoned;
    internal bool IsOpen => !abandoned && IsVisibleInTree();

    internal void Configure(GameResourceRepository repository, PreviewText text, Vector2 canonicalMouse,
        IReadOnlyList<SceneryHexMenuItem> items, Func<bool> current, Action<int> command, Action close)
    {
        if (items.Count != 3 || !items.Select(item => item.Payload).SequenceEqual(new[] {0,1,2}))
            throw new InvalidDataException("Scenery command tree requires its three ordered source leaves.");
        Name = "SceneryHexCommandMenu338"; admitted = current; closed = close; publication = items;
        MouseFilter = MouseFilterEnum.Ignore; SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        assets = new(repository); font = new(repository,text.SourceLocale); shader = UiSourceSprite.CreateShader();
        canvas = new() { Name = "SourceCanvas", Size = new(1920,1080), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(canvas);
        var source = new UiSourceLayout(repository,0x26D980DF);
        Vector2 mount = canonicalMouse + new Vector2(10,-50);
        float span = source.Rectangle(17).End.Y - source.Rectangle(3).Position.Y;
        int height = (int)source.Rectangle(2).Size.Y - (int)span + items.Count * ((int)span/8);
        source.Art(canvas,2,new(mount+source.Rectangle(2).Position,new(source.Rectangle(2).Size.X,height)),assets,shader);
        for (int index=0;index<items.Count;index++)
        {
            int slot=index;var item=items[index];var box=source.Rectangle(3+index*2,mount);
            var button=new UiSourceListCheckboxButton { Name="SceneryCommand"+item.Payload,
                Position=box.Position,InputAdmission=ReadAdmission,Disabled=item.Enabled!=true };
            canvas.AddChild(button);
            button.Configure(repository,assets,shader,font,box.Size,item.Caption,()=> {
                if(ReadAdmission()&&publication[slot].Enabled==true)command(publication[slot].Payload);
            });
            button.SetMeta("source_unknown",item.UnresolvedOwner??"");buttons.Add(button);
        }
        SetMeta("source_layout",source.Source.Source.ResourceUri);SetMeta("source_registry",0x67);
        Resized+=Fit;Fit();
    }
    internal void Apply(IReadOnlyList<SceneryHexMenuItem> items)
    {
        if(abandoned)return;
        if(items.Count!=3||!items.Select(item=>item.Payload).SequenceEqual(new[]{0,1,2}))
            throw new InvalidDataException("Scenery command refresh requires the three original source leaves.");
        publication=items;
        for(int index=0;index<buttons.Count;index++)
        {
            buttons[index].Disabled=items[index].Enabled!=true;
            buttons[index].SetMeta("source_unknown",items[index].UnresolvedOwner??"");
        }
    }
    private bool ReadAdmission()=>!abandoned&&IsVisibleInTree()&&(admitted?.Invoke()??false);
    internal void Close(){if(abandoned)return;Hide();closed?.Invoke();}
    internal void RequestBack(){if(ReadAdmission())Close();}
    internal void Abandon(){abandoned=true;admitted=null;closed=null;foreach(var button in buttons)button.Abandon();Hide();}
    private void Fit(){if(canvas is null)return;float scale=Math.Min(Size.X/1920,Size.Y/1080);
        canvas.Scale=Vector2.One*scale;canvas.Position=(Size-new Vector2(1920,1080)*scale)/2;}
    public override void _ExitTree(){Abandon();font?.Dispose();assets?.Dispose();shader?.Dispose();}
}
