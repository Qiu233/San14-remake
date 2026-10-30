using System.Buffers.Binary;
using System.Text;

namespace San14.Data;

public sealed record AudioSpatialOutputSend(int FromOutputIndex, int ToOutputIndex);
public sealed record AudioSpatialOutputInput(int OutputIndex, float Gain, bool IsControl);
public sealed record AudioSpatialCategoryGain(uint Key, float InitialValue);
public sealed record AudioCompressorParameters(float Threshold, float CompressionSlope,
    float KneeWidthDb, float AttackSeconds, float ReleaseSeconds, float MakeupGain);
public sealed record AudioSpatialCompressorControl(uint ControlId, int TargetOutputIndex,
    string Name, AudioCompressorParameters Parameters);
public sealed record AudioSpatialOutputProcessor(int OutputIndex,
    AudioCompressorParameters FreshParameters);
public sealed record AudioSpatialOutputPlan(AudioMusicOutputGraph Graph, int EntryOutputIndex,
    IReadOnlyList<int> AudibleOutputIndices, IReadOnlyList<AudioSpatialOutputSend> AudioSends,
    IReadOnlyList<AudioSpatialOutputSend> ControlSends,
    IReadOnlyList<AudioSpatialOutputProcessor> FreshProcessors,
    IReadOnlyList<AudioSpatialCompressorControl> AvailableCompressorControls)
{
    public IReadOnlyList<AudioSpatialOutputInput> Inputs { get; init; } = [];
    public IReadOnlyList<AudioSpatialCategoryGain> InitialCategoryGains { get; init; } = [];
    /// <summary>Loaded CGRS listener distance scale; absent only for an unprepared legacy plan.</summary>
    public float? SourceDistanceScale { get; init; }
    /// <summary>The real classification constructor starts its spread at zero.</summary>
    public float? SourceSpreadGain { get; init; }
    public IReadOnlyList<AudioSpatialSpreadControl> SpreadControls { get; init; } = [];
    public IReadOnlyList<uint> SpreadControlKeys { get; init; } = [];
    public IReadOnlyList<int> SpreadControlIndices { get; init; } = [];
}

/// <summary>Fresh output-host construction and the separate, callable CGRS
/// controls. Registering a control declaration does not execute it. A host
/// accepting a later control request must apply its parameters explicitly.</summary>
public static partial class AudioSpatialOutputResources
{
    public static AudioSpatialOutputPlan ReadFreshVoicePlan(AudioMusicOutputGraph graph)
        => ReadFreshPlan(graph, 0xFE924B28);

    public static AudioSpatialOutputPlan ReadFreshEffectsPlan(AudioMusicOutputGraph graph)
        => ReadFreshPlan(graph, 0xF4E09144);

