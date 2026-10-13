using San14.Data;
using San14.Data.Formats;

public sealed partial class RemakeArmyNativeRenderer
{
    private ArmyWorldQueuePreparedPackets? _worldPackets;
    private bool _worldRetired;

    /// <summary>Prepares every body draw from the actual current pose prefix.
    /// Its source alpha partition is retained for node49/50. No GPU draw or
    /// native order is inferred from the returned canonical inventory.</summary>
    public ArmyWorldQueuePreparedPackets PrepareWorldQueuePackets(
        NativeMapArmyPreparedFrame actualArmy,
        IReadOnlyList<ModelBlendAttachment> actualNativeSixColorStates,
        NativeWorldShaderTableRegistry actualSharedTableRegistry)
    {
        ObjectDisposedException.ThrowIf(_worldRetired, this);
        ArmyWorldQueuePreparedPackets.RequireFrame(actualArmy);
        ArgumentNullException.ThrowIfNull(actualSharedTableRegistry);
        ArmyWorldQueuePreparedPackets.RequireColorInputScope(actualArmy, actualNativeSixColorStates);
        _worldPackets = null;
        ModelRasterInputs? raster = null;
        var packets = new List<ModelGbufferInstancePacket>();
        var selections = new List<ArmyWorldQueuePrimitiveSource>();
        var tables = new List<ArmyWorldPrimitiveTableRow>();
        var pendingMaterials = new Dictionary<string, ArmyDrawMaterialState>(StringComparer.Ordinal);
        float seconds = actualArmy.Animation.CallerSeconds;
        foreach (var (unit, frame) in actualArmy.Ordinary)
        {
            if (!_epochs.TryGetValue(unit, out var epoch) || !ReferenceEquals(epoch.Initialization, frame.Initialization))
                _epochs[unit] = epoch = (frame.Initialization, seconds);
            foreach (var actor in frame.Publications.Values)
            {
                var model = actualArmy.Models[actor.Category];
                var sources = _sources.Get(model);
                foreach (var draw in sources.Draws)
                {
                    raster ??= ArmyWorldQueuePreparedPackets.PreparationRaster(actualNativeSixColorStates);
                    var source = sources.GetDraw(draw.Index);
                    string packet = $"remake-army/{unit}/{actor.ComponentId}/draw{draw.Index}";
                    string writer = sources.WorldTables.ImportFreshNullWrapper(model.Catalog, actor.Category, model.Transforms.Geometry, draw.Index);
                    var actualFullTable=sources.WorldTables.ReadActualFreshPriorTable(model.Catalog,actor.Category,model.Transforms.Geometry,draw.Index,
                        actualSharedTableRegistry.RetainImportedTable(source.SourceDraw.GraphSource.Draw.Binding.Shader));
                    var actualSelected=actualSharedTableRegistry.ReadSelectedInterface(actualFullTable,18);
                    if(actualSelected.Provider?.ShaderFileHash!=source.SourceDraw.Shader.Source.FileId)
                        throw new NotSupportedException("Actual owned body fulltable requires a different source program/inputs producer.");
                    tables.Add(new(packet, actualFullTable,
                        writer + ";C34B76-actual-base-full-table"));
                    var selection = sources.WorldQueues.Read(packet, source.SourceDraw.GraphSource.Graph,
                        source.SourceDraw.GraphSource.Draw.Binding.Model, draw.Index, actualSelected, source.SourceDraw.Shader);
                    if (!selection.Opaque49.SourceQueueSelectsShader)
                        throw new NotSupportedException("Ordinary B90 source body did not select original opaque queue49.");
                    var material = _materials.Read(packet, source, frame.Initialization);
                    var prepared = source.Prepare(packet, new(actor.AnimationInput with { CurrentTimeSeconds = seconds - epoch.Start },
                        actor.WorldTransform, actualArmy.Context.PreparedCamera!,
                        new(true, [true,false,false], actualArmy.Animation.ClipPolicy),
                        new byte[6256], material?.Pixel ?? new byte[80], material?.Flags ?? 0, false, null, raster));
                    packets.Add(prepared.Packet); selections.Add(selection);
                    pendingMaterials[packet] = new(source, frame.Initialization, prepared.Draw.NextCachedMaterialFlags, prepared.Packet.Pixel.Constants[0].ToArray());
                }
            }
        }
        ArmyWorldQueuePreparedPackets? result = null;
        result = new(actualArmy, "ArmyOrdinary", packets, selections, tables, () =>
        {
            ObjectDisposedException.ThrowIf(_worldRetired, this);
            if (!ReferenceEquals(_worldPackets, result))
                throw new InvalidDataException("Army body packet preparation was superseded or retired.");
        });
        _materials.Commit(pendingMaterials);
        _sources.Retain(actualArmy.Models.Keys.ToHashSet());
        foreach (int old in _epochs.Keys.Where(k => !actualArmy.Ordinary.ContainsKey(k)).ToArray()) _epochs.Remove(old);
        return _worldPackets = result;
    }
}
