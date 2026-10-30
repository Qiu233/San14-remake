using Godot;
using San14.Data;

internal sealed partial class OriginalAudioOutputs
{
    private readonly Dictionary<int, IReadOnlyList<string>> spatialBuses = [];
    private OriginalSpatialSpread? spatialSpread;
    private ulong spatialOwner;
    private string spatialMaster = "";

    private void InitializeSpatialOutputs(ulong owner)
    {
        spatialOwner = owner;
        foreach (var row in buses) spatialBuses.Add(row.Key, new[] { row.Value });
        spatialMaster = buses.Single(row => EffectsGraph.Outputs[row.Key].NodeKind == 7).Value;
        var plan = AudioSpatialOutputResources.ReadFreshEffectsPlan(EffectsGraph);
        if (plan.SpreadControls.Count == 0) return;
        if (mixRate != 48000) throw new NotSupportedException("audio.spatial.control_mixer_rate");
        var detectors = new Dictionary<uint, string>();
        foreach (uint key in plan.SpreadControls.SelectMany(control => control.TriggerOutputKeys).Distinct())
        {
            var node = EffectsGraph.Outputs.Single(output => output.Key == key);
            //The native generic/compressor getters reject8003; onlykind4
            //has the actual detector. Failed trigger getters are ignored.
            if (node.NodeKind != 4) continue;
            detectors.Add(key, PrepareSpatialNode(node.Index)[0]);
        }
        spatialSpread = new(plan, mixRate, spatialMaster, detectors);
    }

    internal IReadOnlyList<SpatialAudioSend> PrepareSpatialSends(AudioSpatialOutputPlan plan)
    {
        ValidateHost();
        if (plan.Graph.Source.DecodedSha256 != EffectsGraph.Source.DecodedSha256)
            throw new InvalidOperationException("audio.spatial.output_resource_replaced");
        if (plan.FreshProcessors.Any(processor => processor.FreshParameters != new AudioCompressorParameters(1, 1, 0, 0, 0, 1)))
            throw new NotSupportedException("Spatial sound needs an active compressor consumer.");
        AudioServer.Lock();
        try
        {
            //Control inputs carry real PCM into their source detector. Their
            //bus is silent after processing, rather than dropping the input.
            return plan.Inputs.SelectMany(input => PrepareSpatialNode(input.OutputIndex)
                .Select(bus => new SpatialAudioSend(bus, input.Gain, input.IsControl))).ToArray();
        }
        finally { AudioServer.Unlock(); }
    }

    private IReadOnlyList<string> PrepareSpatialNode(int index)
    {
        if (spatialBuses.TryGetValue(index, out var existing)) return existing;
        if ((uint)index >= EffectsGraph.Outputs.Count)
            throw new InvalidDataException("audio.spatial.output_index");
        var node = EffectsGraph.Outputs[index];
        if (node.NodeKind is not (0 or 2 or 4 or 5 or 7) || node.Variant > 1 ||
            (node.NodeKind is 2 or 4 or 5) && node.Variant != 0 ||
            (node.Flags & ~1u) != 0 || node.GainKeys.Count != 0)
            throw new NotSupportedException("audio.spatial.output_processor");
        if (node.NodeKind is not (0 or 4) && node.DownstreamIndices.Count > 1)
            throw new NotSupportedException("Spatial stateful processor has multiple audio sends.");
        if (node.NodeKind == 4 && mixRate != 48000)
            throw new NotSupportedException("audio.spatial.control_mixer_rate");
        //A kind4 output's links are control attachments, not audible sends.
        var targets = node.NodeKind == 4 ? new[] { spatialMaster } :
            node.DownstreamIndices.SelectMany(PrepareSpatialNode).ToArray();
        if (targets.Length == 0)
            throw new InvalidDataException("audio.spatial.output_master_identity");
        var names = new string[targets.Length];
        for (int branch = 0; branch < names.Length; branch++)
        {
            string name = $"OriginalAudio{spatialOwner}_{node.Key:X8}_{branch}";
            if (AudioServer.GetBusIndex(name) >= 0) throw new InvalidOperationException("Spatial output already exists.");
            int slot = AudioServer.BusCount;
            AudioServer.AddBus(); AudioServer.SetBusName(slot, name);
            creationOrder.Add(name);
            AudioServer.SetBusSend(slot, targets[branch]);
            AudioServer.SetBusVolumeLinear(slot, node.NodeKind == 4 ? 0f : node.StaticGain);
            names[branch] = name;
        }
        spatialBuses.Add(index, names);
        return names;
    }

    internal float ReadSpatialSpread(AudioSpatialOutputPlan plan)
    {
        if (plan.SpreadControlIndices.Count == 0) return 0f;
        return (spatialSpread ?? throw new InvalidOperationException("audio.spatial.spread_owner_missing")).Read(plan);
    }

    private void DisposeSpatialOutputs()
    {
        spatialSpread?.Dispose(); spatialSpread = null;
        spatialBuses.Clear();
    }
}
