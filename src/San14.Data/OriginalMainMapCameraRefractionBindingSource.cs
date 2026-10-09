using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Exact Main/View0 source F103 CPU50 resource and encoded23 setter
/// route. A declaration is not a current descriptor. Its device publisher must
/// resolve the same accepted frame's actual ready B6169FDB shader view.</summary>
public sealed class OriginalMainMapCameraRefractionBindingSource
{
    private sealed record LoadedDepthDeclaration(LoadedRdbAsset Graph, LoadedRdbAsset Objects,
        int NodeIndex, int CombinationIndex, int PixelProgramIndex, OriginalDeferredTargetImageSource Depth);
    private sealed class LoadedShaderDeclarations
    {
        public readonly object Gate = new();
        public readonly List<LoadedDepthDeclaration> Depths = new();
    }
    private static readonly ConditionalWeakTable<LoadedRdbAsset, LoadedShaderDeclarations> LoadedDeclarations = new();
    private OriginalMainMapCameraRefractionBindingSource(OriginalDeferredF103SelectedSource selection,
        OriginalDeferredTargetImageSource depth)
    { Selection=selection;RefractionDepth=depth; }
    public OriginalDeferredF103SelectedSource Selection { get; }
    public OriginalDeferredTargetImageSource RefractionDepth { get; }
    public long FrameNumber=>Selection.Current.FrameNumber;
    public short NativeSetterParameter=>0x23;
    public int CpuResourceOffset=>0x50;
    public int RefractionModuleDescriptorOffset=>0x48;
    public string SourceProvenance=>Selection.Camera.FrameProvenance+";"+Selection.Current.CurrentProducerProvenance+
        ";node166-field11-F744A94F/B6169FDB -> namedData -> CPU50 -> DataCPU.V28 -> F103-encoded23 -> Refraction48;"+
        "actual-ready-descriptor-publication-required;no-declaration-only-presence";

    public static OriginalMainMapCameraRefractionBindingSource ReadSource(OriginalDeferredF103SelectedSource selection)
    {
        ArgumentNullException.ThrowIfNull(selection);selection.Validate();
        var plan=selection.SourcePlan;var reference=plan.ShaderReference;
        if(selection.Camera.Source.Camera.ObjectId!=OriginalMainMapCameraAdapter.MainCameraObjectId||plan.ViewId!=0||
            selection.Current.NativeColorMode!=0x60||plan.Node.Index!=166||
            reference.ShaderObject.ClassId!=OriginalRenderGraphShaderReferences.ShaderObjectClassId||
            reference.ShaderObject.Kind!=DokObjectKind.Inline||
            reference.ShaderObject.ObjectId!=0xC96F6A61||reference.ShaderFileHash!=0x9D8E811F||
            reference.G1SReference.NameHash!=OriginalRenderGraphShaderReferences.G1SFileResourceHashProperty||
            reference.G1SReference.TypeCode!=5||reference.G1SReference.ElementCount!=1||
            reference.G1SReference.RawValue.Length!=4||
            BinaryPrimitives.ReadUInt32LittleEndian(reference.G1SReference.RawValue)!=0x9D8E811F||
            plan.Shader.Source.OverrideSource is not null||plan.Shader.Source.ResourceUri!="san14://rdb/system/9D8E811F")
            throw new InvalidDataException("Refraction binding source requires the actual source-created Main38D/View0/Class868/Color60 frame.");
        // The graph, DOK and selected program declarations belong to their
        // loaded asset lifetime. Camera/frame/descriptor readiness is current;
        // unchanged loaded bytes do not need a second import on every request.
        var loaded=LoadedDeclarations.GetValue(plan.Shader.Source, static _=>new());
        OriginalDeferredTargetImageSource depth;
        lock(loaded.Gate)
        {
            var found=loaded.Depths.SingleOrDefault(item=>ReferenceEquals(item.Graph,plan.Graph.Source)&&
                ReferenceEquals(item.Objects,plan.TextureObjects.Source)&&item.NodeIndex==plan.Node.Index&&
                item.CombinationIndex==selection.Combination.Index&&item.PixelProgramIndex==selection.PixelProgramIndex);
            if(found is not null)depth=found.Depth;
            else
            {
                depth=OriginalMapSelectedSourceImagePlan.Read(selection).GraphTargets.Single(t=>t.Role=="RefractionDepth");
                loaded.Depths.Add(new(plan.Graph.Source,plan.TextureObjects.Source,plan.Node.Index,
                    selection.Combination.Index,selection.PixelProgramIndex,depth));
            }
        }
        var field=plan.Node.Call.Arguments[11];
        if(depth.EncodedParameter!=0x23||depth.SourceArgument.NameHash!=field.NameHash||
            depth.SourceArgument.DescriptorWord!=field.DescriptorWord||
            !depth.SourceArgument.RawValue.AsSpan().SequenceEqual(field.RawValue)||field.NameHash!=0xF744A94F||
            field.DescriptorWord!=0x05000001||field.RawValue.Length!=4||
            BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue)!=0xB6169FDB||
            depth.Texture.Object.ObjectId!=0xB6169FDB||depth.Texture.Object.ClassId!=0x72233E17||
            depth.Texture.Kind!=OriginalRenderTextureKind.Texture2D||depth.Texture.LevelNum!=1||
            depth.Texture.PixelFormat!=0x4E||depth.ResourceMapping.PrimaryEncoded!=0x23||
            depth.ResourceMapping.Name!="sDepthMap"||depth.PixelBinding.Name.Text!="sDepthMap"||
            depth.PixelBinding.Dimension!=4||depth.PixelBinding.ReturnType!=5)
            throw new InvalidDataException("Main refraction resource no longer follows the exact named Data/CPU50/encoded23 source route.");
        return new(selection,depth);
    }

    public void ValidateTarget(OriginalDeferredAuxiliaryTargetSource actualTarget,long frameNumber)
    {
        ArgumentNullException.ThrowIfNull(actualTarget);OriginalDeferredAuxiliaryTargetSources.Validate(actualTarget);
        Selection.Validate();
        if(frameNumber!=FrameNumber||actualTarget.Texture.Object.ObjectId!=RefractionDepth.Texture.Object.ObjectId||
            actualTarget.Texture.Source.Source.ResourceUri!=RefractionDepth.Texture.Source.Source.ResourceUri||
            actualTarget.Texture.Source.Source.DecodedSha256!=RefractionDepth.Texture.Source.Source.DecodedSha256||
            actualTarget.Texture with{Source=RefractionDepth.Texture.Source,Object=RefractionDepth.Texture.Object}
                !=RefractionDepth.Texture)
            throw new InvalidDataException("Ready refraction descriptor must belong to the same accepted frame and exact source B6169FDB owner.");
    }
}
