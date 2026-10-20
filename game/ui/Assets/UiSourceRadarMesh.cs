using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The six-vertex, texture-free radar's verified ordinary stage-zero
/// branch. Only opaque uniform palettes and unmodified ancestor color apply.</summary>
internal partial class UiSourceRadarMesh : Control
{
    private UiSourceLayout source=null!;
    private int node;
    private int[] indices=[];
    private Vector2[] points=[];
    private Color[] colours=[];
    private Shader? shader;
    private ShaderMaterial? material;
    internal IReadOnlyList<Vector2> Points=>points;
    internal IReadOnlyList<int> Indices=>indices;

    internal void Configure(UiSourceLayout source,int node,AreaMarkerColor palette)
    {
        var record=source.Nodes[node];
        var mesh=LcskPolygonNodeReader.Read(source.Source.Layout,record);
        if(mesh.Vertices.Count!=6||mesh.Indices.Count!=15||record.RawBytes[98]!=0||palette.Alpha!=255)
            throw new InvalidDataException("Radar renderer requires the opaque six-point stage-zero source branch.");
        this.source=source;this.node=node;indices=mesh.Indices.ToArray();
        colours=Enumerable.Repeat(new Color(palette.Red/255f,palette.Green/255f,palette.Blue/255f,1),6).ToArray();
        MouseFilter=MouseFilterEnum.Ignore;
        shader=new Shader {ResourceName="OriginalUiRadarStageZero",Code="""
            shader_type canvas_item;
            render_mode unshaded, blend_disabled;
            varying vec4 vertex_colour;
            void vertex() { vertex_colour = COLOR; }
            vec3 encode_srgb(vec3 v) {
                return mix(1.055 * pow(max(v, vec3(0.0)), vec3(1.0 / 2.4)) - 0.055,
                    v * 12.92, lessThanEqual(v, vec3(0.0031308)));
            }
            void fragment() { COLOR = vec4(encode_srgb(vertex_colour.rgb), 1.0); }
            """};
        material=new ShaderMaterial {Shader=shader};Material=material;
        SetMeta("source_node_index",node);
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }
    internal void ApplyValues(IReadOnlyList<int>? values,IReadOnlyList<int> maxima)
    {
        Visible=values is not null;
        points=values is null?[]:UiSourceRadarGeometry.Positions(source,node,values,maxima);
        QueueRedraw();
    }
    public override void _Draw()
    {
        if(points.Length==6)RenderingServer.CanvasItemAddTriangleArray(GetCanvasItem(),indices,points,colours);
    }
    public override void _ExitTree()
    {
        Material=null;material?.Dispose();shader?.Dispose();
    }
}