    private static AudioSpatialOutputPlan ReadFreshPlan(AudioMusicOutputGraph graph, uint categoryKey)
    {
        ArgumentNullException.ThrowIfNull(graph);
        bool gainKeysKnown = graph.Classification.GainKeys.SequenceEqual(new[] { categoryKey }) ||
            categoryKey == 0xF4E09144 && graph.Classification.GainKeys.SequenceEqual(new uint[] { 0x744A80CF, categoryKey });
        if (graph.Classification.Routes.Count == 0 || graph.Classification.Routes.Any(route => route.Kind != 0) || !gainKeysKnown)
            throw new NotSupportedException("Spatial sound needs its source classification and category control.");
        var inputs = new List<AudioSpatialOutputInput>();
        foreach (var route in graph.Classification.Routes)
        {
            var selected = graph.Outputs.Where(node => node.Key == route.OutputKey).ToArray();
            if (selected.Length != 1) throw new InvalidDataException("Spatial sound output is not unique.");
            inputs.Add(new(selected[0].Index, route.Gain, selected[0].NodeKind == 4));
        }
        var starts = inputs.Where(input => !input.IsControl).ToArray();
        if (starts.Length == 0) throw new NotSupportedException("Spatial sound has no audible classification send.");
        var audible = new List<int>();
        var sends = new List<AudioSpatialOutputSend>();
        var visiting = new HashSet<int>();
        var visited = new HashSet<int>();
        foreach (var input in starts) Visit(input.OutputIndex);
        var controls = new List<AudioSpatialOutputSend>();
        foreach (var node in graph.Outputs.Where(node => node.NodeKind == 4))
        {
            if (node.Variant != 0) throw new NotSupportedException("Spatial duck control variant is unsupported.");
            foreach (int to in node.DownstreamIndices)
            {
                if ((uint)to >= graph.Outputs.Count || graph.Outputs[to].NodeKind != 5)
                    throw new InvalidDataException("Spatial duck control needs its compressor target.");
                if (visited.Contains(to)) controls.Add(new(node.Index, to));
            }
        }
        //12C2260/12C2680 publish this actual setup through12C2160 and the
        //native effect parameter setter.12D29B0 is an identity at these values.
        //The independent6D8C8F46 records below are not this setup packet.
        var processors = audible.Where(index => graph.Outputs[index].NodeKind is 2 or 5)
            .Select(index => new AudioSpatialOutputProcessor(index, new(1, 1, 0, 0, 0, 1))).ToArray();
        var initialGains = ReadInitialCategoryGains(graph, out float distanceScale);
        var spreadControls = ReadSpreadControls(graph, out var spreadKeys, out var spreadIndices);
        return new(graph, starts[0].OutputIndex, audible.AsReadOnly(), sends.AsReadOnly(), controls.AsReadOnly(),
            Array.AsReadOnly(processors), ReadCompressorControls(graph))
            { Inputs = inputs.AsReadOnly(), InitialCategoryGains = initialGains, SourceDistanceScale = distanceScale,
                SourceSpreadGain = 0f, SpreadControls = spreadControls,
                SpreadControlKeys = spreadKeys, SpreadControlIndices = spreadIndices };

        void Visit(int index)
        {
            if ((uint)index >= graph.Outputs.Count || !visiting.Add(index))
                throw new InvalidDataException("Spatial output graph has an invalid edge or cycle.");
            if (visited.Contains(index)) { visiting.Remove(index); return; }
            var node = graph.Outputs[index];
            if (node.NodeKind is not (0 or 2 or 5 or 7) || node.Variant > 1 ||
                (node.NodeKind is 2 or 5) && node.Variant != 0 ||
                (node.Flags & ~1u) != 0 || node.GainKeys.Count != 0)
                throw new NotSupportedException("Spatial output processor is outside the fresh source factories.");
            if (node.NodeKind == 7 ? node.DownstreamIndices.Count != 0 : node.DownstreamIndices.Count == 0)
                throw new InvalidDataException("Spatial output has an invalid master termination.");
            audible.Add(index);
            foreach (int to in node.DownstreamIndices)
            {
                //12C6B60 passes every target to native SetOutputVoices, and
                //12C6A70 writes a unity six-to-six matrix for each real send.
                sends.Add(new(index, to));
                Visit(to);
            }
            visiting.Remove(index); visited.Add(index);
        }
    }

