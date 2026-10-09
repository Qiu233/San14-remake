using San14.Data;
using San14.Data.Formats;

/// <summary>The actual compiled source stage retained after ordinary Landscape
/// F8. The device backend owns a combined vertex/pixel program; stage code
/// keeps the two original frontend bindings distinct within that program.</summary>
public sealed record WorldGeometryTerrain56StageLoan(
    TerrainGbufferProgram ActualProgram, G2ShaderCombinationStage SourceStage);

public sealed record WorldGeometryTerrain56StageInheritance(
    TerrainGeometrySubmittedFrame Submitted, TerrainGbufferProgram ActualLastProgram,
    IReadOnlyList<WorldGeometryLateGenericResourceLoan?> StageLoans);

/// <summary>Persistent stage loans for the accepted normal Terrain56 draw.
/// 980980 binds the six original combination entries; ordinary F8 exit
/// BA80F0 -> 97F0B0 retains VS/PS and clears HS/DS/GS/CS. This owner never
/// derives Generic+C8 from a GPU program, DXBC reflection or texture bindings.</summary>
public sealed class WorldGeometryTerrain56StageSourceOwner
{
    private readonly NativeMapScene scene;
    private readonly TerrainGbufferProgram detail, coarse;
    private readonly IReadOnlyList<WorldGeometryLateGenericResourceLoan?> detailLoans, coarseLoans;
    public NativeMapScene ActualScene => scene;

    public WorldGeometryTerrain56StageSourceOwner(NativeMapScene actualScene,
        LoadedWorldGeometryLateEffectSource loadedSource)
    {
        ArgumentNullException.ThrowIfNull(actualScene); ArgumentNullException.ThrowIfNull(loadedSource);
        scene = actualScene; detail = scene.DetailProgram; coarse = scene.CoarseProgram;
        detailLoans = CreateSourceStageLoans(detail); coarseLoans = CreateSourceStageLoans(coarse);
    }

    /// <summary>Borrow only the program selected by the actual final ordered
    /// device draw. Reading this packet performs no source IO or hashing.</summary>
    public WorldGeometryTerrain56StageInheritance BorrowCompletedTerrain56(
        TerrainGeometrySubmittedFrame actualSubmitted, NativeMapFrameContext actualContext)
    {
        ArgumentNullException.ThrowIfNull(actualSubmitted); ArgumentNullException.ThrowIfNull(actualContext);
        actualSubmitted.RequireCurrent(actualContext);
        if (!ReferenceEquals(scene.LastTerrainSubmittedFrame, actualSubmitted) || actualSubmitted.Batches.Count == 0)
            throw new InvalidDataException("Terrain stage inheritance requires this scene's actual nonempty ordinary F8 submission.");
        var last = actualSubmitted.Batches[^1]; var program = last.Coarse ? coarse : detail;
        if (!program.ShaderRid.IsValid || last.CombinationIndex != program.Combination.Index ||
            last.ShaderUri != program.Source.Shader.Shader.Source.ResourceUri ||
            last.VertexProgramSha256 != program.VertexTranslation.SourceProgramSha256 ||
            last.PixelProgramSha256 != program.PixelTranslation.SourceProgramSha256)
            throw new InvalidDataException("Last Terrain stage loan differs from its accepted original source combination and compiled program.");
        return new(actualSubmitted, program, last.Coarse ? coarseLoans : detailLoans);
    }

    /// <summary>Import the completed frontend stage writes into the shared CPU
    /// owner. Its independently retained Generic+C8 remains unchanged.</summary>
    public WorldGeometryTerrain56StageInheritance AcceptCompletedTerrain56(
        TerrainGeometrySubmittedFrame actualSubmitted, NativeMapFrameContext actualContext,
        WorldGeometryLateGenericScopeState actualGeneric)
    {
        ArgumentNullException.ThrowIfNull(actualGeneric);
        var inherited = BorrowCompletedTerrain56(actualSubmitted, actualContext);
        foreach (int stage in new[] { 0, 2, 3, 4, 5, 1 })
        {
            if (stage >= 2) actualGeneric.ClearActualResourceLoan(stage);
            else actualGeneric.AcceptActualCompletedStageBinding(stage, inherited.StageLoans[stage],
                inherited.StageLoans[stage]?.FrontendHandle ?? 0);
        }
        return inherited;
    }

    private static IReadOnlyList<WorldGeometryLateGenericResourceLoan?> CreateSourceStageLoans(
        TerrainGbufferProgram actualProgram)
    {
        // Native Generic order differs from DXBC stage code: VS, PS, HS, DS, GS, CS.
        (uint Code, int Offset)[] fields = [(1, 0), (0, 2), (3, 8), (4, 10), (2, 12), (5, 14)];
        var loans = new WorldGeometryLateGenericResourceLoan?[6];
        for (int stage = 0; stage < fields.Length; stage++)
        {
            var entry = actualProgram.Combination.Stages.Single(row => row.StageCode == fields[stage].Code);
            if (entry.RowFieldOffset != fields[stage].Offset ||
                (entry.StageTableEntryIndex < 0) != (entry.Entry is null))
                throw new InvalidDataException("Terrain combination differs from the source 980980 stage array selector.");
            if (stage >= 2)
            {
                if (entry.Entry is not null)
                    throw new NotSupportedException("The admitted ordinary terrain device program has a non-VS/PS stage.");
                continue; // Actual 97F0B0 exit also clears these four stages.
            }
            if (entry.Entry is null || !actualProgram.ShaderRid.IsValid)
                throw new InvalidDataException("Successful normal Terrain56 requires its original vertex and pixel entries.");
            // Godot submits these source stages in one real ShaderRid. Native
            // frontend stage commands distinguish VS and PS, so the port uses
            // that actual device handle in the two independently owned slots.
            loans[stage] = new(new WorldGeometryTerrain56StageLoan(actualProgram, entry), actualProgram.ShaderRid.Id);
        }
        return Array.AsReadOnly(loans);
    }
}
