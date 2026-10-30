using San14.Data;

/// <summary>A classification's sends into the actual shared CGRS output bank.
/// The output epoch owns bus/DSP lifetime; a scene owns only this route borrow.</summary>
internal sealed class OriginalSpatialOutput : IDisposable
{
    internal AudioMusicOutputGraph Graph { get; }
    internal AudioSpatialOutputPlan Plan { get; }
    internal IReadOnlyList<string> Buses { get; }
    internal IReadOnlyList<SpatialAudioSend> Sends { get; }
    internal string Bus => Buses[0];
    internal float ClassificationGain => Graph.Classification.Routes[0].Gain;

    internal OriginalSpatialOutput(OriginalAudioOutputs owner, AudioMusicOutputGraph graph)
        : this(owner, AudioSpatialOutputResources.ReadFreshVoicePlan(graph)) { }

    internal OriginalSpatialOutput(OriginalAudioOutputs owner, AudioSpatialOutputPlan plan)
    {
        Graph = plan.Graph;
        Plan = plan;
        Sends = owner.PrepareSpatialSends(plan);
        Buses = Sends.Select(send => send.Bus).ToArray();
    }

    public void Dispose() { }
}
