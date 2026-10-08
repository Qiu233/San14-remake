using San14.Data;
using San14.Data.Formats;

public sealed partial class OriginalDeferredEarlyShadowModelDeviceOwner
{
    /// <summary>Import the actual Army model resource and its final full
    /// primitive18 table. Skin/palette, features, CBs and texture bindings are
    /// current early19 inputs; a state18 GBuffer packet is not an input here.</summary>
    public OriginalDeferredEarlyShadowModelSource LoadArmySource(
        OriginalNativeWorldShaderTableState actualFinalTable, LoadedOriginalArmyModel actualModel,
        int actualDrawIndex)
    {
        ArgumentNullException.ThrowIfNull(actualModel);
        return LoadActualModelSource(actualFinalTable, actualModel.Resources.Model, actualDrawIndex);
    }

    /// <summary>The actual banner/flag resource supplies original geometry
    /// only. Its final table comes from the current material publisher, not
    /// the flag GBuffer variation's selected renderer.</summary>
    public OriginalDeferredEarlyShadowModelSource LoadFlagSource(
        OriginalNativeWorldShaderTableState actualFinalTable, OriginalRemakeArmyFlagSource actualModel,
        int actualDrawIndex)
    {
        ArgumentNullException.ThrowIfNull(actualModel);
        return LoadActualModelSource(actualFinalTable, actualModel.Model, actualDrawIndex);
    }

    private OriginalDeferredEarlyShadowModelSource LoadActualModelSource(
        OriginalNativeWorldShaderTableState actualFinalTable, LoadedModelMaterials actualModel, int actualDrawIndex)
    {
        ArgumentNullException.ThrowIfNull(actualFinalTable); ArgumentNullException.ThrowIfNull(actualModel);
        gate.Wait();
        try { RequireLive(); return LoadModelSource(actualFinalTable, actualModel, actualDrawIndex, null); }
        finally { gate.Release(); }
    }

    // Caller holds gate. Loaded Model identity is the resource generation,
    // shared by all current actors and all imported final tables for it.
    private OriginalDeferredEarlyShadowModelSource LoadModelSource(
        OriginalNativeWorldShaderTableState actualFinalTable, LoadedModelMaterials actualModel,
        int actualDrawIndex, OriginalMapStaticModelAsset? actualStaticAsset)
    {
        actualFinalTable.Registry.RequireCurrent(actualFinalTable);
        if (sources.TryGetValue(actualModel, out var rows) && rows.TryGetValue((actualFinalTable, actualDrawIndex), out var current))
            return current;
        if (!G1ModelDrawReader.Read(actualModel.Geometry).Draws.Any(draw => draw.Index == actualDrawIndex))
            throw new InvalidDataException("Early19 source draw is absent from the actual imported Army/Flag/Model geometry.");
        var selected = Source.ReadFinalInterface(actualFinalTable.TableOwner);
        LoadedOriginalDeferredEarlyShadowRenderer? renderer = null;
        if (selected.Boundary == OriginalDeferredEarlyShadowInterfaceBoundary.RequiresRenderer)
        {
            var provider = selected.Interface!.Provider!;
            if (!renderers.TryGetValue(provider, out renderer))
                renderers.Add(provider, renderer = Source.LoadRenderer(repository, selected));
        }
        var profile = actualStaticAsset is null ? OriginalArmySkinVertexAdapter.Select(actualModel.Geometry, actualDrawIndex) : null;
        current = new(this, actualFinalTable, actualModel, actualStaticAsset, actualDrawIndex, selected, renderer, profile);
        if (rows is null) sources.Add(actualModel, rows = new());
        rows.Add((actualFinalTable, actualDrawIndex), current);
        return current;
    }

    /// <summary>CPU-only preparation of the current original state3 program.
    /// The same source-pinned result is used by the device upload cache.</summary>
    public OriginalModelShaderProgramPreparation PrepareProgramSource(OriginalDeferredEarlyShadowModelProgramSelection actualSelection)
    {
        ArgumentNullException.ThrowIfNull(actualSelection);
        gate.Wait();
        try { RequireSource(actualSelection.Source); return PrepareProgramSourceCore(actualSelection); }
        finally { gate.Release(); }
    }

    private OriginalModelShaderProgramPreparation PrepareProgramSourceCore(OriginalDeferredEarlyShadowModelProgramSelection selection)
    {
        var prepared = OriginalModelShaderProgram.PrepareSource(selection.Source.Renderer!.Shader, selection.Program.CombinationIndex, compiler);
        return selection.Source.ArmySkinProfile is { } profile && prepared.VertexProgramIndex is >= 4 and <= 7
            ? OriginalDeferredEarlyShadowArmyVertexTransport.Adapt(prepared, profile) : prepared;
    }

    internal void RequireCurrentVertexTransport(OriginalDeferredEarlyShadowModelProgramSelection selection,
        OriginalModelShaderProgram program, OriginalModelStageInputs actualVertex)
    {
        if (selection.Source.ArmySkinProfile is { } profile && program.VertexProgramIndex is >= 4 and <= 7)
            OriginalDeferredEarlyShadowArmyVertexTransport.RequireCurrentGates(program.VertexBindings, profile, actualVertex);
    }

    /// <summary>Retire the imported model generation before its source owner
    /// or sampled material resources retire. Draws go first, then its uploaded
    /// vertex/index resources and source handles. Shared state3 programs and
    /// target loans remain owned by this device component.</summary>
    public async Task RetireModelAsync(LoadedModelMaterials actualModel)
    {
        ArgumentNullException.ThrowIfNull(actualModel);
        await gate.WaitAsync();
        try
        {
            RequireLive();
            var uploads = geometry.Where(row => ReferenceEquals(row.Key.Item1, actualModel)).ToArray();
            var ownedGeometry = uploads.Select(row => row.Value).ToHashSet(ReferenceEqualityComparer.Instance);
            for (int i = draws.Count - 1; i >= 0; i--)
                if (ownedGeometry.Contains(draws[i].Geometry))
                { await draws[i].Draw.DisposeAsync(); draws.RemoveAt(i); }
            foreach (var upload in uploads)
            { await upload.Value.DisposeAsync(); geometry.Remove(upload.Key); }
            sources.Remove(actualModel);
        }
        finally { gate.Release(); }
    }
}
