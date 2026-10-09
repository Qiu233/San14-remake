using Godot;
using San14.Data;

/// <summary>Binds the proved conditional ordinary F8 stencil recipe to the
/// actual source-created Scene. Main graph activation stays a separate gate.</summary>
public sealed class NativeTerrainStencilCurrentOwner
{
    private readonly LoadedTerrainGbiMapResources material;
    private readonly TerrainOrdinaryNativeStencilSource source;
    private bool retained = true;
    private NativeTerrainStencilPreparedFrame? current;
    private NativeTerrainStencilCurrentOwner(LoadedTerrainGbiMapResources material)
    { this.material = material; source = TerrainOrdinaryNativeStencilSource.ReadFreshFieldScene(material.Shader); }
    public string OwnerIdentity { get; } = "owned-Landscape-F8-stencil:" + Guid.NewGuid().ToString("N");
    public static NativeTerrainStencilCurrentOwner CreateFreshFieldSceneNoExternalAssignments(LoadedTerrainGbiMapResources material) => new(material);
    public void MarkImportedEditedOrUnresolved(string writer)
    { ArgumentException.ThrowIfNullOrWhiteSpace(writer); retained = false; current = null; }

    /// <summary>Actual source-created node51 CPU.v8 activation. The current
    /// View and independent WorldPQ still have to match this accepted frame;
    /// the graph supplies flags, never a guessed light mask or camera slot.</summary>
    public NativeTerrainStencilPreparedFrame PrepareFromNode51(NativeMapFrameContext context,
        MapSourceCreatedF103CurrentFrame frame, NativeTerrainCurrentViewFrame terrain,
        TerrainCurrentKind1WorldPQPublication actualWorldPQ,
        MainSceneNode51Execution execution, TerrainGbufferRasterState baseRaster)
        =>PrepareFromGeneralScene(context,frame,terrain,actualWorldPQ,execution,baseRaster);

    /// <summary>Ordinary F8 needs the actual unsuppressed GeneralScene
    /// invocation. Original node51's Flag2 skips this consumer.</summary>
    public NativeTerrainStencilPreparedFrame PrepareFromGeneralScene(NativeMapFrameContext context,
        MapSourceCreatedF103CurrentFrame frame, NativeTerrainCurrentViewFrame terrain,
        TerrainCurrentKind1WorldPQPublication actualWorldPQ,
        MainSceneNode51Execution execution, TerrainGbufferRasterState baseRaster)
    {
        current = null;
        ArgumentNullException.ThrowIfNull(execution);
        if(execution.SuppressesOrdinaryTerrainF8)
            throw new InvalidOperationException("This actual GeneralScene invocation skips ordinary Terrain F8.");
        execution.RequireSelectedView(context.FrameNumber, frame.Lighting.Binding);
        if (frame.Context.FrameNumber != context.FrameNumber || execution.Invocation.ContextPointer50Absent != false ||
            execution.Invocation.CurrentContextOwnerIdentity != frame.Context.Source.OwnedHostIdentity ||
            frame.Context.Source.Host.Locale != execution.Source.Locale ||
            frame.Context.Source.Host.RuleVersion != execution.Source.RuleVersion)
            throw new InvalidDataException("Node51 and current Host use different executable sources.");
        var prepared = Prepare(context, frame, terrain, actualWorldPQ, execution.CurrentCallFlags,
            execution.SourceProvenance, baseRaster);
        prepared.Node51Execution = execution;
        return prepared;
    }

