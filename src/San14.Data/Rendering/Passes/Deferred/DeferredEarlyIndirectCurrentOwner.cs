using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Module-lived CPU cache for one actual2939 shader instance. The
/// source first96B initializes both variant caches. Current feature selection
/// is independent; untouched variant72 retains its actual constructor source.</summary>
public sealed class DeferredEarlyIndirectShaderState
{
    private readonly byte[] requested, committed;
    private int featureRow, variantRow;
    internal DeferredEarlyIndirectShaderState(LoadedDeferredEarlyIndirectSource source,
        LoadedDeferredEarlyIndirectPatternMapSource native, DeferredEarlyIndirectProvider provider)
    {
        Source = source; Provider = provider; Native = native;
        requested = source.ReadSourceVariant(provider, 0).ToArray(); committed = requested.ToArray();
        Pattern = native.ConstructAfterSourceFactoryPreparation();
    }
    public LoadedDeferredEarlyIndirectSource Source { get; }
    public LoadedDeferredEarlyIndirectPatternMapSource Native { get; }
    public DeferredEarlyIndirectProvider Provider { get; }
    public DeferredEarlyIndirectPatternMapState Pattern { get; }
    public ReadOnlyMemory<byte> RequestedVariant96 => requested;
    public ReadOnlyMemory<byte> CommittedVariant96 => committed;
    public ReadOnlyMemory<byte> MatchedFeature48 => Source.ReadFeatureRow(Provider, featureRow).RawBytes;
    public int CurrentFeatureRow => featureRow;
    public int CurrentVariantRow => variantRow;
    public string SourceEvidence => Native.SourceEvidence + ";actual2939/981F00-first96B-two-caches;" +
        "980850-96DE40/9808A0-matched48-separate;actual-v28-PatternMap/Bias/SubShadowMaps-do-not-write-variant;" +
        "EBCD70-only-request997250-4C;variant72-retained-source;no-device-ready";
    public void ApplyRecordRequest(DeferredEarlyIndirectCurrentFeature actualFeature)
    {
        actualFeature.RequireCurrent(actualFeature.Frame);
        if (actualFeature.Frame.Camera.ExecutableLocale != Native.Locale)
            throw new InvalidDataException("Early68 current shader state belongs to another native profile.");
        float fade = BinaryPrimitives.ReadSingleLittleEndian(actualFeature.EffectiveLightData.Span.Slice(0xF24, 4));
        if (!float.IsFinite(fade)) throw new InvalidDataException("Early68 current ShadowReceiverBias F24 is nonfinite.");
        ApplyNative4CRequest(fade > 0 ? (sbyte)0 : (sbyte)1);
        var match = Source.SelectCurrentFeature(Provider, actualFeature.ReadWords());
        if (match.RowIndex >= 0) featureRow = match.RowIndex; //980850 preserves previous on miss.
    }
    /// <summary>Actual997250. Rejected values retain the owned cache; this
    /// operation never publishes a source variant row as a current writer.</summary>
    public void ApplyNative4CRequest(sbyte actualValue)
    {
        uint value = unchecked((uint)(int)actualValue);
        if (BinaryPrimitives.ReadUInt32LittleEndian(requested.AsSpan(76, 4)) == value) return;
        if ((Source.AllowedVariantWordValues(Provider, 19) & (1u << (actualValue & 31))) != 0)
            BinaryPrimitives.WriteUInt32LittleEndian(requested.AsSpan(76, 4), value);
    }
    public DeferredEarlyIndirectProgram SelectCurrentProgram()
    {
        // Actual2939 A52890 compares the entire96B cache. On a source miss,
        // 9817AB copies the committed cache back; no row0 fallback is minted.
        if (!requested.AsSpan().SequenceEqual(committed))
        {
            var match = Source.MatchVariant(Provider, requested);
            if (match is null) committed.CopyTo(requested, 0);
            else { variantRow = match.Index; requested.CopyTo(committed, 0); }
        }
        return Source.ResolveProgram(Provider, checked((uint)featureRow), checked((uint)variantRow));
    }
}

