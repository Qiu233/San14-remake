using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record DeferredEarlyF103LoadedProgram(int PixelIndex, int VertexIndex,
    string PixelSha256, string VertexSha256, DxbcResourceDefinitions PixelDefinitions,
    G1ShaderResourceBindings ResourceBindings);

public static partial class DeferredEarlyF103SourceSelection
{
    // This directory holds source-only tables. Current queries, feature/variant
    // bytes, Camera/light packets, selected frames and GPU receipts are absent.
    private sealed class LoadedSource
    {
        public LoadedSource(LoadedDeferredTexturePlan original)
        {
            Pin = ReadPin(original.Node.Index);
            Source = ReadPinnedSource(original, Pin);
            Tables = Source.Shader.ReadG1Bindings();
            if (Tables.PassCount != 1 || !Tables.RawDimensionCounts.SequenceEqual(new uint[] { 1, 0, 0, 0, 0, 0, 192 }) ||
                Tables.Variants.RecordWidth != 96 || Tables.Variants.Records.Count != 192)
                throw new InvalidDataException("Early F103 source selection table shape changed.");
            InitialVariant = Tables.Variants.Records[0].RawBytes.ToArray();
            CapabilityMasks = new uint[21];
            foreach (var row in Tables.Variants.Records)
                for (int word = 0; word < CapabilityMasks.Length; word++)
                    CapabilityMasks[word] |= 1u << (int)(Word(row.RawBytes, word) & 31);
            uint[] expectedMasks = [2, 3, 3, 3, 1, 1, 1, 1, 1, 3, 3, 9, 3, 1, 1, 7, 1, 3, 1, 1, 1];
            if (!CapabilityMasks.SequenceEqual(expectedMasks) || Word(InitialVariant, 0) != 1 || Word(InitialVariant, 11) != 3 ||
                Enumerable.Range(0, 24).Any(i => i != 0 && i != 11 && Word(InitialVariant, i) != 0))
                throw new InvalidDataException("Early F103 native row0 or first21 capability masks changed.");
        }
        public Pin Pin { get; }
        public LoadedDeferredTexturePlan Source { get; }
        public G1ShaderBindingTables Tables { get; }
        public byte[] InitialVariant { get; }
        public uint[] CapabilityMasks { get; }
        public ConcurrentDictionary<int, DeferredEarlyF103LoadedProgram> Programs { get; } = new();
        public ConcurrentDictionary<int, LoadedDeferredTexturePlan> TexturePlans { get; } = new();
    }

    private static readonly ConditionalWeakTable<LoadedDeferredTexturePlan, LoadedSource> LoadedSources = new();
    private static LoadedSource ReadLoadedSource(LoadedDeferredTexturePlan source)
        => LoadedSources.GetValue(source, original =>
        {
            var loaded = new LoadedSource(original);
            // Selected packets expose the canonical imported plan. Subsequent
            // requests from the owning factory use that same directory.
            LoadedSources.Add(loaded.Source, loaded);
            return loaded;
        });

    internal static DeferredEarlyF103LoadedProgram ReadLoadedProgram(
        LoadedDeferredTexturePlan source, G2ShaderCombination combination)
    {
        var loaded = ReadLoadedSource(source);
        return loaded.Programs.GetOrAdd(combination.Index, _ =>
        {
            var shader = loaded.Source.Shader;
            int pixel = combination.Stages.Single(s => s.StageCode == 0 && s.Entry is not null).Entry!.ProgramIndex;
            int vertex = combination.Stages.Single(s => s.StageCode == 1 && s.Entry is not null).Entry!.ProgramIndex;
            string vertexHash = Hash(shader.Container.GetProgramBytes(shader.Source.Bytes, vertex));
            if (vertexHash != FullscreenVertexSha256)
                throw new InvalidDataException("Early F103 fullscreen vertex program differs from its pinned source.");
            return new(pixel, vertex, Hash(shader.Container.GetProgramBytes(shader.Source.Bytes, pixel)), vertexHash,
                shader.ReadResourceDefinitions(pixel), shader.ReadG1ResourceBindings(combination.Index));
        });
    }
}