    public NativeTerrainStencilPreparedFrame Prepare(NativeMapFrameContext context,
        MapSourceCreatedF103CurrentFrame frame, NativeTerrainCurrentViewFrame terrain,
        TerrainCurrentKind1WorldPQPublication actualWorldPQ, byte actualCurrentCallFlags,
        string currentExecutionProducer, TerrainGbufferRasterState baseRaster)
    {
        current = null; RequireRetained(); ArgumentException.ThrowIfNullOrWhiteSpace(currentExecutionProducer);
        terrain.ValidateCurrent(context, frame);
        if (!ReferenceEquals(context.AcceptedTerrainMaterial, material) || baseRaster.Stencil is not null)
            throw new InvalidDataException("Terrain source stencil requires the actual accepted material and no externally assigned stencil.");
        var typedTerrain = terrain.CurrentViewPublication.Terrain1BB8 ?? throw new InvalidDataException("Actual terrain View1BB8 publication is missing.");
        var publication = new TerrainFreshOwnedStencilPublication(context.FrameNumber, typedTerrain.ViewIdentity,
            typedTerrain.CameraOwnerIdentity, OwnerIdentity + "/record0/" + context.FrameNumber,
            OwnerIdentity + "/F8", source.ShaderSha256, source.RendererKind, actualCurrentCallFlags,
            source.TerrainSource.Exclusion2B8, false, false, true, true, true, true,
            OwnerIdentity + "/retained-source-DC52-GBuffer;" + currentExecutionProducer);
        var receipt = source.PrepareCurrent(typedTerrain, actualWorldPQ, publication);
        var face = new TerrainGbufferStencilFace(RenderingDevice.CompareOperator.Always,
            RenderingDevice.StencilOperation.Keep, RenderingDevice.StencilOperation.Keep,
            RenderingDevice.StencilOperation.Replace, receipt.Reference, receipt.ReadMask, receipt.WriteMask);
        return current = new(this, context, frame, terrain, receipt, baseRaster with { Stencil = new(face, face) });
    }
    private void RequireRetained()
    { if (!retained) throw new InvalidDataException("Terrain F8 stencil owner was imported, edited or unresolved."); source.ValidateSourceUnchanged(); }
    internal void RequireCurrent(NativeTerrainStencilPreparedFrame expected)
    { RequireRetained(); if (!ReferenceEquals(expected, current)) throw new InvalidDataException("Terrain F8 stencil preparation was superseded or failed."); }
}

public sealed class NativeTerrainStencilPreparedFrame
{
    private readonly NativeTerrainStencilCurrentOwner owner;
    internal NativeTerrainStencilPreparedFrame(NativeTerrainStencilCurrentOwner owner,
        NativeMapFrameContext context, MapSourceCreatedF103CurrentFrame frame,
        NativeTerrainCurrentViewFrame terrain, TerrainOrdinaryNativeStencilReceipt recipe,
        TerrainGbufferRasterState raster)
    { this.owner = owner; Context = context; Frame = frame; Terrain = terrain; SourceRecipe = recipe; Raster = raster; }
    public NativeMapFrameContext Context { get; }
    public MapSourceCreatedF103CurrentFrame Frame { get; }
    public NativeTerrainCurrentViewFrame Terrain { get; }
    public TerrainOrdinaryNativeStencilReceipt SourceRecipe { get; }
    public TerrainGbufferRasterState Raster { get; }
    public MainSceneNode51Execution? Node51Execution { get; internal set; }
    public void ValidateCurrent(NativeMapFrameContext actualContext)
    { owner.RequireCurrent(this); Terrain.ValidateCurrent(actualContext, Frame); }
    public void ValidateSubmitted(TerrainGeometrySubmittedFrame actualSubmission)
    {
        ValidateCurrent(actualSubmission.Context); actualSubmission.RequireCurrent(Context);
        var accepted = Context.AcceptedTerrainBatches ?? throw new InvalidDataException("Accepted terrain inventory is unavailable.");
        if (actualSubmission.Raster != Raster || actualSubmission.Batches.Count != accepted.Count ||
            actualSubmission.Batches.Where((batch, index) => batch.Coarse != accepted[index].Coarse ||
                !batch.Patches.SequenceEqual(accepted[index].Patches) || batch.InstanceCount != accepted[index].Patches.Count ||
                batch.ShaderUri != SourceRecipe.Source.ShaderUri || batch.ShaderSha256 != SourceRecipe.Source.ShaderSha256).Any())
            throw new InvalidDataException("Terrain device submission differs from its exact current source recipe/material/patches.");
    }
}
