using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Size, local transform origin and scale written by a source caller.</summary>
internal sealed record UiSourceNodeGeometry(Vector2? Size = null, Vector2? Origin = null, Vector2? Scale = null,
    Vector2? Offset = null);

/// <summary>Source geometry for mounted UI children. Each child root is
/// replaced by its caller's mount; designer root offsets are never added twice.</summary>
internal sealed class UiSourceLayout
{
    internal LoadedLayout Source { get; }
    internal IReadOnlyList<LcskNodeRecord> Nodes { get; }
    private readonly Rect2[] rectangles;
    private readonly Transform2D[] transforms;
    private readonly Vector2[] nodeSizes;

    internal UiSourceLayout(GameResourceRepository repository, uint file,
        IReadOnlyDictionary<int, UiSourceNodeGeometry>? geometry = null)
        : this(repository.LoadLayout("ScreenLayout", file), geometry) { }

    internal UiSourceLayout(LoadedLayout source, IReadOnlyDictionary<int, UiSourceNodeGeometry>? geometry = null)
    {
        Source = source;
        Nodes = LcskNodeReader.Read(Source.Layout);
        rectangles = new Rect2[Nodes.Count];
        transforms = new Transform2D[Nodes.Count];
        nodeSizes = new Vector2[Nodes.Count];
        foreach (var node in Nodes)
        {
            var f = node.HeaderValues;
            UiSourceNodeGeometry? current = geometry?.GetValueOrDefault(node.Index);
            Vector2 size = current?.Size ?? new(node.Value44,node.Value46);
            nodeSizes[node.Index] = size;
            if (f.Any(v => !float.IsFinite(v)) || f[7] <= 0 || f[8] <= 0)
                throw new InvalidDataException($"Unsupported static UI transform: {node.Name}.");
            if (node.ParentIndex is not int parent)
            {
                transforms[node.Index] = Transform2D.Identity;
                rectangles[node.Index] = new(Vector2.Zero, size);
                continue;
            }
            var parentNode = Nodes[parent];
            Vector2 parentSize = geometry?.GetValueOrDefault(parent)?.Size ?? new(parentNode.Value44,parentNode.Value46);
            Vector2 attachment = Anchor((byte)parentNode.Value48) * parentSize +
                (Anchor((byte)(node.Value48 >> 8)) - Vector2.One * .5f) * parentSize +
                (current?.Offset ?? new Vector2(f[0],-f[1]));
            // Project the source quaternion into the UI plane, converting Y-up
            // to Y-down on both sides. Top scrollbar arrows use a real X half-turn.
            float x=f[3],y=f[4],z=f[5],w=f[6];
            Vector2 basisX=new(1-2*(y*y+z*z),-2*(x*y+z*w));
            Vector2 basisY=new(-2*(x*y-z*w),1-2*(x*x+z*z));
            Vector2 scale = current?.Scale ?? new(f[7],f[8]);
            var local=new Transform2D(basisX*scale.X,basisY*scale.Y,attachment);
            local.Origin-=local.BasisXform(Anchor((byte)node.Value48)*size);
            if (current?.Origin is Vector2 origin) local.Origin = origin;
            transforms[node.Index]=transforms[parent]*local;
            rectangles[node.Index]=transforms[node.Index]*new Rect2(Vector2.Zero,size);
        }
    }

    internal Rect2 Rectangle(int index, Vector2 mount = default) => new(rectangles[index].Position + mount, rectangles[index].Size);

    internal UiSourceType6Parameters Type6(int index) => UiSourceType6Parameters.Read(Nodes[index]);

