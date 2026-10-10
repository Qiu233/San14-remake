using San14.Data;

/// <summary>Projects the successful ordinary56 caller's CPU tail from its
/// actual submitted terrain packet. Invoke after the real56 caller returns,
/// before57; this issues no whole-node, shader, GPU or draw receipt.</summary>
public static class WorldGeometryLateStateFromTerrain56
{
    public static WorldGeometryLateCpuState CreateAfterActualSubmission(
        WorldGeometryTargetBinding actual53,
        NativeTerrainStencilPreparedFrame preparedTerrain,
        TerrainGeometrySubmittedFrame submittedTerrain)
    {
        ArgumentNullException.ThrowIfNull(actual53);
        ArgumentNullException.ThrowIfNull(preparedTerrain);
        ArgumentNullException.ThrowIfNull(submittedTerrain);
        actual53.RequireCurrent(); preparedTerrain.ValidateSubmitted(submittedTerrain);
        var execution = preparedTerrain.Node51Execution ??
            throw new InvalidDataException("Late state requires the actual GeneralScene56 invocation, not a stencil recipe alone.");
        var sourceFrame = preparedTerrain.Frame;
        if (actual53.SourceNode != 53 || execution.Source.NodeIndex != 56 ||
            execution.SuppressesOrdinaryTerrainF8 ||
            !ReferenceEquals(actual53.Accepted.Context, submittedTerrain.Context) ||
            !ReferenceEquals(actual53.Accepted.ActualGeometrySourceFrame, sourceFrame) ||
            !ReferenceEquals(actual53.Frame, submittedTerrain.Target) ||
            actual53.Frame.ColorViews.Count != 5 || submittedTerrain.Batches.Count == 0 ||
            submittedTerrain.Batches.All(b => b.InstanceCount == 0))
            throw new InvalidDataException("Late state requires successful nonempty ordinary56 terrain in this actual53 five-target scope.");
        execution.RequireSelectedView(submittedTerrain.Context.FrameNumber, sourceFrame.Lighting.Binding);

        var raster = submittedTerrain.Raster;
        uint masks = raster.PackedColorWriteMasks ??
            throw new InvalidDataException("Submitted ordinary56 source masks must be represented explicitly.");
        if ((masks & ~0xFFFFFu) != 0 || raster.Stencil is null)
            throw new InvalidDataException("Submitted ordinary56 packet does not contain its five active masks and actual F8 stencil state.");
        var colors = Enumerable.Range(0, 5).Select(i =>
        {
            byte rgba = (byte)((masks >> (i * 4)) & 15);
            return new WorldGeometryLateColorState(false, (byte)(rgba & 7), (rgba & 8) != 0);
        }).ToArray();
        var recipe = preparedTerrain.SourceRecipe;
        var stencil = new WorldGeometryLateStencilState(recipe.StencilEnabledAfterPrimitiveCleanup,
            recipe.NativeCompareCode, recipe.Reference, recipe.ReadMask, recipe.WriteMask,
            recipe.NativeFailOperation, recipe.NativeDepthFailOperation, recipe.NativePassOperation);
        var depth = new WorldGeometryLateDepth(raster.DepthTest, raster.DepthWrite,
            (uint)raster.DepthCompare, stencil.Enabled);

        // D2D94B..99E / D712F0 writes the five active color slots. Neither
        // D2D050's tail nor D31D30 restores their previous blend/masks.
        // D2E443..468 disables stencil only; its other fields remain.
        // D32755..D3278B clears clip1F0 only for this Common-present caller.
        // Alpha1BE and unbound slots have no complete current-source writer
        // here and stay unrepresented; the owning frontend retains them.
        return WorldGeometryLateCpuState.CreateActiveProjection(depth, colors,
            actualAlphaEnabled: null, stencil,
            actualClipWord1F0: sourceFrame.Common.AuxiliaryPresent ? 0u : null);
    }
}
