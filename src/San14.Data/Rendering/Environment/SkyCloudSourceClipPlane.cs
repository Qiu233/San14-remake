using System.Numerics;

namespace San14.Data;

/// <summary>The dedicated vertex clip semantic is excluded from ordinary
/// named bindings. This new owned cache uses the source frontend's mask0 and
///95EA50 neutral-plane preparation; it never copies an original live27E0 cache.</summary>
public sealed partial class SkyCloudSourceClipPlane
{
    private readonly Vector4 neutral;
    private readonly string locale, recipe;
    private MapOwnedGraphicsColorMaskHistorySource? frontend;
    private SkyCloudSourceClipPlanePacket? current;
    private SkyCloudSourceClipPlanePacket? completed;
    private bool valid = true;
    public MapOwnedGraphicsColorMaskHistorySource? OwnedFrontendSource => frontend;
    public long CompletedSourceFrameNumber { get; private set; }
    private SkyCloudSourceClipPlane(string locale, Vector4 neutral, string recipe)
    { this.locale = locale; this.neutral = neutral; this.recipe = recipe; }
    public static SkyCloudSourceClipPlane CreateFresh(GameResourceRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var source = RenderSamplerSourceAdapters.ReadCloudSpecialClipNativeSource(repository.SourceDirectory, locale);
        return new(locale, source.Neutral, source.Provenance);
    }

    /// <summary>CPU descriptor plan for cached draw creation only. This is
    /// explicitly separate from the first actual owned GPU attempt.</summary>
    public SkyCloudSourceClipPlanePacket PrepareConstructorPlan(SkySceneSourceFramePacket source,
        MapSourceCreatedSeasonLightingFrame present, SkyAppliedSourceStageFrame stages,
        MainSceneNode51Execution execution)
    {
        RequireSourceFrame(source, present, stages, execution);
        if (CompletedSourceFrameNumber != 0 || current is { IsConstructorPlan: false } || frontend?.WorkSubmissionStarted == true)
            throw new InvalidDataException("Clip constructor plan cannot replace an actual current frontend publication.");
        return current = new(this, source, present, stages, execution, null, neutral,
            recipe + ";constructor-mask0/owned-95EA50-cache-plan;not-GPU-current", true, 0);
    }