    internal UiSourceType6 Blur(Control parent, int index, Rect2 rectangle, float? weightInput = null)
    {
        var parameters = Type6(index);
        if (weightInput is float weight)
        {
            if (!float.IsFinite(weight) || weight <= 0)
                throw new ArgumentOutOfRangeException(nameof(weightInput));
            parameters = parameters with { WeightInput = weight };
        }
        var transform = transforms[index];
        if (Math.Abs(transform.X.Y) > .00001f || Math.Abs(transform.Y.X) > .00001f ||
            Nodes[index].HeaderValues[2] != 0)
            throw new NotSupportedException("Source Type6 mounting requires an axis-aligned planar pane.");
        var node = new UiSourceType6 { Name = Nodes[index].Name + index,
            Position = rectangle.Position, Size = rectangle.Size };
        Vector2 orientation = new(Math.Sign(transform.X.X), Math.Sign(transform.Y.Y));
        if (orientation.X == 0 || orientation.Y == 0) throw new NotSupportedException("Type6 has degenerate projected geometry.");
        node.Scale = orientation;
        node.Position += new Vector2(orientation.X < 0 ? rectangle.Size.X : 0, orientation.Y < 0 ? rectangle.Size.Y : 0);
        parent.AddChild(node); node.Configure(parameters);
        node.SetMeta("source_node_index", index); node.SetMeta("source_layout", Source.Source.ResourceUri);
        return node;
    }

    internal UiSourceType6Geometry Type6Geometry(int index, Control canvas, Vector2I contextSize,
        Vector2 mount, bool flipUvRows)
    {
        _ = Type6(index);
        if (Nodes[index].HeaderValues[2] != 0 || Nodes[index].Value44 > short.MaxValue || Nodes[index].Value46 > short.MaxValue)
            throw new NotSupportedException("Type6 requires supported planar source dimensions.");
        var size = nodeSizes[index];
        Vector2[] corners = [Vector2.Zero, new(size.X, 0), new(0, size.Y), size];
        Transform2D screen = canvas.GetGlobalTransformWithCanvas();
        return UiSourceType6Geometry.FromScreenCorners(corners.Select(p => screen * (transforms[index] * p + mount)).ToArray(),
            contextSize, flipUvRows);
    }

    /// <summary>Apply the source recursive size setter, preserving integer local
    /// top-left positions before the original anchor transforms are evaluated.</summary>
    internal UiSourceLayout ResizedSubtree(int index,Vector2 targetSize,bool preserveMargins,
        Vector2? rootSize = null)
    {
        var sizes=Nodes.Select(n=>new Vector2(n.Value44,n.Value46)).ToArray();
        var positions=Nodes.Select(n=>new Vector2(n.HeaderValues[0],n.HeaderValues[1])).ToArray();
        var changed=new HashSet<int>();
        Vector2 original=sizes[index];
        if(original.X==0||original.Y==0)throw new InvalidDataException("Source recursive resize requires nonzero dimensions.");
        Resize(index,targetSize/original);
        var geometry=changed.ToDictionary(i=>i,i=>new UiSourceNodeGeometry(Size:sizes[i],
            Offset:new(positions[i].X,-positions[i].Y)));
        if(rootSize is Vector2 root)geometry[0]=new(Size:root);
        return new UiSourceLayout(Source,geometry);

        Vector2 IntegerAnchor(int i)
        {
            Vector2 anchor=Anchor((byte)Nodes[i].Value48)*2;
            return new((int)(anchor.X*sizes[i].X*Nodes[i].HeaderValues[7]*.5f),
                (int)(anchor.Y*sizes[i].Y*Nodes[i].HeaderValues[8]*.5f));
        }
        Vector2 TopLeft(int i)
        {
            Vector2 offset=IntegerAnchor(i);
            return new((int)positions[i].X-offset.X,-((int)positions[i].Y+offset.Y));
        }
        void SetTopLeft(int i,Vector2 value)
        {
            Vector2 offset=IntegerAnchor(i);
            positions[i]=new(offset.X+value.X,-(offset.Y+value.Y));changed.Add(i);
        }
        void Resize(int i,Vector2 ratio)
        {
            Vector2 old=sizes[i],position=TopLeft(i);
            sizes[i]=new(unchecked((short)(int)(old.X*ratio.X)),unchecked((short)(int)(old.Y*ratio.Y)));
            SetTopLeft(i,position);
            Vector2 delta=sizes[i]-old,anchor=Anchor((byte)Nodes[i].Value48)*2;
            foreach(var child in Nodes.Where(n=>n.ParentIndex==i))
            {
                int c=child.Index;Vector2 point=TopLeft(c),childRatio=ratio;
                if(preserveMargins)
                {
                    if(sizes[c].X==0||sizes[c].Y==0)throw new InvalidDataException("Source margin resize requires nonzero child dimensions.");
                    childRatio=new(Math.Max((sizes[c].X+delta.X)/sizes[c].X,0),Math.Max((sizes[c].Y+delta.Y)/sizes[c].Y,0));
                    SetTopLeft(c,point-new Vector2((int)(anchor.X*delta.X)/2,(int)(anchor.Y*delta.Y)/2));
                }
                else SetTopLeft(c,new((int)(point.X*ratio.X),(int)(point.Y*ratio.Y)));
                Resize(c,childRatio);
            }
        }
    }

