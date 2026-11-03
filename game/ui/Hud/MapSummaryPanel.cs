using System.Buffers.Binary;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>A real compact summary layout. Only current presentation data is
/// rebound; artwork, masks, labels and nested controls belong to this panel.</summary>
internal partial class MapSummaryPanel : Control
{
    private MapSummaryResources resources=null!;
    private UiSourceLayout? source;
    private string layoutName="";
    private readonly Dictionary<int,Control> artwork=[];
    private readonly Dictionary<int,UiSourceText> labels=[];
    private readonly Dictionary<int,(int? Preset,MapCornerColors? Colors)> labelProfiles=[];
    private readonly Dictionary<int,(MapSummaryImage Identity,Control View)> images=[];
    private readonly Dictionary<int,(MapSummaryPortrait Identity,Control View)> portraits=[];
    private readonly Dictionary<int,(int Reference,Control Clip,Control Fill)> gauges=[];
    private readonly Dictionary<string,(int? Mount,MapSummaryPanel View)> nested=[];
    private readonly HashSet<int> replacements=[];
    internal string LayoutName=>layoutName;
    internal int Slot {get;private set;}

    internal void Configure(MapSummaryResources resources,string name, IReadOnlyDictionary<int,UiSourceNodeGeometry>? geometry=null)
    {
        this.resources=resources;layoutName=name;source=name.Length==0?null:geometry is null?resources.Layout(name):
            new UiSourceLayout(resources.Layout(name).Source,geometry);
        Name="MapSummaryPanel";MouseFilter=MouseFilterEnum.Ignore;
        if(source is null){Size=Vector2.Zero;return;}
        Size=source.Rectangle(0).Size;
        foreach(var node in source.Nodes)
        {
            if(node.Type==2)
            {
                var text=source.Text(this,node.Index,default,"",resources.Font);
                text.ZIndex=node.Index;labels.Add(node.Index,text);
            }
        }
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }

    internal void Apply(MapSummaryPage page)
    {
        if(page.LayoutName!=layoutName)throw new InvalidDataException("map.summary.layout_owner");
        Slot=page.Slot;SetMeta("source_summary_slot",page.Slot);SetMeta("source_receiver",page.ReceiverId);
        SetMeta("source_unresolved_owner",page.UnresolvedOwner??"");
        //Dynamic mounts replace their source dummy subtree. Remember these
        //roles when an unknown/hidden publication omits its resolved resource.
        foreach(var flag in page.Flags)replacements.Add(flag.Node);
        if(page.Traits.Count>0)replacements.Add(3);
        foreach(var bar in page.Bars)replacements.Add(bar.Node);
        foreach(var radar in page.Radars)replacements.Add(radar.Node);
        foreach(var image in page.Images)replacements.Add(image.Node);
        foreach(var indicator in page.Indicators)replacements.Add(indicator.Node);
        foreach(var portrait in page.Portraits)replacements.Add(portrait.Node);
        foreach(var gauge in page.Gauges){replacements.Add(gauge.Node);if(gauge.AnimationName is not null)replacements.Add(gauge.ReferenceNode);}
        foreach(var child in page.Children)if(child.MountNode is int node)replacements.Add(node);
        if(source is not null) foreach(var node in source.Nodes)
        {
            if(node.Type is not (1 or 3 or 6))continue;
            bool replaced=Replaced(node.Index);
            if(!replaced&&Shown(node.Index,page)&&!artwork.ContainsKey(node.Index))
            {
                var pane=node.Type==6?source.Blur(this,node.Index,source.Rectangle(node.Index)):
                    source.Art(this,node.Index,source.Rectangle(node.Index),resources.Assets,resources.Shader,
                        blend:ArtworkBlend(node.Index));
                pane.ZIndex=node.Index;artwork.Add(node.Index,pane);
            }
            if(artwork.TryGetValue(node.Index,out var art))
            {art.Visible=!replaced&&Shown(node.Index,page);
                ApplyColors(art,page.Colors.TryGetValue(node.Index,out var tint)?tint:null);}
        }
        foreach(var(node,label) in labels)
        {
            label.Text=page.Text.GetValueOrDefault(node)??"";
            label.Visible=!Replaced(node)&&Shown(node,page);
            int? preset=page.TextPresets.TryGetValue(node,out int selectedPreset)?selectedPreset:null;
            MapCornerColors? colors=page.Colors.TryGetValue(node,out var color)?color:null;
            if(!labelProfiles.TryGetValue(node,out var previous)||previous!=(preset,colors))
            {
                var profile=MapFontProfile.FromNode(source!.Nodes[node],source.Source.Source.ResourceUri);
                if(preset is int style)profile=profile.WithPreset(resources.Presets[style]);
                if(colors is{} c)profile=profile with {ColourLT=c.LT.CpuPacked,ColourRT=c.RT.CpuPacked,
                    ColourLB=c.LB.CpuPacked,ColourRB=c.RB.CpuPacked};
                label.Configure(resources.Font,profile);labelProfiles[node]=(preset,colors);
            }
            label.SetMeta("source_unresolved_owner",page.UnresolvedFields.GetValueOrDefault(node)??"");
        }
        foreach(var(_,view) in images.Values)view.Hide();
        foreach(var image in page.Images)
        {
            if(!images.TryGetValue(image.Node,out var existing)||existing.Identity!=image)
            {
                Retire(existing.View);
                var view=source!.Art(this,image.Node,source.Rectangle(image.Node),resources.Assets,resources.Shader,
                    commonSpriteOverride:image.SpriteName,commonTexturePack:image.TexturePack);
                view.ZIndex=image.Node;existing=(image,view);images[image.Node]=existing;
            }
            existing.View.Visible=Shown(image.Node,page);
            ApplyColors(existing.View,page.Colors.TryGetValue(image.Node,out var color)?color:null);
        }
        ApplyDevelopmentArrow(page);
        foreach(var(_,view) in portraits.Values)view.Hide();
        foreach(var portrait in page.Portraits)ApplyPortrait(portrait,page);
        foreach(var(_,clip,_) in gauges.Values)clip.Hide();
        foreach(var(_,_,view) in animatedGauges.Values)view.Hide();
        foreach(var gauge in page.Gauges)
        {
            if(gauge.AnimationName is not null){ApplyAnimatedGauge(gauge,page);continue;}
            if(!gauges.TryGetValue(gauge.Node,out var existing)||existing.Reference!=gauge.ReferenceNode)
            {
                Retire(existing.Clip);
                var reference=source!.Rectangle(gauge.ReferenceNode);
                var clip=new Control {Name="SummaryGauge"+gauge.Node,Position=reference.Position,Size=reference.Size,
                    ClipContents=true,MouseFilter=MouseFilterEnum.Ignore,ZIndex=gauge.Node};AddChild(clip);
                var fill=source.Art(clip,gauge.Node,source.Rectangle(gauge.Node,-reference.Position),resources.Assets,resources.Shader);
                existing=(gauge.ReferenceNode,clip,fill);gauges[gauge.Node]=existing;
            }
            var rectangle=source!.Rectangle(gauge.ReferenceNode);
            int width=gauge.Maximum==0?0:(int)((long)(int)rectangle.Size.X*gauge.Current/gauge.Maximum);
            existing.Clip.Size=new(width,rectangle.Size.Y);
            existing.Clip.Visible=width>0&&Shown(gauge.Node,page);
            existing.Clip.SetMeta("source_fill_width",width);
            ApplyColors(existing.Fill,page.Colors.TryGetValue(gauge.Node,out var color)?color:null);
        }
        ApplyBars(page);
        ApplyTraits(page);
        ApplyRadars(page);
        ApplyFlags(page);
        ApplyIndicators(page);
        foreach(var(_,view) in nested.Values)view.Hide();
        foreach(var child in page.Children)
        {
            if(!nested.TryGetValue(child.Key,out var existing)||existing.Mount!=child.MountNode||existing.View.LayoutName!=child.Page.LayoutName)
            {
                Retire(existing.View);
                var view=new MapSummaryPanel {Name="SummaryChild_"+child.Key};AddChild(view);
                view.Configure(resources,child.Page.LayoutName);
                if(child.MountNode is int node)
                {var box=source!.Rectangle(node);view.Position=box.Position;
                    if(view.Size.X>0&&view.Size.Y>0)view.Scale=box.Size/view.Size;view.ZIndex=node;}
                else if(view.source is not null)view.Position=view.source.AuthoredRootRectangle().Position;
                existing=(child.MountNode,view);nested[child.Key]=existing;
            }
            existing.View.Apply(child.Page);
            existing.View.Visible=child.Page.ReceiverValid==true&&(child.MountNode is not int visibleMount||Shown(visibleMount,page));
        }
    }

    private UiSourceSprite.Blend ArtworkBlend(int index)
    {
        // The dialog mask writes only destination alpha. Rendering its white
        // source as ordinary artwork changes the RGB behind both blur passes.
        // Resolve this from the layout's actual nonempty render groups rather
        // than a sprite name or a special summary slot.
        var sprite=LcskSpriteNodeReader.Read(source!.Source.Layout,source.Nodes[index]);
        var groups=sprite.Groups.Where(group=>group.ComponentCount!=0).ToArray();
        bool alphaMask=groups.Length>0&&groups.All(group=>
            BinaryPrimitives.ReadUInt32LittleEndian(group.RawRenderBytes.AsSpan(4,4))==0x00011000u);
        return alphaMask?UiSourceSprite.Blend.AlphaMask:UiSourceSprite.Blend.Ordinary;
    }

    private bool Replaced(int index)
    {
        for(int? current=index;current is int node;current=source!.Nodes[node].ParentIndex)
            if(replacements.Contains(node))return true;
        return false;
    }
    private bool Shown(int index,MapSummaryPage page)
    {
        for(int? current=index;current is int node;current=source!.Nodes[node].ParentIndex)
            if(page.Visibility.TryGetValue(node,out bool visible)&&!visible)return false;
        return true;
    }
    private static void Retire(Control? view)
    {if(view is not null){view.GetParent().RemoveChild(view);view.QueueFree();}}
    private static void ApplyColors(Node root,MapCornerColors? colors)
    {
        if(root is UiSourceSprite image){if(colors is{} value)image.SetColors(value);else image.ResetColors();}
        foreach(var child in root.GetChildren())ApplyColors(child,colors);
    }
}
