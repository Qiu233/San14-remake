using San14.Data;
using San14.Data.Formats;

public sealed partial class OriginalRemakeArmyNativeRenderer
{
    private readonly OriginalArmyWorldQueueSourceCache _worldSources = new();
    private readonly OriginalArmyWorldPreparedTableSource _worldTableSources = new();
    private OriginalArmyWorldQueuePreparedPackets? _worldPackets;
    private bool _worldRetired;

    /// <summary>Prepares every body draw from the actual current pose prefix.
    /// Its source alpha partition is retained for node49/50. No GPU draw or
    /// native order is inferred from the returned canonical inventory.</summary>
    public OriginalArmyWorldQueuePreparedPackets PrepareWorldQueuePackets(
        OriginalNativeMapArmyPreparedFrame actualArmy,
        IReadOnlyList<OriginalModelBlendAttachment> actualNativeSixColorStates,
        OriginalNativeWorldShaderTableRegistry actualSharedTableRegistry)
    {
        ObjectDisposedException.ThrowIf(_worldRetired, this);
        OriginalArmyWorldQueuePreparedPackets.RequireFrame(actualArmy);
        ArgumentNullException.ThrowIfNull(actualSharedTableRegistry);
        OriginalArmyWorldQueuePreparedPackets.RequireColorInputScope(actualArmy, actualNativeSixColorStates);
        _worldPackets = null;
        OriginalModelRasterInputs? raster = null;
        var packets = new List<OriginalModelGbufferInstancePacket>();
        var selections = new List<OriginalArmyWorldQueuePrimitiveSource>();
        var tables = new List<OriginalArmyWorldPrimitiveTableRow>();
        float seconds = actualArmy.Animation.CallerSeconds;
        foreach (var (unit, frame) in actualArmy.Ordinary)
        {
            if (!_epochs.TryGetValue(unit, out var epoch) || !ReferenceEquals(epoch.Initialization, frame.Initialization))
                _epochs[unit] = epoch = (frame.Initialization, seconds);
            foreach (var actor in frame.Publications.Values)
            {
                var model = actualArmy.Models[actor.Category];
                if (!_sourceDraws.TryGetValue(actor.Category, out var draws))
                    _sourceDraws.Add(actor.Category, draws = G1ModelDrawReader.Read(model.Transforms.Geometry).Draws);
                foreach (var draw in draws)
                {
                    raster ??= OriginalArmyWorldQueuePreparedPackets.PreparationRaster(actualNativeSixColorStates);
                    var key = (actor.Category, draw.Index);
                    if (!_sources.TryGetValue(key, out var source))
                    {
                        _shaderObjects ??= _repository.LoadObjects("system", 0x718CCB8B);
                        _graph ??= _repository.LoadRenderGraph("system", 0x024496EE);
                        _sources.Add(key, source = OriginalArmyAnimatedGbufferSource.Load(_repository,
                            model, _shaderObjects, _graph, 49, draw.Index));
                    }
                    if (source.Model.Transforms.Source.ResourceUri != model.Transforms.Source.ResourceUri ||
                        source.Model.Transforms.Source.DecodedSha256 != model.Transforms.Source.DecodedSha256)
                        throw new InvalidDataException("Army renderer source generation changed; rebuild the renderer before current World preparation.");
                    string packet = $"remake-army/{unit}/{actor.ComponentId}/draw{draw.Index}";
                    string writer = _worldTableSources.ImportFreshNullWrapper(model.Catalog, actor.Category, model.Transforms.Geometry, draw.Index);
                    var actualFullTable=_worldTableSources.ReadActualFreshPriorTable(model.Catalog,actor.Category,model.Transforms.Geometry,draw.Index,
                        actualSharedTableRegistry.RetainImportedTable(source.SourceDraw.GraphSource.Draw.Binding.Shader));
                    var actualSelected=actualSharedTableRegistry.ReadSelectedInterface(actualFullTable,18);
                    if(actualSelected.Provider?.ShaderFileHash!=source.SourceDraw.Shader.Source.FileId)
                        throw new NotSupportedException("Actual owned body fulltable requires a different source program/inputs producer.");
                    tables.Add(new(packet, actualFullTable,
                        writer + ";C34B76-actual-base-full-table"));
                    var selection = _worldSources.Read(packet, source.SourceDraw.GraphSource.Graph,
                        source.SourceDraw.GraphSource.Draw.Binding.Model, draw.Index, actualSelected, source.SourceDraw.Shader);
                    if (!selection.Opaque49.SourceQueueSelectsShader)
                        throw new NotSupportedException("Ordinary B90 source body did not select original opaque queue49.");
                    _materials.TryGetValue(packet, out var material);
                    var prepared = source.Prepare(packet, new(actor.AnimationInput with { CurrentTimeSeconds = seconds - epoch.Start },
                        actor.WorldTransform, actualArmy.Context.PreparedCamera!,
                        new(true, [true,false,false], actualArmy.Animation.ClipPolicy),
                        new byte[6256], material.Pixel ?? new byte[80], material.Flags, false, null, raster));
                    packets.Add(prepared.Packet); selections.Add(selection);
                    _materials[packet] = (prepared.Draw.NextCachedMaterialFlags, prepared.Packet.Pixel.Constants[0].ToArray());
                }
            }
        }
        var keep = packets.Select(p => p.InstanceKey).ToHashSet(StringComparer.Ordinal);
        foreach (string old in _materials.Keys.Where(k => !keep.Contains(k)).ToArray()) _materials.Remove(old);
        foreach (int old in _epochs.Keys.Where(k => !actualArmy.Ordinary.ContainsKey(k)).ToArray()) _epochs.Remove(old);
        OriginalArmyWorldQueuePreparedPackets? result = null;
        result = new(actualArmy, "ArmyOrdinary", packets, selections, tables, () =>
        {
            ObjectDisposedException.ThrowIf(_worldRetired, this);
            if (!ReferenceEquals(_worldPackets, result))
                throw new InvalidDataException("Army body packet preparation was superseded or retired.");
        });
        return _worldPackets = result;
    }
}