    internal Vector2 PolygonPoint(int index,Vector2 sourcePoint,Vector2 mount=default)
    {
        var node=Nodes[index];
        if(node.Type!=8)throw new ArgumentException("Polygon coordinates require a type-8 node.",nameof(index));
        // Mesh positions are source Y-up offsets about the pane's own anchor,
        // whereas transforms map pane top-left coordinates into the UI plane.
        Vector2 anchor=Anchor((byte)node.Value48)*new Vector2(node.Value44,node.Value46);
        return transforms[index]*(anchor+new Vector2(sourcePoint.X,-sourcePoint.Y))+mount;
    }
    private static Vector2 Anchor(byte value)
    {
        int x = value >> 4, y = value & 15;
        if (x > 2 || y > 2) throw new InvalidDataException("Unsupported source UI anchor.");
        return new(x * .5f, y * .5f);
    }

    internal Control Art(Control parent, int index, Rect2 rectangle, UiAssets assets, Shader shader,
        MapCornerColors? colors = null, string? commonSpriteOverride = null, bool expansionCommonSprite = false,
        UiSourceSprite.Blend blend = UiSourceSprite.Blend.Ordinary)
    {
        var node = Nodes[index];
        var transform=transforms[index];
        bool projected = Math.Abs(transform.X.Y)>0.00001f||Math.Abs(transform.Y.X)>0.00001f;
        if(Math.Abs(node.HeaderValues[2])>0.00001f || projected && node.Type != 1)
            throw new InvalidDataException($"UI art requires an unsupported projected transform: {node.Name}.");
        var source = LcskSpriteNodeReader.Read(Source.Layout, node);
        var group = new Control { Name = node.Name, Position = rectangle.Position, Size = rectangle.Size,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        parent.AddChild(group);
        Node canvas = group;
        Vector2 drawSize = rectangle.Size;
        if (projected)
        {
            // Simple source panes keep their authored planar projection and UVs.
            // The rectangle is its screen-space bound, not the unrotated quad.
            Vector2 ratio = rectangle.Size / rectangles[index].Size;
            var projection = new Node2D { Name = "SourceProjection", Transform = new(
                transform.X * ratio, transform.Y * ratio,
                (transform.Origin - rectangles[index].Position) * ratio) };
            group.AddChild(projection); canvas = projection; drawSize = nodeSizes[index];
        }
        else
        {
            Vector2 orientation=new(Math.Sign(transform.X.X),Math.Sign(transform.Y.Y));
            group.Scale=orientation;
            group.Position+=new Vector2(orientation.X<0?rectangle.Size.X:0,orientation.Y<0?rectangle.Size.Y:0);
        }
        group.SetMeta("source_node_index", index);
        group.SetMeta("source_layout", Source.Source.ResourceUri);
        float[] xs = [0, drawSize.X], ys = [0, drawSize.Y];
        if (node.Type == 3)
        {
            var m = source.SourceMargins;
            Vector2 scale = new(Math.Abs(transform.X.X),Math.Abs(transform.Y.Y));
            if (m.Any(v => v < 0) || (m[2] + m[3])*scale.X > rectangle.Size.X || (m[0] + m[1])*scale.Y > rectangle.Size.Y)
                throw new InvalidDataException($"Source UI nine-part margins exceed {node.Name}.");
            xs = [0, m[2]*scale.X, rectangle.Size.X - m[3]*scale.X, rectangle.Size.X];
            ys = [0, m[0]*scale.Y, rectangle.Size.Y - m[1]*scale.Y, rectangle.Size.Y];
        }
        foreach (var segment in source.Groups)
        {
            if (segment.ComponentCount == 0) continue;
            if (segment.ComponentCount != 1) throw new InvalidDataException("UI sprite composition requires its own renderer.");
            int x = node.Type == 3 ? segment.Index / 3 : 0, y = node.Type == 3 ? segment.Index % 3 : 0;
            // A declared zero-width border has degenerate UVs as well. It
            // contributes no geometry; keep validation for every visible part.
            if(xs[x+1]==xs[x]||ys[y+1]==ys[y])continue;
            var part = segment.Parts.Single();
            // Some authored nine-part corners mirror a source region. Keep
            // corner colours attached to geometry and reverse only the UVs.
            var uv=part.Uv;
            bool flipU=uv.BottomRight.U<uv.TopLeft.U,flipV=uv.BottomRight.V<uv.TopLeft.V;
            if(flipU)uv=new(uv.TopRight,uv.TopLeft,uv.BottomRight,uv.BottomLeft);
            if(flipV)uv=new(uv.BottomLeft,uv.BottomRight,uv.TopLeft,uv.TopRight);
            bool constantU=uv.BottomRight.U==uv.TopLeft.U,constantV=uv.BottomRight.V==uv.TopLeft.V;
            var sampledUv=uv;
            if(constantU||constantV)
            {
                // Authored nine-part centres can sample a single source texel
                // or line while filling a nonzero geometry rectangle. A zero
                // AtlasTexture region would discard that geometry in Godot.
                if(uv.TopLeft.V!=uv.TopRight.V||uv.BottomLeft.V!=uv.BottomRight.V||
                    uv.TopLeft.U!=uv.BottomLeft.U||uv.TopRight.U!=uv.BottomRight.U)
                    throw new InvalidDataException("Source constant-UV sprite requires a rectangular sampling region.");
                uv=new(new(0,0),new(1,0),new(0,1),new(1,1));
            }
            // Runtime common-sprite rebinding replaces the texture description;
            // it preserves the authored geometry, margins and local UVs.
            Texture2D texture=commonSpriteOverride is null
                ?assets.SrgbLayoutSpritePart(Source,part.SpriteName,uv)
                :assets.SrgbCommonSpritePart(commonSpriteOverride,uv,expansionCommonSprite);
            var image = new UiSourceSprite { Position = new(xs[x], ys[y]), Size = new(xs[x + 1] - xs[x], ys[y + 1] - ys[y]) };
            canvas.AddChild(image);
            image.Configure(shader,texture,blend,colors);
            if(constantU||constantV)image.SetSamplingRectangle(new(new(sampledUv.TopLeft.U,sampledUv.TopLeft.V),
                new(sampledUv.BottomRight.U-sampledUv.TopLeft.U,sampledUv.BottomRight.V-sampledUv.TopLeft.V)));
            image.SetUvFlip(flipU,flipV);
        }
        return group;
    }

    internal UiSourceText Text(Control parent, int index, Vector2 mount, string value, MapFont font,
        MapTextPreset? preset = null, Rect2? rectangle = null)
    {
        Rect2 box = rectangle ?? Rectangle(index, mount);
        var profile = MapFontProfile.FromNode(Nodes[index], Source.Source.ResourceUri);
        if (preset is not null) profile = profile.WithPreset(preset);
        var text = new UiSourceText { Name = Nodes[index].Name, Position = box.Position, Size = box.Size };
        parent.AddChild(text);
        text.Configure(font, profile);
        text.Text = value;
        return text;
    }
}