/// <summary>Persistent source parameter/cache owner. The caller retains its
/// actual common Generic and current registry/resource owners separately.</summary>
public sealed class DeferredEarlyIndirectCurrentOwner
{
    private readonly Dictionary<DeferredEarlyIndirectProvider, DeferredEarlyIndirectShaderState> shaders;
    private DeferredEarlyIndirectCurrentScope? current;
    private Exception? interruptedWriter;
    public DeferredEarlyIndirectCurrentOwner(LoadedDeferredEarlyIndirectSource source,
        LoadedDeferredEarlyIndirectPatternMapSource native)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Native = native ?? throw new ArgumentNullException(nameof(native));
        shaders = Enum.GetValues<DeferredEarlyIndirectProvider>().ToDictionary(kind => kind,
            kind => new DeferredEarlyIndirectShaderState(source, native, kind));
    }
    public LoadedDeferredEarlyIndirectSource Source { get; }
    public LoadedDeferredEarlyIndirectPatternMapSource Native { get; }
    public DeferredEarlyIndirectShaderState Shader(DeferredEarlyIndirectProvider provider) => shaders[provider];
    public DeferredEarlyIndirectCurrentScope BeginCurrent(
        MapSourceCreatedF103CurrentFrame geometry, MapCommonClockPublication clock,
        WorldGeometryLateGenericScopeState actualGeneric,
        DeferredEarlyIndirectReceiverBiasState? actualReceiverBias = null)
    {
        if (current is not null) throw new InvalidOperationException("Early68 already owns an active current CPU parameter scope.");
        if (interruptedWriter is not null)
            throw new InvalidOperationException("The interrupted early68 source writer must retire before another current scope.", interruptedWriter);
        ArgumentNullException.ThrowIfNull(geometry); ArgumentNullException.ThrowIfNull(clock); ArgumentNullException.ThrowIfNull(actualGeneric);
        clock.RequireCurrentForCommon(geometry.Common);
        if (clock.FrameNumber != geometry.FrameNumber || geometry.Camera.ExecutableLocale != Native.Locale ||
            (geometry.ViewFields.ViewFlags19AC & 0x9FFFFFFF) == 0)
            throw new InvalidDataException("Early68 parameter scope requires its actual active View, Common254 and same native profile.");
        return current = new(this, geometry, clock, actualGeneric, actualReceiverBias);
    }
    internal void End(DeferredEarlyIndirectCurrentScope actual)
    {
        if (!ReferenceEquals(current, actual)) throw new InvalidDataException("Early68 parameter scope belongs to another owner.");
        current = null;
    }
    internal void RecordInterruptedWriter(Exception error) => interruptedWriter ??= error;
    public static DeferredEarlyIndirectProvider SelectCurrentProvider(
        MapSourceCreatedF103CurrentFrame geometry, bool actualLitBufferPresent)
    {
        if (!actualLitBufferPresent || (geometry.ViewFields.CurrentPublication.ShadowPrimitiveCount1BFC & 15) == 0 ||
            geometry.ViewFields.CurrentPublication.SelectedShadowWrapper1BD0Present == false)
            return DeferredEarlyIndirectProvider.NoSubShadow;
        if (geometry.ViewFields.View1BD0CpuVtable is not ulong actualCpu)
            throw new InvalidDataException("Early68 provider selection requires its actual selected shadow wrapper CPU getter, including null.");
        return actualCpu == 0 ? DeferredEarlyIndirectProvider.NoSubShadow : DeferredEarlyIndirectProvider.Primary;
    }
}

