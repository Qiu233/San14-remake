using Godot;
using San14.Data;

// Four original resource controls, owned only by the actual chronology scope.
internal partial class ChronologyMapFlareLayer : Control
{
    private UiAssets? assets;private Shader? shader;
    private readonly List<Flare> flares=[];
    private readonly Node2D canvas=new();
    internal void Configure(GameResourceRepository repository)
    {
        MouseFilter=MouseFilterEnum.Ignore;SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(canvas);Resized+=Fit;Fit();
        assets=new(repository);shader=UiSourceSprite.CreateShader();
        var definition=MapMarkerLayout.LoadNamed(repository,"cmn_map_flare");
        for(int index=0;index<4;index++)
        {
            var flare=new Flare{Name="ChronologyFlare"+index};canvas.AddChild(flare);
            flare.Configure(definition,assets,shader);flares.Add(flare);
        }
    }
    private void Fit()=>canvas.Scale=Size/new Vector2(1920,1080);
    internal void HideFlares(){foreach(var flare in flares)flare.SetShown(false);}
    internal void Present(int index,Vector2? pixel)
    {
        var flare=flares[index];
        if(pixel is not {} p){flare.SetShown(false);return;}
        flare.Present(p);
    }
    public override void _ExitTree()
    {assets?.Dispose();assets=null;shader?.Dispose();shader=null;}
    private partial class Flare : UiSourceAnimationView
    {
        private double clock;private bool shown;
        internal void Configure(LoadedLayout definition,UiAssets assets,Shader shader)
        {
            var layout=new UiSourceLayout(definition);
            AutomaticAnimationClock=false;
            ConfigureAnimation(layout,new HashSet<string>{"flare_loop"},layout.Nodes.Select(node=>node.Index));
            foreach(var node in layout.Nodes.Where(node=>node.Type is 1 or 3))
                layout.Art(AnimationPanes[node.Index],node.Index,new(Vector2.Zero,new(node.Value44,node.Value46)),
                    assets,shader,localCoordinates:true);
        }
        internal void Present(Vector2 position){Position=position;SetShown(true);}
        internal void SetShown(bool value){shown=value;Visible=shown;}
        public override void _Process(double delta)
        {clock+=delta;ApplyAnimationAt("flare_loop",clock,repeat:true);Visible=shown;}
    }
}