    private static IReadOnlyList<AudioSpatialCategoryGain> ReadInitialCategoryGains(AudioMusicOutputGraph graph, out float distanceScale)
    {
        byte[] bytes = graph.Source.Bytes;
        uint U(int at)
        {
            if (at < 0 || at > bytes.Length - 4) throw new InvalidDataException("Spatial category field is outside its resource.");
            return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at, 4));
        }
        for (int at = 80; at < bytes.Length;)
        {
            int size = checked((int)U(at + 4));
            if (size < 8 || size > bytes.Length - at) throw new InvalidDataException("Spatial category section is incomplete.");
            if (U(at) == 0xE0736214)
            {
                if (size < 0x60) throw new InvalidDataException("Spatial category master is incomplete.");
                distanceScale = BitConverter.Int32BitsToSingle(unchecked((int)U(at + 0x44)));
                if (!float.IsFinite(distanceScale))
                    throw new InvalidDataException("Spatial listener distance scale is nonfinite.");
                int count = checked((int)U(at + 0x20)), offset = checked((int)U(at + 0x24));
                if (count > 1024 || offset < 0 || offset > size || count > (size - offset) / 12)
                    throw new InvalidDataException("Spatial category initial gain table is invalid.");
                var result = new List<AudioSpatialCategoryGain>();
                foreach (uint key in graph.Classification.GainKeys)
                {
                    int found = -1;
                    for (int row = 0; row < count; row++)
                        if (U(at + offset + row * 12) == key)
                        { if (found >= 0) throw new InvalidDataException("Ambiguous spatial category gain."); found = row; }
                    if (found < 0) throw new InvalidDataException("Spatial category initial gain is absent.");
                    float value = BitConverter.Int32BitsToSingle(unchecked((int)U(at + offset + found * 12 + 4)));
                    if (!float.IsFinite(value) || value < 0) throw new InvalidDataException("Invalid spatial category initial gain.");
                    result.Add(new(key, value));
                }
                return result.AsReadOnly();
            }
            at = checked(at + size);
        }
        throw new InvalidDataException("Spatial category master is absent.");
    }

    private static IReadOnlyList<AudioSpatialCompressorControl> ReadCompressorControls(AudioMusicOutputGraph graph)
    {
        byte[] bytes = graph.Source.Bytes;
        var result = new List<AudioSpatialCompressorControl>();
        for (int at = 80; at < bytes.Length;)
        {
            uint U(int relative)
            {
                if (relative < 0 || at > bytes.Length - 4 || relative > bytes.Length - at - 4)
                    throw new InvalidDataException("Spatial control field is outside its resource.");
                return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at + relative, 4));
            }
            int size = checked((int)U(4));
            if (size < 8 || size > bytes.Length - at)
                throw new InvalidDataException("Spatial control section is incomplete.");
            if (U(0) == 0x6D8C8F46)
            {
                if (size < 0x20) throw new InvalidDataException("Spatial control header is incomplete.");
                if (U(0x10) == 0xA6A39905)
                {
                    var targets = graph.Outputs.Where(node => node.Key == U(0xC)).ToArray();
                    if (targets.Length != 1) throw new InvalidDataException("Spatial compressor target is not unique.");
                    int offset = checked((int)U(0x14)), count = checked((int)U(0x18));
                    if (count != 20 || offset < 0x20 || offset > size - count)
                        throw new NotSupportedException("Spatial compressor needs its source five-value parameter format.");
                    float F(int index)
                    {
                        float value = BitConverter.Int32BitsToSingle(unchecked((int)U(offset + index * 4)));
                        return float.IsFinite(value) ? value : throw new InvalidDataException("Nonfinite compressor parameter.");
                    }
                    float threshold = F(0), slope = F(1), attack = F(2), release = F(3), makeup = F(4);
                    if (threshold is < 0 or > 1 || slope is < 0 or > 1 || attack is < 0 or > 10 ||
                        release is < 0 or > 10 || makeup is < 0 or > 2)
                        throw new InvalidDataException("Spatial compressor parameter is outside its source domain.");
                    int nameAt = checked((int)U(0x1C));
                    if (nameAt < 0x20 || nameAt >= size)
                        throw new InvalidDataException("Spatial compressor name is outside its section.");
                    int length = bytes.AsSpan(at + nameAt, size - nameAt).IndexOf((byte)0);
                    if (length < 0) throw new InvalidDataException("Spatial compressor name is not terminated.");
                    string name = new UTF8Encoding(false, true).GetString(bytes, at + nameAt, length);
                    //12CF0D0's20-byte arm appends knee0 and mode0.
                    //12CE9C0 moves those fields before12C2160's backend map.
                    result.Add(new(U(8), targets[0].Index, name, new(threshold, slope, 0, attack, release, makeup)));
                }
            }
            at = checked(at + size);
        }
        return result.AsReadOnly();
    }
}
