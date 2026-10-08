using San14.Data;

/// <summary>A source Static Data whose actual device image has completed
/// preparation. The caller retains the physical image and supplies its own
/// lifecycle check; this entry never loads an image or certifies a Boolean.</summary>
public sealed record OriginalPatternCameraStaReadyPlaneEntry(OriginalStaticTextureBinding Source,
    OriginalSky2DeviceInput CurrentDescriptor, uint NativeViewFormat, Action RequireCurrentOwner);

/// <summary>Real source-owned StaticSky2AtmoTable Data. It owns registered raw
/// contexts, exact prepared CPU10/20 and18/28 copies, and a retained complete
/// Plane Data. Loading numeric sources alone does not make this Data ready.</summary>
public sealed class OriginalPatternCameraStaDataOwner : IOriginalPatternCameraCurrentNamedData, IDisposable
{
    private readonly PreparationContext context;
    private bool retired;
    private OriginalPatternCameraStaDataOwner(LoadedOriginalSkyAtmoTableReadySource source,
        OriginalSkyAtmoPlaneDataOwner actualPlane, object actualAssetContextOwner,
        object actualPreparationContextOwner, Action requireActualContexts)
    {
        Source = source;
        context = new(source, actualPlane, actualAssetContextOwner, actualPreparationContextOwner, requireActualContexts);
        Cpu = source.CreateFreshCpu(context);
    }
    public LoadedOriginalSkyAtmoTableReadySource Source { get; }
    public OriginalSkyAtmoTableCpuOwner Cpu { get; }
    public uint ObjectKey => OriginalSkyAtmoTableResources.ObjectId;
    public uint FactoryClassId => OriginalSkyAtmoTableResources.ClassId;
    public object CurrentDataOwner { get { RequireCurrent(); return Cpu; } }
    public string SourceEvidence => Cpu.SourceEvidence + ";actual-normal-Context-owned-registered-raw-Data;Pattern-K1680-STA";

