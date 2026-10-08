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
        byte[] exe=File.ReadAllBytes(native.PhysicalPath);VerifyHash(exe,native.ExecutableSha256,"InputDepth executable");
        var image=new NativeImage(exe);byte[] text=image.DecodeText(out int start);
        VerifyHash(text,native.DecodedTextSha256,"InputDepth decoded text");
        void Check(int rva,int n,string hash)
        {
            var actual=OriginalMapPostEffect3NativeSourceProfile.CodeWindow(native.Locale,"OriginalMapPostEffect3InputDepthSource.cs",rva,n,hash);
            VerifyHash(text.AsSpan(actual.Rva-start,n),actual.Sha,"InputDepth source route");
        }
        Check(0xE7C0B6,0x31,"632027A3F1A61FF152F696F0C034A24F8736B2E5CACF4E3AA63AE79A0ACCACC7");
        Check(0xE7C054,0x62,"321F8D2E1ABD6CBE764EFDE7FB7B16D736014C6CBBF42C3E1516011845C944EE");
        Check(0xE7C3ED,0x6F,"7A5C3850086B70B0AE5E123BBDB3CB7057EF86179B6FBEA1CA8F65203C55EFFA");
        Check(0xE6BA2E,0x3A,"28739B785C5D5F41952E576C88F3B7BE7960ACD7F5AE454D348F2D0B0792976C");
        Check(0xEB67EC,0x8C,"C7141684155DF5231CB555EF20BE18F91C10B07B3434F7DF8BCF3538E1A8E36B");
        Check(0xEB69F5,0x33,"BAE6990B2D9A455EE1B8592B593305C99797DE57AF71614FB0E9DE513801FB80");
        var graph=repository.LoadRenderGraph("system",0x024496EE);
        if(graph.Source.OverrideSource is not null||graph.Source.DecodedSha256!="B9AE1EC8209C067DBC73084CDC0EB149A65E282C25D4B8AD398F43071072971D"||
            graph.Document.Nodes.Count<=193||graph.Document.Nodes[193].Call.FactoryClassId!=0xB2D30063)
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
        return new(graph,texture,native,native.ResourceUri+$";{native.Locale}:paired-source-profile;TC-reference-role-anchors;"+
            ";TC:E7C0B6..E7C0DA-arg4-depth;E7C432-caller40/E6BA37-callee48->CPU38;"+
            "E7C054-arg12-zero/E7C0A9-null/E7C412-caller60/E6BA5F-callee68->CPU60-null;"+
            "EB67EC/EB681E-CPU60-null-selects-CPU38;D9B580-owned-success;C0BB30-current-wrapper;EB686B->rbp50;EB6A23-InputDepth;"+
            graph.Source.ResourceUri+"@"+graph.Source.DecodedSha256+";source-arg4=E13DDB4F;arg12=0");
    }
}
