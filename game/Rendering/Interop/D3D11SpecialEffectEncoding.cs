using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using San14.Data;

/// <summary>Current D33 admission inputs, independent of particle population.
/// The selected light is the same ordered kind-one root used by View1B18.</summary>
internal static class D3D11SpecialEffectEncoding
{
    internal const int ByteLength = 20;
    private sealed record Inputs(uint CommonFlags, uint SelectorMask, uint? SelectedTag);
    private static readonly ConditionalWeakTable<D3D11CurrentSeasonFrame, Inputs> Frames = new();

    internal sealed class Source(uint commonFlags, G1EffectViewLightSource selector)
    {
        internal void Bind(D3D11CurrentSeasonFrame frame)
        {
            ArgumentNullException.ThrowIfNull(frame);
            var selected = frame.Lights.FirstOrDefault(light => selector.Selects(light.GroupMask));
            Frames.Remove(frame);
            Frames.Add(frame, new(commonFlags, selector.GroupMask, selected?.GroupMask));
        }
    }

    internal static Source Create(GameResourceRepository repository, CommonRenderSettings common)
    {
        var view = repository.LoadObjects("system", DefaultView0Source.ViewResourceId)
            .Document.Objects.Single(value => value.ObjectId == DefaultView0Source.ViewObjectId);
        var declaration = repository.LoadObjects("KIDSSystemResource", CommonRenderResources.SourceFileId)
            .Document.Objects.Single(value => value.ObjectId == CommonRenderResources.SourceObjectId);
        return new(common.Flags8, G1EffectViewLightSource.Import(view, declaration));
    }

    internal static byte[] Write(D3D11CurrentSeasonFrame frame)
    {
        if (!Frames.TryGetValue(frame, out var input))
            throw new InvalidOperationException("Special Effect current View admission owner is unavailable.");
        byte[] result = new byte[ByteLength];
        BinaryPrimitives.WriteUInt32LittleEndian(result, 0x31455053); // SPE1
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), input.CommonFlags);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8), input.SelectorMask);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12), input.SelectedTag.HasValue ? 1u : 0u);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(16), input.SelectedTag ?? 0);
        return result;
    }
}
