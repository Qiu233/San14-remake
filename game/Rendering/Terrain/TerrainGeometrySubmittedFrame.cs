using San14.Data;

/// <summary>Actual complete terrain render-thread submission. CPU counts,
/// allocated targets and an explicit stencil recipe cannot create this receipt.
/// This is device submission evidence, not original final-state authority.</summary>
public sealed class TerrainGeometrySubmittedFrame
{
    private readonly NativeMapScene scene;
    private readonly IReadOnlyList<TerrainGbufferDraw> submittedDraws;
    internal TerrainGeometrySubmittedFrame(NativeMapScene scene, NativeMapFrameContext context,
        TerrainGbufferRasterState raster, IReadOnlyList<(NativeTerrainBatch Batch, TerrainGbufferDraw Draw)> completed)
    {
        this.scene = scene; Context = context; Raster = raster;
        submittedDraws=Array.AsReadOnly(completed.Select(entry=>entry.Draw).ToArray());
        Batches = Array.AsReadOnly(completed.Select(entry => new TerrainGeometrySubmittedBatch(
            entry.Batch.Coarse, Array.AsReadOnly(entry.Batch.Patches.ToArray()), entry.Draw.VertexCount,
            entry.Draw.IndexCount, entry.Draw.InstanceCount, entry.Draw.Program.Combination.Index,
            entry.Draw.Program.Source.Shader.Shader.Source.ResourceUri,
            entry.Draw.Program.Source.Shader.Shader.Source.DecodedSha256,
            entry.Draw.Program.VertexTranslation.SourceProgramSha256, entry.Draw.Program.PixelTranslation.SourceProgramSha256)).ToArray());
    }
    public NativeMapFrameContext Context { get; }
    public RenderTargetFrame Target => Context.TerrainFrame;
    public TerrainGbufferRasterState Raster { get; }
    public IReadOnlyList<TerrainGeometrySubmittedBatch> Batches { get; }
    public void RequireCurrent(NativeMapFrameContext actualContext)
    {
        if (!ReferenceEquals(scene.LastTerrainSubmittedFrame, this) || !ReferenceEquals(Context, actualContext) ||
            !ReferenceEquals(scene.TerrainFrame, Target))
            throw new InvalidDataException("Terrain submission was replaced, failed, retired or belongs to another accepted context.");
    }
    /// <summary>Last real terrain uniform-set writer for this original PS
    /// sampler slot. The receipt stores the actual ordered draws, not a later
    /// lookup in the mutable texture dictionary or a shader-name inference.</summary>
    internal DeferredDeviceBinding RequireLastSubmittedPixelSampler(uint register)
    {
        RequireCurrent(Context);
        for(int i=submittedDraws.Count-1;i>=0;i--)
        {
            var draw=submittedDraws[i];var actual=draw.RequireActualPixelSamplerBindings()
                .Where(b=>b.SourceBindPoint==register).ToArray();
            bool originalDeclares=draw.Program.PixelBindings.SourceDefinitions.Bindings.Any(b=>
                b.InputType==3&&register>=b.BindPoint&&register-b.BindPoint<b.BindCount);
            if(actual.Length==0)
            {
                if(originalDeclares)throw new NotSupportedException("Terrain original sampler writer is absent from its actual device packet.");
                continue;
            }
            if(actual.Length!=1||!originalDeclares||actual[0].DescriptorKind!="Sampler"||!actual[0].Resource.IsValid||
                draw.Program.PixelTranslation.SourceProgramSha256!=Batches[i].PixelProgramSha256)
                throw new InvalidDataException("Terrain PS sampler does not belong to its actual ordered original shader submission.");
            return actual[0];
        }
        throw new NotSupportedException("Actual terrain did not submit this original PS sampler register.");
    }
}
public sealed record TerrainGeometrySubmittedBatch(bool Coarse,
    IReadOnlyList<TerrainPatchInput> Patches, int VertexCount, int IndexCount, int InstanceCount,
    int CombinationIndex, string ShaderUri, string ShaderSha256, string VertexProgramSha256,
    string PixelProgramSha256);