    /// <summary>Borrow the privately created frontend before actual WORK.
    /// Binding does not prepare a draw or acknowledge a completed frame.</summary>
    public void BindOwnedFrontend(MapOwnedGraphicsColorMaskHistorySource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!valid || source.Locale != locale || source.CompletedSourceFrameNumber != 0 ||
            source.WorkSubmissionStarted || CompletedSourceFrameNumber != 0 ||
            frontend is not null && !ReferenceEquals(frontend, source))
            throw new InvalidDataException("Clip history must borrow this exact fresh frontend before WORK.");
        frontend = source;
    }

    public SkyCloudSourceClipPlanePacket PrepareFirstCurrent(MapOwnedGraphicsMaskPrefix prefix,
        SkySceneSourceFramePacket source, MapSourceCreatedSeasonLightingFrame present,
        SkyAppliedSourceStageFrame stages, MainSceneNode51Execution execution)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        if (!prefix.UsesConstructedFrontendState || CompletedSourceFrameNumber != 0)
            throw new InvalidDataException("First-only clip preparation cannot acquire a later frontend entry.");
        return PrepareCurrent(prefix, source, present, stages, execution);
    }

    /// <summary>Prepare the source special cache from this frontend's retained
    /// mask0. A later entry requires this clip owner and the paired frontend to
    /// have acknowledged the same complete source Host output. Raw planes are
    /// not inferred to remain zero: disabled mask nibbles select source neutral.</summary>
    public SkyCloudSourceClipPlanePacket PrepareCurrent(MapOwnedGraphicsMaskPrefix prefix,
        SkySceneSourceFramePacket source, MapSourceCreatedSeasonLightingFrame present,
        SkyAppliedSourceStageFrame stages, MainSceneNode51Execution execution)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        RequireSourceFrame(source, present, stages, execution);
        prefix.RequireCanonicalSkyDeclaredHookScope();
        if (frontend is null) BindOwnedFrontend(prefix.Source);
        if (!ReferenceEquals(frontend, prefix.Source) || prefix.Source.Locale != locale ||
            prefix.Source.WorkSubmissionStarted || prefix.FrameNumber != source.FrameNumber ||
            prefix.ViewIdentity != source.Binding.ViewIdentity || prefix.FrameNumber <= CompletedSourceFrameNumber ||
            prefix.PreviousCompletedFrameNumber != CompletedSourceFrameNumber ||
            prefix.Source.CompletedSourceFrameNumber != CompletedSourceFrameNumber ||
            prefix.UsesConstructedFrontendState != (CompletedSourceFrameNumber == 0))
            throw new InvalidDataException("Clip requires this exact current prefix before WORK and its own complete preceding source history.");
        if (current is { IsConstructorPlan: false } && current.FrameNumber > CompletedSourceFrameNumber)
        {
            if (!ReferenceEquals(current.Prefix, prefix) || !ReferenceEquals(current.AcceptedSource, source) ||
                !ReferenceEquals(current.AcceptedPresent, present) || !ReferenceEquals(current.AcceptedStages, stages) ||
                !ReferenceEquals(current.AcceptedExecution, execution))
                throw new InvalidDataException("Clip is already prepared for another source generation/attempt.");
            return current;
        }
        return current = new(this, source, present, stages, execution, prefix, neutral,
            recipe + ";actual-owned-source-Sky-prefix;entry-mask0/95EA50-renewed-neutral-cache;" +
            (CompletedSourceFrameNumber == 0 ? "constructor-entry;" : $"completed-Host-frame{CompletedSourceFrameNumber};") +
            prefix.SourceProvenance, false, CompletedSourceFrameNumber);
    }

    /// <summary>Source-semantic validation only. The Game final wrapper joins
    /// the actual submitted zero-Army/static/Terrain/Early stock packet and
    /// complete source final call before allowing any history to commit.</summary>
    public void ValidateCompletedSourceHostOutput(SkyCloudSourceClipPlanePacket packet,
        MapCopy97CurrentStockPacket actualStock, MapFinalOutputProductionPrepared actualFinal)
    {
        ArgumentNullException.ThrowIfNull(actualFinal);
        ValidateSubmittedCurrentFamily(packet, actualStock);
        actualStock.Source.ValidateCompletedSourceHostOutput(actualStock, actualFinal);
    }

    /// <summary>Validate the exact private submitted family before the final
    /// draw. This checks identity and source scope without committing history.</summary>
    public void ValidateSubmittedCurrentFamily(SkyCloudSourceClipPlanePacket packet,
        MapCopy97CurrentStockPacket actualStock)
    {
        ArgumentNullException.ThrowIfNull(packet); ArgumentNullException.ThrowIfNull(actualStock);
        Validate(packet, packet.AcceptedSource, packet.AcceptedPresent);
        actualStock.ValidateCurrent();
        // Sky prepared this exact geometry lighting instance before57's
        // OR40. Shading reevaluates current keys on a new frame/light copy;
        // join the admitted source transition without replacing the old Clip.
        MapCopy97CurrentStockSource.RequireGeometryContextJoin(actualStock.Frame,
            actualStock.GeometryFrame,actualStock.Terrain);
        var geometry=actualStock.GeometryFrame??actualStock.Frame;
        if (packet.IsConstructorPlan || packet.Prefix is not { } prefix || frontend is null ||
            packet.PreviousCompletedSourceFrameNumber != CompletedSourceFrameNumber ||
            !ReferenceEquals(actualStock.Source.OwnedFrontendSource, frontend) ||
            !ReferenceEquals(actualStock.Prefix, prefix) || actualStock.FrameNumber != packet.FrameNumber ||
            actualStock.PreviousCompletedSourceFrameNumber != CompletedSourceFrameNumber ||
            !ReferenceEquals(geometry.Lighting, packet.AcceptedStages.AcceptedLighting) ||
            geometry.Lighting.Binding != packet.AcceptedSource.Binding)
            throw new InvalidDataException("Clip cannot acknowledge a plan, missing/foreign current family or another frontend attempt.");
        // Reuses the exact admitted class18/Terrain/ordinary78/late166 and
        // Camera/light/final scope; its Game getter validates actual layers.
        packet.AcceptedPresent.ValidatePreparedLighting(geometry.Lighting);
    }

    /// <summary>Invoke before Stock and mask replay, after all three have
    /// validated the same successful actual final call. This does not certify
    /// a GPU fence or readback. The finite native recipe returns mask0.</summary>
    public void ReplayCompletedSourceHostOutput(SkyCloudSourceClipPlanePacket packet,
        MapCopy97CurrentStockPacket actualStock, MapFinalOutputProductionPrepared actualFinal)
    {
        ValidateCompletedSourceHostOutput(packet, actualStock, actualFinal);
        CompletedSourceFrameNumber = packet.FrameNumber;
        completed = packet;
    }

    /// <summary>Independent mode1 completion. A known fallback draw is not a
    /// proof that subsequent Q callbacks/overlays retain the clip mask.</summary>
    public void ValidateCompletedSourceHostOutput(SkyCloudSourceClipPlanePacket packet,
        MapCopy97CurrentStockPacket actualStock,MapPostEffect3ApplyMode1Completed actualFinal)
    {
        ArgumentNullException.ThrowIfNull(actualFinal);ValidateSubmittedCurrentFamily(packet,actualStock);
        actualStock.Source.ValidateCompletedSourceHostOutput(actualStock,actualFinal);
    }
    public void ReplayCompletedSourceHostOutput(SkyCloudSourceClipPlanePacket packet,
        MapCopy97CurrentStockPacket actualStock,MapPostEffect3ApplyMode1Completed actualFinal)
    {
        ValidateCompletedSourceHostOutput(packet,actualStock,actualFinal);
        CompletedSourceFrameNumber=packet.FrameNumber;completed=packet;
    }
    public void RequireCompletedPacket(SkyCloudSourceClipPlanePacket packet)
    {
        if (!valid || !ReferenceEquals(completed, packet) || !ReferenceEquals(current, packet) ||
            packet.IsConstructorPlan || packet.FrameNumber != CompletedSourceFrameNumber ||
            frontend?.CompletedSourceFrameNumber != CompletedSourceFrameNumber)
            throw new InvalidDataException("Clip lacks its own exact complete source Host acknowledgement.");
        packet.Prefix!.RequireCurrentOrJustCompletedSkyDeclaredHookScope();
    }

    public void MarkImportedOrUnknownPlaneOrMaskWriter(string writer)
    { ArgumentException.ThrowIfNullOrWhiteSpace(writer); valid = false; }
    private void RequireSourceFrame(SkySceneSourceFramePacket source, MapSourceCreatedSeasonLightingFrame present,
        SkyAppliedSourceStageFrame stages, MainSceneNode51Execution execution)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(present);
        ArgumentNullException.ThrowIfNull(stages); ArgumentNullException.ThrowIfNull(execution);
        if (!valid) throw new InvalidDataException("Clip owned source scope was imported or given an unknown plane/mask writer.");
        execution.RequireSelectedView(present.FrameNumber, source.Binding);
        stages.ValidateCurrent(stages.AcceptedLighting, present.AcceptedSeasonCamera.Inputs.Source.Selection, present.CurrentSkyCamera);
        if (!ReferenceEquals(stages.AcceptedPresent, present) || source.Binding != stages.AcceptedLighting.Binding ||
            source.FrameNumber != present.FrameNumber || present.AcceptedSeasonCamera.Inputs.MainFrame.Camera.ExecutableLocale != locale ||
            execution.SelectedViewIndex != 0 || stages.ScenePresent || stages.WorkContributionEnabled ||
            source.SourceFrame.SixCameras.Count != 6 || source.SourceFrame.Controller is null)
            throw new InvalidDataException("Clip source scope requires the same actual primary View and exact direct seasonal Sky/WORK branch.");
    }
    internal void Validate(SkyCloudSourceClipPlanePacket packet, SkySceneSourceFramePacket source,
        MapSourceCreatedSeasonLightingFrame present)
    {
        if (!valid || !ReferenceEquals(current, packet) || !ReferenceEquals(packet.AcceptedSource, source) ||
            !ReferenceEquals(packet.AcceptedPresent, present))
            throw new InvalidDataException("Clip packet is stale, foreign or belongs to another source frame.");
        RequireSourceFrame(source, present, packet.AcceptedStages, packet.AcceptedExecution);
        packet.Prefix?.RequireCurrentOrJustCompletedSkyDeclaredHookScope();
    }
}