    /// <summary>Load/registration boundary. These are the current normal
    /// Context owners, not another source probe or a synthetic native Camera.</summary>
    public static OriginalPatternCameraStaDataOwner CreateFresh(LoadedOriginalSkyAtmoTableReadySource source,
        OriginalSkyAtmoPlaneDataOwner actualPlane, object actualAssetContextOwner,
        object actualPreparationContextOwner, Action requireActualContexts)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(actualPlane);
        ArgumentNullException.ThrowIfNull(actualAssetContextOwner); ArgumentNullException.ThrowIfNull(actualPreparationContextOwner);
        ArgumentNullException.ThrowIfNull(requireActualContexts);
        return new(source, actualPlane, actualAssetContextOwner, actualPreparationContextOwner, requireActualContexts);
    }
    public OriginalSkyAtmoReadyResult PrepareCurrent()
    { ObjectDisposedException.ThrowIf(retired, this); return Cpu.PrepareCurrent(); }
    public void RequireCurrent()
    { ObjectDisposedException.ThrowIf(retired, this); Cpu.RequireCurrent(); }

    /// <summary>Actual four-entry Static descriptor adoption. Entry2 is
    /// required even though the Cloud program consumes only3/1/0. All image,
    /// view, sampler and lifetime owners are supplied by the normal device
    /// builder; there is no fallback or image allocation here.</summary>
    public static OriginalSkyAtmoPlaneDataOwner CreatePlaneFromReadyEntries(
        LoadedOriginalSkyAtmoTableReadySource source,
        IReadOnlyList<OriginalPatternCameraStaReadyPlaneEntry> actualEntries)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(actualEntries);
        if (actualEntries.Count != 4) throw new InvalidDataException("STA Plane needs its complete four actual ready Static entries.");
        var records = new IOriginalSkyAtmoCurrentStaticTextureData[4];
        for (int i = 0; i < records.Length; i++)
        {
            var actual = actualEntries[i]; var expected = source.Plane.Entries[i];
            ArgumentNullException.ThrowIfNull(actual); ArgumentNullException.ThrowIfNull(actual.RequireCurrentOwner);
            var device = actual.CurrentDescriptor;
            if (actual.Source.Definition.ObjectId != expected.Definition.ObjectId || actual.Source.ImageIndex != i ||
                actual.Source.Resource.Source.ResourceUri != expected.Resource.Source.ResourceUri ||
                actual.Source.Resource.Source.DecodedSha256 != expected.Resource.Source.DecodedSha256 ||
                device.SourceResourceUri != expected.Resource.Source.ResourceUri + "#image=" + i ||
                device.SourceDimension != (i == 0 ? 8u : 4u) || !device.ShaderView.IsValid || !device.OwnerTexture.IsValid ||
                actual.NativeViewFormat != source.Plane.ReadSourceShaderViewFormat(i) ||
                !device.OriginalSamplerDescriptor.Span.SequenceEqual(source.Plane.ReadSourceSamplerDescriptor(i)))
                throw new InvalidDataException("STA ready Plane descriptor differs from its actual source Static/TID/view/sampler.");
            actual.RequireCurrentOwner(); records[i] = new CurrentStaticData(actual);
        }
        return source.CreatePlaneData(records);
    }

    private sealed class CurrentStaticData : IOriginalSkyAtmoCurrentStaticTextureData
    {
        private readonly OriginalPatternCameraStaReadyPlaneEntry entry;
        internal CurrentStaticData(OriginalPatternCameraStaReadyPlaneEntry entry) => this.entry = entry;
        public uint ObjectKey => entry.Source.Definition.ObjectId;
        public uint FactoryClassId => 0xFF7DBFD4;
        public object CurrentCpuOwner => this;
        public object CurrentTextureDescriptor => entry.CurrentDescriptor;
        public OriginalSkyAtmoReadyResult PrepareCurrent() { RequireCurrent(); return OriginalSkyAtmoReadyResult.Ready; }
        public void RequireCurrent()
        {
            entry.RequireCurrentOwner();
            if (!entry.CurrentDescriptor.ShaderView.IsValid || !entry.CurrentDescriptor.OwnerTexture.IsValid)
                throw new InvalidDataException("Plane Static Data's prepared physical descriptor has retired.");
        }
    }
    private sealed class PreparationContext : IOriginalSkyAtmoTablePreparationContext
    {
        private readonly OriginalSkyAtmoPlaneDataOwner plane;
        private readonly Dictionary<uint, OriginalSkyAtmoRegisteredRawContext> raw;
        private readonly Action requireContexts;
        internal PreparationContext(LoadedOriginalSkyAtmoTableReadySource source, OriginalSkyAtmoPlaneDataOwner plane,
            object asset, object preparation, Action requireContexts)
        {
            this.plane = plane; this.requireContexts = requireContexts;
            AssetContextOwner = asset; PreparationContextOwner = preparation; requireContexts();
            // Actual B41860 creation and registration in the current source
            // domain. Constructor reservation order is Amb2D then TTable;
            // ready dependency indices use the prepended TTable/Amb2D list.
            raw = new() {
                [OriginalSkyAtmoTableResources.Ambient2DFileId] = source.CreateRegisteredRawContext(OriginalSkyAtmoTableResources.Ambient2DFileId),
                [OriginalSkyAtmoTableResources.TransmittanceFileId] = source.CreateRegisteredRawContext(OriginalSkyAtmoTableResources.TransmittanceFileId)
            };
        }
        public object AssetContextOwner { get; }
        public object PreparationContextOwner { get; }
        public OriginalSkyAtmoPlaneDataOwner ReadCurrentPlaneData(uint objectKey)
        {
            RequireCurrent();
            if (objectKey != plane.ObjectKey) throw new NotSupportedException("STA current Context has no supported Plane producer for this key.");
            return plane;
        }
        public OriginalSkyAtmoRegisteredRawContext? ReadRegisteredRawContext(uint key)
        { RequireCurrent(); return raw.GetValueOrDefault(key); }
        public void RequireCurrent() => requireContexts();
        public void Retire() { foreach (var value in raw.Values) value.Retire(); raw.Clear(); }
    }
    public void Dispose()
    {
        if (retired) return;
        retired = true; Cpu.Retire(); context.Retire();
        // The actual complete Plane/Static physical descriptors are borrowed.
        // Their normal device owner retires only after this Data is released.
    }
}
