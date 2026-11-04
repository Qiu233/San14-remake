using System.Buffers.Binary;
using San14.Data;
using San14.Data.Formats;

/// <summary>The actual five ordinary PC action endpoints. This is a retained
/// art pose consumer, not the separate PS4/animation playback controller.</summary>
internal sealed class UiSourceButtonStateProfile
{
    private static readonly string[] Names = ["0_normal", "1_focus", "2_push", "3_disable", "4_disable_focus"];
    private readonly LoadedLayout layout;
    private readonly IReadOnlyList<LcskNodeRecord> nodes;
    private readonly byte[][] alphas;
    private readonly Dictionary<int,uint> blends=[];
    internal IReadOnlyList<(string Name,float Start,float End)> Actions { get; }

    internal UiSourceButtonStateProfile(LoadedLayout layout, IReadOnlyList<string>? actionNames = null)
    {
        var names=actionNames??Names;
        if(names.Count!=5)throw new ArgumentException("Button profile requires five source actions.",nameof(actionNames));
        this.layout=layout;nodes=LcskNodeReader.Read(layout.Layout);
        var clips=LcskAnimationReader.Read(layout.Layout,names.ToHashSet(StringComparer.Ordinal))
            .ToDictionary(clip=>clip.Name,StringComparer.Ordinal);
        alphas=new byte[names.Count][];
        var actions=new List<(string,float,float)>();
        for(int state=0;state<names.Count;state++)
        {
            if(!clips.TryGetValue(names[state],out var clip))
                throw new InvalidDataException("button.state_art.missing_exact_action."+names[state]);
            actions.Add((clip.Name,clip.StartFrame,clip.EndFrame));
            var own=nodes.Select(AuthoredAlpha).ToArray();
            foreach(var track in clip.Tracks)
            {
                if(track.Channel!=10)throw new InvalidDataException("button.state_art.non_alpha_channel");
                if(track.Curve.Keys.Count==0)continue;
                own[track.NodeId]=Alpha((float)track.Curve.Sample(clip.EndFrame));
            }
            var effective=new byte[nodes.Count];
            for(int node=0;node<nodes.Count;node++)effective[node]=Effective(node);
            alphas[state]=effective;
            byte Effective(int node)
            {
                var pane=nodes[node];
                if(pane.ParentIndex is not int parent||!PropagatesAlpha(nodes[parent]))return own[node];
                //ACB280 uses the refreshed parent's byte, not its unquantized
                //float or a Canvas-wide alpha multiplied after composition.
                return (byte)(Effective(parent)/255f*(own[node]/255f)*255f);
            }
        }
        Actions=actions.AsReadOnly();
    }

    internal byte AlphaAt(int state,int node)=>alphas[state][node];

    internal uint BlendAt(int node)
    {
        if(blends.TryGetValue(node,out uint retained))return retained;
        var art=LcskSpriteNodeReader.Read(layout.Layout,nodes[node]);
        uint? blend=null;
        foreach(var group in art.Groups.Where(group=>group.ComponentCount!=0))
        {
            var raw=group.RawRenderBytes.AsSpan();
            uint current=BinaryPrimitives.ReadUInt32LittleEndian(raw[4..]);
            if(current is not (0x00554400 or 0x00114400)||blend is uint previous&&previous!=current||
                BinaryPrimitives.ReadSingleLittleEndian(raw[8..])!=1||group.ComponentCount!=1||
                group.Parts[0].RawBytes.AsSpan(6,3).IndexOfAnyExcept((byte)0)>=0)
                throw new InvalidDataException("button.state_art.unsupported_slice_material."+nodes[node].Name);
            blend=current;
        }
        return blends[node]=blend??0x00554400;
    }

    private static byte AuthoredAlpha(LcskNodeRecord node)=>node.Type switch
    {
        0=>Alpha(BinaryPrimitives.ReadSingleLittleEndian(node.RawBytes.AsSpan(72,4))),
        1=>Alpha(BinaryPrimitives.ReadSingleLittleEndian(node.RawBytes.AsSpan(88,4))),
        // This family has white Type3 tint and unit slice opacity. Text keeps
        // its own caption/preset consumer; only its Type0 ancestors animate.
        2 or 3=>255,
        _=>throw new InvalidDataException("button.state_art.unsupported_node."+node.Name)
    };

    private static bool PropagatesAlpha(LcskNodeRecord parent)=>parent.Type switch
    {
        0=>(BinaryPrimitives.ReadUInt32LittleEndian(parent.RawBytes.AsSpan(64,4))&1)!=0,
        1=>(BinaryPrimitives.ReadUInt32LittleEndian(parent.RawBytes.AsSpan(80,4))&1)!=0,
        _=>throw new InvalidDataException("button.state_art.unsupported_alpha_parent."+parent.Name)
    };

    private static byte Alpha(float value)
    {
        if(!float.IsFinite(value))throw new InvalidDataException("button.state_art.nonfinite_alpha");
        return (byte)(Math.Clamp(value,0,1)*255f);
    }
}