public sealed class DeferredEarlyIndirectCurrentScope : IDisposable
{
    private readonly DeferredEarlyIndirectCurrentOwner owner;
    private readonly WorldGeometryLateGenericScopeState generic;
    private readonly float savedTime;
    private readonly DeferredEarlyIndirectReceiverBiasState? receiverBias;
    private DeferredEarlyIndirectPatternMapWrite? activePattern;
    private DeferredEarlyIndirectShaderState? activeShader;
    private DeferredEarlyIndirectCurrentRecord? activeRecord;
    private int nextGroup;
    private bool receiverOverridden;
    private DeferredEarlyIndirectProvider? selectedProvider;
    private bool disposed;
    internal DeferredEarlyIndirectCurrentScope(DeferredEarlyIndirectCurrentOwner owner,
        MapSourceCreatedF103CurrentFrame frame, MapCommonClockPublication clock,
        WorldGeometryLateGenericScopeState generic, DeferredEarlyIndirectReceiverBiasState? receiverBias)
    {
        (this.owner, Frame, Clock, this.generic) = (owner, frame, clock, generic);
        this.receiverBias = receiverBias;
        savedTime = generic.ReadActualParameterTime(); generic.SetActualParameterTime(clock.CommonTime254);
    }
    public MapSourceCreatedF103CurrentFrame Frame { get; }
    public MapCommonClockPublication Clock { get; }
    /// <summary>D76780's actual local88 output for this record's resource
    /// selector. It is independent of texture readiness.</summary>
    public DeferredEarlyIndirectCurrentFeature? CurrentFeature { get; private set; }
    public DeferredEarlyIndirectReceiverBiasState? CurrentReceiverBias => receiverBias;
    public DeferredEarlyIndirectConstantPacket PrepareGroup(
        DeferredEarlyIndirectCurrentRecord record, DeferredEarlyIndirectCurrentGroup group,
        DeferredEarlyIndirectPatternCameraTail camera, IDeferredEarlyIndirectCurrentPatternResources resources,
        DeferredEarlyIndirectProvider actualProvider, ReadOnlySpan<byte> actualCompleteModuleGlobals,
        IDeferredEarlyIndirectCurrentPatternSky? actualSky = null)
    {
        byte[] currentBytes = actualCompleteModuleGlobals.ToArray();
        return PrepareGroup(record, group, camera, resources, actualProvider, _ => currentBytes, actualSky);
    }
    /// <summary>The actual selected layout is provided to the owning module
    /// globals reader after selection, avoiding a speculative layout or a
    /// second feature producer. The reader supplies retained current bytes.</summary>
    public DeferredEarlyIndirectConstantPacket PrepareGroup(
        DeferredEarlyIndirectCurrentRecord record, DeferredEarlyIndirectCurrentGroup group,
        DeferredEarlyIndirectPatternCameraTail camera, IDeferredEarlyIndirectCurrentPatternResources resources,
        DeferredEarlyIndirectProvider actualProvider,
        Func<DeferredEarlyIndirectProgram, ReadOnlyMemory<byte>> readActualModuleGlobals,
        IDeferredEarlyIndirectCurrentPatternSky? actualSky = null)
    {
        ArgumentNullException.ThrowIfNull(readActualModuleGlobals);
        ObjectDisposedException.ThrowIf(disposed, this); Clock.RequireCurrentForCommon(Frame.Common);
        if (activePattern is not null || !ReferenceEquals(record.Frame, Frame) ||
            activeRecord is not null && !ReferenceEquals(activeRecord, record))
            throw new InvalidDataException("Early68 group is foreign or the previous source record reset has not returned.");
        camera.RequireCurrent(Frame); resources.RequireCurrent(Frame);
        bool special = DeferredEarlyIndirectPatternSpecialSource.UsesSpecialBranch(camera, resources);
        if (special)
        {
            if (actualSky is null)
                throw new InvalidDataException("Current special PatternMap requires the actual Host.v1A8 owner, not an ordinary matrix fallback.");
            DeferredEarlyIndirectPatternSpecialSource.RequireActualSta(Frame, resources);
            actualSky.RequireCurrent(Frame);
        }
        if (selectedProvider is { } previous && previous != actualProvider)
            throw new InvalidDataException("Early68's source provider is selected once for the actual pass.");
        var shader = owner.Shader(actualProvider);
        if (activeRecord is null)
        {
            if (record.Groups.Count == 0 || !ReferenceEquals(record.Groups[0], group))
                throw new InvalidDataException("Early68 must enter the first actual source group before writing its record cache.");
            var feature = DeferredEarlyIndirectCurrentFeatureSource.Prepare(Frame, record.Lighting, camera);
            float fade = BinaryPrimitives.ReadSingleLittleEndian(feature.EffectiveLightData.Span.Slice(0xF24, 4));
            if (fade > 0 && receiverBias is null)
                throw new InvalidDataException("Positive current F24 requires the actual retained ShadowReceiverBias owner.");
            if (fade > 0)
            {
                receiverBias!.OverrideRecord(fade, BinaryPrimitives.ReadSingleLittleEndian(feature.EffectiveLightData.Span.Slice(0xF28, 4)));
                receiverOverridden = true;
            }
            shader.ApplyRecordRequest(feature); CurrentFeature = feature; selectedProvider = actualProvider;
            activeRecord = record; activeShader = shader; nextGroup = 0;
        }
        if (!ReferenceEquals(activeShader, shader) || nextGroup >= record.Groups.Count ||
            !ReferenceEquals(record.Groups[nextGroup], group))
            throw new InvalidDataException("Early68 groups must follow the source order on the same shader instance.");
        var program = shader.SelectCurrentProgram();
        DeferredEarlyIndirectPatternMapWrite? pattern = null;
        try
        {
            pattern = special ? shader.Pattern.ApplyCurrentSpecial(camera, resources, actualSky!)
                : shader.Pattern.ApplyCurrentOrdinary(camera.PrepareCurrent(resources), group.ModifierParameter.Span,
                    generic.ReadActualParameterTime());
            var packet = DeferredEarlyIndirectConstants.Prepare(program,
                DeferredEarlyIndirectCurrentCameraSource.Capture(Frame, Frame.Lighting), pattern,
                readActualModuleGlobals(program).Span);
            activePattern = pattern; return packet;
        }
        catch (Exception error)
        {
            owner.RecordInterruptedWriter(error);
            if (pattern is not null) shader.Pattern.ApplySourceRecordReset(pattern);
            throw;
        }
    }
    /// <summary>Called after the actual group callback returned, including a
    /// source v60 rejection. Does not imply that a GPU submission occurred.</summary>
    public void CompleteCurrentGroup()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (activePattern is null || activeShader is null) throw new InvalidOperationException("Early68 has no active current group.");
        activeShader.Pattern.ApplySourceRecordReset(activePattern); activePattern = null;
        if (++nextGroup == activeRecord!.Groups.Count)
        {
            if (receiverOverridden) { receiverBias!.ResetAfterRecord(); receiverOverridden = false; }
            activeShader.ApplyNative4CRequest(0); activeRecord = null; activeShader = null;
            CurrentFeature = null;
        }
    }
    public void Dispose()
    {
        if (disposed) return;
        try { if (activePattern is not null) CompleteCurrentGroup(); }
        finally
        {
            try
            {
                if (receiverOverridden) { receiverBias!.ResetAfterRecord(); receiverOverridden = false; }
                activeShader?.ApplyNative4CRequest(0); generic.SetActualParameterTime(savedTime);
            }
            finally { activeRecord = null; activeShader = null; owner.End(this); disposed = true; }
        }
    }
}
