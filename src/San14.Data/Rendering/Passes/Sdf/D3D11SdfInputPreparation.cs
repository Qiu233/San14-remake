namespace San14.Data;

/// <summary>The actual normal Main construction exported to its native SDF
/// owner. Resource declarations and signed Host overrides keep separate roles.
/// This is a fresh default-allocator recipe, not a live count observation.</summary>
public sealed class D3D11SdfInputPreparation
{
    private D3D11SdfInputPreparation(MainMapContextOwnerSource host,
        CommonRenderSettings common, LoadedOrdinaryRsLayerSource source,
        OrdinarySdfOperation1ConfigurationSource configuration,
        OrdinarySdfHostSlotOverrides overrides, OrdinarySdfHostPoolFields objectPool,
        SdfBackendShaderResources shaders)
        => (Host,Common,Source,Configuration,Overrides,ObjectPool,Shaders) =
            (host,common,source,configuration,overrides,objectPool,shaders);

    public MainMapContextOwnerSource Host { get; }
    public CommonRenderSettings Common { get; }
    public LoadedOrdinaryRsLayerSource Source { get; }
    public OrdinarySdfOperation1ConfigurationSource Configuration { get; }
    public OrdinarySdfHostSlotOverrides Overrides { get; }
    public OrdinarySdfHostPoolFields ObjectPool { get; }
    public SdfBackendShaderResources Shaders { get; }

    public static D3D11SdfInputPreparation PrepareFreshNormalMain(
        GameResourceRepository repository, MainMapContextOwnerSource actualFreshHost,
        LoadedDefaultView0Source actualView, LoadedObjects actualCommon)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(actualFreshHost);
        var source = OrdinaryRsLayerSource.Load(repository,actualFreshHost.Locale);
        var configuration = OrdinarySdfOperation1ConfigurationSource.Load(actualView,actualCommon);
        return new(actualFreshHost,CommonRenderResources.ImportSettings(actualCommon),source,configuration,
            OrdinarySdfOperation1ConfigurationSource.ReadFreshNormalHostSlotOverrides(actualFreshHost),
            OrdinarySdfHostPoolConfigurationSource.Load(repository,actualFreshHost.Locale).NormalStartupHostFields,
            SdfBackendShaderResources.Load(repository,source));
    }

    public void WriteNativeInputs(string directory)
    {
        Directory.CreateDirectory(directory);
        using (var writer = new BinaryWriter(File.Create(Path.Combine(directory,"normal-source.bin"))))
        {
            writer.Write(0x31464453u); writer.Write(1u); // SDF1 / resource-packet version
            writer.Write(Host.ContextFlags3E4);
            writer.Write(Overrides.SlotCapacity748);
            writer.Write(Common.Flags10);
            writer.Write(Configuration.SourceMode);
            writer.Write(Configuration.SettingsSlotCapacity);
            writer.Write(Configuration.SettingsRecordCapacity);
            writer.Write(Configuration.VolumeGate);
            writer.Write(ObjectPool.Host724); writer.Write(ObjectPool.Host728);
            writer.Write(Source.ConstructFreshSdfNumericState().State40);
            // TC and SC actual normal cold-default allocation calls memset0
            // for the whole operation0 object. No reuse/custom path uses this writer.
            writer.Write(1u);
            var oc = RenderTextureResources.Read(Source.Objects,Source.PreparationNode2.OcTargetObjectNameHash);
            if (oc.Kind != RenderTextureKind.Texture2D || oc.MultiSample != 0)
                throw new NotSupportedException("native-map.sdf.current_OC_texture_kind");
            // Only the resource and SRV rows are consumed here. OC's output
            // color-space field belongs to its separate RTV path; requesting
            // that unused alternate RTV would incorrectly reject float OC.
            var formats = RenderTextureFormatMath.Resolve(oc.PixelFormat,oc.ColorSpace,0);
            writer.Write(oc.Width); writer.Write(oc.Height);
            writer.Write(formats.ResourceFormat); writer.Write(formats.ShaderViewFormat);
            writer.Write((uint)oc.LevelNum);
        }
        // The actual SdfOcclusion declaration selects this source G1S. Original
        // program0 is its vertex-id fullscreen stage used by the fallback.
        var shader = Shaders.SdfOcclusionPrograms;
        if (shader.Source.FileId != SdfFinalProducerPlan.ShaderFileId)
            throw new InvalidDataException("SDF source fullscreen program belongs to another declaration.");
        File.WriteAllBytes(Path.Combine(directory,"fullscreen-vs.dxbc"),
            shader.Container.GetProgramBytes(shader.Source.Bytes,0).ToArray());
    }
}
