using Godot;
using San14.Data;

/// <summary>The selected spatial cue's actual audible output graph. Category
/// controls remain at the player; control inputs are not audible sends.</summary>
internal sealed class OriginalSpatialOutput : IDisposable
{
    private readonly List<string> buses = [];
    internal AudioMusicOutputGraph Graph { get; }
    internal AudioSpatialOutputPlan Plan { get; }
    internal IReadOnlyList<string> Buses { get; }
    internal IReadOnlyList<SpatialAudioSend> Sends { get; }
    internal string Bus => Buses[0];
    internal float ClassificationGain => Graph.Classification.Routes[0].Gain;

    internal OriginalSpatialOutput(ulong owner, AudioMusicOutputGraph graph)
        : this(owner, AudioSpatialOutputResources.ReadFreshVoicePlan(graph)) { }

    internal OriginalSpatialOutput(ulong owner, AudioSpatialOutputPlan plan)
    {
        Graph = plan.Graph;
        Plan = plan;
        //The native fresh factories publish identity compressor parameters.
        //Registered, unrequested control declarations do not change that setup.
        //An actual later compressor-control command needs its own DSP consumer.
        if (Plan.FreshProcessors.Any(processor => processor.FreshParameters != new AudioCompressorParameters(1, 1, 0, 0, 0, 1)))
            throw new NotSupportedException("Spatial sound needs an active compressor consumer.");
        var prepared = new Dictionary<int, IReadOnlyList<string>>();
        AudioServer.Lock();
        try
        {
            Sends = Plan.Inputs.Where(input => !input.IsControl)
                .SelectMany(input => Prepare(input.OutputIndex).Select(bus => new SpatialAudioSend(bus, input.Gain))).ToArray();
            Buses = Sends.Select(send => send.Bus).ToArray();

            IReadOnlyList<string> Prepare(int index)
            {
                if (prepared.TryGetValue(index, out var existing)) return existing;
                var output = Graph.Outputs[index];
                //Only the linear submix may split. A stateful processor cannot
                //be duplicated to emulate an otherwise unimplemented fan-out.
                if (output.DownstreamIndices.Count > 1 && output.NodeKind != 0)
                    throw new NotSupportedException("Spatial voice processor has multiple audio sends.");
                var sends = output.DownstreamIndices.SelectMany(Prepare).ToArray();
                var roots = new string[Math.Max(1, sends.Length)];
                for (int edge = 0; edge < roots.Length; edge++)
                {
                    string name = $"OriginalSpatial{owner}_{Graph.Classification.Id:X8}_{output.Key:X8}_{edge}";
                    if (AudioServer.GetBusIndex(name) >= 0) throw new InvalidOperationException("Voice output already exists.");
                    int slot = AudioServer.BusCount;
                    AudioServer.AddBus(); AudioServer.SetBusName(slot, name);
                    buses.Add(name);
                    AudioServer.SetBusVolumeLinear(slot, output.StaticGain);
                    if (sends.Length > 0) AudioServer.SetBusSend(slot, sends[edge]);
                    roots[edge] = name;
                }
                prepared.Add(index, roots);
                return roots;
            }
        }
        catch { Dispose(); throw; }
        finally { AudioServer.Unlock(); }
    }

    public void Dispose()
    {
        foreach (string name in buses.AsEnumerable().Reverse())
        {
            int index = AudioServer.GetBusIndex(name);
            if (index >= 0) AudioServer.RemoveBus(index);
        }
        buses.Clear();
    }
}
