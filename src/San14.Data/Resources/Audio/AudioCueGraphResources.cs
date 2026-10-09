using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data;

public sealed record AudioCueGraphNode(int Offset, uint Opcode, int DeclaredHeaderBytes,
    uint Flags, IReadOnlyList<int> ChildOffsets, uint? SoundId);
public sealed record AudioCueGraph(AudioNamedCue Cue, LoadedRdbAsset Source,
    IReadOnlyList<int> RootOffsets, IReadOnlyList<AudioCueGraphNode> Nodes);
public sealed record AudioCueSoundReference(uint CueId, string CueName, uint SoundId,
    int CueSectionOffset, int LeafOffset, LoadedRdbAsset Source);

/// <summary>
/// Parses the original direct group/sample-reference subset of KTSR cue graphs. Original
/// opcode dispatch and relative pointer reads are confirmed in both installed CE executables.
/// Only group opcode 51D25082 with selector bits 16..19 zero and leaf opcode 6282CA29 are
/// supported. Stateful/random selectors, gain, scheduling and current playback are not inferred.
/// </summary>
public static class AudioCueGraphResources
{
    public const uint DirectGroupOpcode = 0x51D25082;
    public const uint SoundReferenceOpcode = 0x6282CA29;

    public static AudioCueGraph Read(AudioSelectionBank bank, string cueName)
    {
        ArgumentNullException.ThrowIfNull(bank);
        ArgumentException.ThrowIfNullOrEmpty(cueName);
        AudioNamedCue[] cues = bank.Cues.Where(c => c.Name == cueName).ToArray();
        if (cues.Length != 1) throw new InvalidDataException("A cue graph name must identify exactly one original cue section.");
        AudioNamedCue cue = cues[0];
        byte[] b = bank.Source.Bytes;
        if (!Convert.ToHexString(SHA256.HashData(b)).Equals(bank.Source.DecodedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Audio selection source changed after parsing.");
        int start = cue.SectionOffset, end = checked(start + cue.SectionBytes);
        var nodes = new Dictionary<int, AudioCueGraphNode>();
        var visiting = new HashSet<int>();
        uint U32(int at)
        {
            Range(at, 4);
            return BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at, 4));
        }
        void Range(int at, int size)
        {
            if (size < 0 || at < start || at > end || size > end - at)
                throw new InvalidDataException("Audio cue graph pointer extends outside its original cue section.");
        }
        int Offset(int baseOffset, uint relative)
        {
            long at = (long)baseOffset + relative;
            if (at > int.MaxValue) throw new InvalidDataException("Audio cue relative pointer exceeds int32.");
            Range((int)at, 4);
            return (int)at;
        }
        IReadOnlyList<int> Children(int baseOffset, int countField, int pointerField)
        {
            uint count = U32(baseOffset + countField);
            if (count > 1024) throw new InvalidDataException("Audio cue child count exceeds its bounded graph budget.");
            if (count == 0) return Array.Empty<int>();
            int table = Offset(baseOffset, U32(baseOffset + pointerField));
            Range(table, checked((int)count * 4));
            var children = new int[count];
            for (int i = 0; i < children.Length; i++) children[i] = Offset(baseOffset, U32(table + 4 * i));
            return Array.AsReadOnly(children);
        }
        void Visit(int offset, int depth)
        {
            if (depth > 64 || visiting.Contains(offset))
                throw new InvalidDataException("Audio cue graph is cyclic or exceeds the recursion budget.");
            if (nodes.ContainsKey(offset)) return;
            if (nodes.Count >= 1024) throw new InvalidDataException("Audio cue graph exceeds the node budget.");
            Range(offset, 24);
            uint opcode = U32(offset), rawSize = U32(offset + 4);
            if (rawSize is < 24 or > int.MaxValue)
                throw new InvalidDataException("Audio cue node header length is invalid.");
            // This length covers the node header, not its recursively referenced children.
            Range(offset, (int)rawSize);
            uint flags = U32(offset + 12);
            visiting.Add(offset);
            if (opcode == DirectGroupOpcode)
            {
                if ((flags & 0xF0000) != 0)
                    throw new NotSupportedException("Audio cue group uses an unimplemented stateful/random selector.");
                IReadOnlyList<int> children = Children(offset, 16, 20);
                foreach (int child in children) Visit(child, depth + 1);
                if (nodes.Count >= 1024) throw new InvalidDataException("Audio cue graph exceeds the node budget.");
                nodes.Add(offset, new(offset, opcode, (int)rawSize, flags, children, null));
            }
            else if (opcode == SoundReferenceOpcode)
                nodes.Add(offset, new(offset, opcode, (int)rawSize, flags, Array.Empty<int>(), U32(offset + 20)));
            else
                throw new NotSupportedException($"Audio cue opcode {opcode:X8} is outside the verified direct graph subset.");
            visiting.Remove(offset);
        }
        Range(start, 48);
        IReadOnlyList<int> roots = Children(start, 32, 36);
        foreach (int root in roots) Visit(root, 0);
        return new(cue, bank.Source, roots, Array.AsReadOnly(nodes.Values.OrderBy(n => n.Offset).ToArray()));
    }

    /// <summary>Returns the SoundID read by a single static reference chain. It is not a claim
    /// that the original runtime's registry, filters or mixer actually played this sample.</summary>
    public static AudioCueSoundReference ReadSingleSoundReference(AudioSelectionBank bank, string cueName)
    {
        AudioCueGraph graph = Read(bank, cueName);
        if (graph.RootOffsets.Count != 1)
            throw new NotSupportedException("Multiple cue roots require an explicit playback graph.");
        var nodes = graph.Nodes.ToDictionary(n => n.Offset);
        AudioCueGraphNode node = nodes[graph.RootOffsets[0]];
        while (node.SoundId is null)
        {
            if (node.ChildOffsets.Count != 1)
                throw new NotSupportedException("A cue with multiple/empty group children has no single static sound reference.");
            node = nodes[node.ChildOffsets[0]];
        }
        return new(graph.Cue.CueId, cueName, node.SoundId.Value, graph.Cue.SectionOffset, node.Offset, graph.Source);
    }
}
