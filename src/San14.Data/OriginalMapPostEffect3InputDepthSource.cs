using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Exact node193 Apply argument and original CPU getter route. This
/// declaration does not initialize pixels or substitute RefractionDepth.</summary>
public sealed class LoadedOriginalMapPostEffect3InputDepthSource
{
    internal LoadedOriginalMapPostEffect3InputDepthSource(LoadedRenderGraph graph,
        OriginalRenderTextureDefinition texture,LoadedOriginalDeferredSamplerNativeSource native,string evidence)
        =>(Graph,Texture,Native,SourceEvidence)=(graph,texture,native,evidence);
    public LoadedRenderGraph Graph{get;}
    public OriginalRenderTextureDefinition Texture{get;}
    public LoadedOriginalDeferredSamplerNativeSource Native{get;}
    public string SourceEvidence{get;}
    public uint TextureObjectId=>0xE13DDB4F;
    public bool CompositeGBuffersArgumentIsAbsent=>true;
    public void RequireSelectedOwnedDepthDescriptor(OriginalRenderTextureRuntimeDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var d=descriptor.Declaration;
        if(d.Object.ObjectId!=TextureObjectId||d.Source.Source.ResourceUri!=Texture.Source.Source.ResourceUri||
            d.Source.Source.DecodedSha256!=Texture.Source.Source.DecodedSha256||
            descriptor.Kind!=OriginalRenderTextureKind.Texture2D||descriptor.LevelNum!=1||
            descriptor.MinMipLevel!=0||descriptor.MultiSample!=0||
            string.IsNullOrWhiteSpace(descriptor.OwnerIdentity))
            throw new InvalidDataException("Apply InputDepth wrapper has another source resource, mip/sample contract or owner.");
        var format=OriginalRenderTextureFormatMath.Resolve(descriptor.PixelFormat,descriptor.ColorSpace,descriptor.OutputColorSpace);
        if(format.TargetViewFormat!=20||format.ShaderViewFormat!=21)
            throw new NotSupportedException("This InputDepth capability supports the actual E13 D32/S8 source view only.");
    }
}

public static class OriginalMapPostEffect3InputDepthSource
{
    public static LoadedOriginalMapPostEffect3InputDepthSource Load(OriginalGameRepository repository,string locale="TC")
        =>OriginalDeferredSamplerSources.ReadPostEffect3InputDepthSource(repository,
            OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale));
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapPostEffect3InputDepthSource ReadPostEffect3InputDepthSource(
        OriginalGameRepository repository,LoadedOriginalDeferredSamplerNativeSource native)
    {
        if(native.Locale is not ("TC" or "SC"))throw new NotSupportedException("Apply InputDepth factory/getter windows are verified for SC/TC.");

        var graph=repository.LoadRenderGraph("system",0x024496EE);
        if(graph.Document.Nodes.Count<=193||graph.Document.Nodes[193].Call.FactoryClassId!=0xB2D30063)
            throw new InvalidDataException("Apply InputDepth needs the original node193 source graph.");
        var args=graph.Document.Nodes[193].Call.Arguments;
        uint Word(int index,uint name)
        {
            if(args.Count!=13||args[index].NameHash!=name||args[index].DescriptorWord!=0x05000001||args[index].RawValue.Length!=4)
                throw new InvalidDataException("Original InputDepth factory argument index/name/type differs.");
            return BinaryPrimitives.ReadUInt32LittleEndian(args[index].RawValue);
        }
        if(Word(4,0x56AB46F0)!=0xE13DDB4F||Word(12,0xCA398DA4)!=0)
            throw new NotSupportedException("Apply depth requires source arg4 E13 and explicit arg12 GBuffers absent.");
        var texture=OriginalRenderTextureResources.Read(repository.LoadObjects("system",0x93FB3280),0xE13DDB4F);
        return new(graph,texture,native,native.ResourceUri+$";{native.Locale}:paired-source-profile;"+
            ";arg4-depth;-caller40/-callee48->CPU38"+
            "-arg12-zero/-null/-caller60/-callee68->CPU60-null"+
            "-CPU60-null-selects-CPU38;-owned-success;-current-wrapper;->rbp50;-InputDepth"+
            graph.Source.ResourceUri+"@"+graph.Source.DecodedSha256+";source-arg4=E13DDB4F;arg12=0");
    }
}