public sealed class SkyCloudSourceClipPlanePacket
{
    private readonly SkyCloudSourceClipPlane owner;
    internal SkyCloudSourceClipPlanePacket(SkyCloudSourceClipPlane owner,
        SkySceneSourceFramePacket source, MapSourceCreatedSeasonLightingFrame present,
        SkyAppliedSourceStageFrame stages, MainSceneNode51Execution execution,
        MapOwnedGraphicsMaskPrefix? prefix, Vector4 value, string provenance, bool plan, long previousCompleted)
    { this.owner = owner; AcceptedSource = source; AcceptedPresent = present; AcceptedStages = stages;
      AcceptedExecution = execution; Prefix = prefix; PreviousCompletedSourceFrameNumber = previousCompleted;
      Value = value; Provenance = provenance; IsConstructorPlan = plan; }
    public SkyCloudSourceClipPlane Source => owner;
    internal SkySceneSourceFramePacket AcceptedSource { get; }
    internal MapSourceCreatedSeasonLightingFrame AcceptedPresent { get; }
    internal SkyAppliedSourceStageFrame AcceptedStages { get; }
    internal MainSceneNode51Execution AcceptedExecution { get; }
    internal MapOwnedGraphicsMaskPrefix? Prefix { get; }
    public Vector4 Value { get; }
    public string Provenance { get; }
    public bool IsConstructorPlan { get; }
    public long FrameNumber => AcceptedSource.FrameNumber;
    public string ViewIdentity => AcceptedSource.Binding.ViewIdentity;
    public long PreviousCompletedSourceFrameNumber { get; }
    public void ValidateCurrent(SkySceneSourceFramePacket source, MapSourceCreatedSeasonLightingFrame present)
        => owner.Validate(this, source, present);
}
