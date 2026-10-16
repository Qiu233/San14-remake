using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The initial Scene attachment and its selected physical terminal.
/// Logical references keep the attachment name; all device consumers borrow
/// the terminal's format and extent. A retained or retargeted proxy needs a
/// new preparation from its actual current owner.</summary>
public sealed class SceneColorTargetResources
{
    private SceneColorTargetResources(uint logicalName,RenderTextureDefinition texture,int width,int height)
    {
        LogicalName=logicalName;Texture=texture;
        Formats=RenderTextureFormatMath.Resolve(texture);
        Width=DeferredAuxiliaryTargetSources.ResolveNativeDimension(texture.Width,width);
        Height=DeferredAuxiliaryTargetSources.ResolveNativeDimension(texture.Height,height);
    }

    public uint LogicalName { get; }
    public RenderTextureDefinition Texture { get; }
    public RenderTextureFormats Formats { get; }
    public int Width { get; }
    public int Height { get; }

    /// <summary>Prepare once before native allocation, using the actual Main
    /// camera and the same initial Common settings as the fresh season caller.
    /// These settings do not stand in for a later runtime Common publication.</summary>
    public static SceneColorTargetResources PrepareFresh(GameResourceRepository repository,
        MapCameraConstructionPacket camera,CommonRenderSettings common,int width,int height)
    {
        var graph=repository.LoadRenderGraph("system",0x024496EE);
        var textures=repository.LoadObjects("system",0x93FB3280);
        var attachment=RenderGraphTargetResources.Read(graph,73,textures).ColorTargets.Single();
        var proxy=graph.Document.Nodes[1];
        if(proxy.Call.FactoryClassId!=0x5F0A8446)
            throw new InvalidDataException("Scene selection requires its declared proxy factory.");
        byte[] Argument(uint name,uint descriptor,int bytes)
        {
            var argument=proxy.Call.Arguments.Single(value=>value.NameHash==name);
            if(argument.DescriptorWord!=descriptor||argument.RawValue.Length!=bytes)
                throw new InvalidDataException("Scene proxy requires its declared typed argument.");
            return argument.RawValue;
        }
        uint Name(uint hash)=>BinaryPrimitives.ReadUInt32LittleEndian(Argument(hash,0x05000001,4));
        if(Argument(0x9075EDA0,0x01000001,1)[0]!=0||
            Name(0x72DB7EBD)!=DefaultView0Source.ViewObjectId||
            camera.Source.Camera.ObjectId!=DefaultView0Source.MainCameraObjectId||
            Name(0x51332A47)!=attachment.ObjectNameHash)
            throw new NotSupportedException("Fresh Scene selection requires the actual Main camera and color attachment.");
        int selector=BinaryPrimitives.ReadInt32LittleEndian(camera.Prefix.Span[0x1CC..]);
        uint high=Name(0x514B2AC3);
        uint? selected=MapSceneProxySourcePolicy.ReadSourceCreatedFreshTerminal(selector,
            common.Flags8,common.Flags0C,high,Name(0x0EBBA230),Name(0xD776D144));
        if(selected==high)
            throw new NotSupportedException("The high Scene terminal needs its own device-format recipe.");
        var texture=selected is uint name?RenderTextureResources.Read(textures,name):attachment.Texture;
        if(attachment.Slot!=0||attachment.SurfaceId!=0||texture.Kind!=RenderTextureKind.Texture2D||texture.MultiSample!=0)
            throw new NotSupportedException("Native Scene allocation requires a single-sample two-dimensional color surface.");
        return new(attachment.ObjectNameHash,texture,width,height);
    }
}
