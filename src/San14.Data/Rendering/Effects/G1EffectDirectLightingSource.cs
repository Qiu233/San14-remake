using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>Current inputs of the linked point-light producer. Particle state
/// remains in the main allocation's GPU buffers.</summary>
public sealed record G1EffectDirectLightingSource(int LinkedCapacity, int MainCapacity,
    int LinkedGenericAddress, int MainGenericAddress, uint ExtraAttributeFlags,
    uint AttenuationType, float OcclusionRate, Vector2 ScatterAttributes,
    float AutoHdrScaleExposure, string ProjectionSource)
{
    public static G1EffectGenericRuntime FindMain(G1EffectGenericRuntime linked,
        IEnumerable<G1EffectGenericRuntime> generics)
    {
        ArgumentNullException.ThrowIfNull(linked); ArgumentNullException.ThrowIfNull(generics);
        if (linked.Record.Type != 10 || !linked.Declaration.UsesDirectBirth)
            throw new ArgumentException("A linked direct point-light generic is required.", nameof(linked));
        int target = checked(linked.Record.FileOffset + 0x200 +
            BinaryPrimitives.ReadInt32LittleEndian(linked.CurrentGenericBytes.AsSpan(0x200)));
        return generics.Single(generic => generic.Record.FileOffset == target &&
            ReferenceEquals(generic.DirectModuleInstanceKey, linked.DirectModuleInstanceKey));
    }

    /// <param name="autoHdrScaleExposure">The current system's exposure setter value.
    /// The original system constructor initializes this independent scalar to one.</param>
    public static G1EffectDirectLightingSource Read(G1EffectGenericRuntime main,
        G1EffectGenericRuntime linked, G1EffectDirectComputeStorage storage,
        float autoHdrScaleExposure)
    {
        ArgumentNullException.ThrowIfNull(main); ArgumentNullException.ThrowIfNull(linked);
        ArgumentNullException.ThrowIfNull(storage);
        var package = storage.Package;
        if (package.LinkedDeclaration?.Record.FileOffset != linked.Record.FileOffset ||
            package.Declaration.Record.FileOffset != main.Record.FileOffset ||
            !ReferenceEquals(main.DirectModuleInstanceKey, linked.DirectModuleInstanceKey))
            throw new ArgumentException("Point-light state belongs to another direct allocation.");
        if (!float.IsFinite(autoHdrScaleExposure)) throw new ArgumentOutOfRangeException(nameof(autoHdrScaleExposure));
        ReadOnlySpan<byte> bytes = linked.CurrentGenericBytes;
        float F(int at) => BinaryPrimitives.ReadSingleLittleEndian(linked.CurrentGenericBytes.AsSpan(at));
        if (linked.Declaration.Stride < 0x4C || bytes.Length < 0x228)
            throw new InvalidDataException("Linked point-light declaration is incomplete.");
        int origin = main.DirectModuleDeclaration.FileOffset;
        // The ordinary View point producer supplies flags seven and attenuation
        // selector 142. These are its source packing choices, not draw passes.
        return new(package.LinkedCapacity, package.Capacity,
            checked(linked.Record.FileOffset - origin), checked(main.Record.FileOffset - origin),
            7, 142, Math.Clamp(F(0x21C), 0, 1), new(F(0x220), F(0x224)),
            autoHdrScaleExposure, G1EffectDirectComputePackage.ReadOwnedSource("G1EffectDirect.Lighting.hlsl"));
    }

    public byte[] Encode()
    {
        byte[] source = System.Text.Encoding.UTF8.GetBytes(ProjectionSource);
        byte[] bytes = new byte[checked(64 + source.Length)];
        void U(int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), value);
        void F(int at, float value) => BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(at), value);
        U(0, 0x31504C43); U(4, 1); U(8, checked((uint)LinkedCapacity)); U(12, checked((uint)MainCapacity));
        U(16, checked((uint)LinkedGenericAddress)); U(20, checked((uint)MainGenericAddress));
        U(24, ExtraAttributeFlags); U(28, AttenuationType); F(32, OcclusionRate);
        F(36, ScatterAttributes.X); F(40, ScatterAttributes.Y); F(44, AutoHdrScaleExposure);
        U(48, 64); U(52, checked((uint)source.Length)); source.CopyTo(bytes, 64);
        return bytes;
    }
}

public interface IG1EffectDirectLightingAllocation : IG1EffectDirectComputeAllocation
{
    void SetLighting(G1EffectDirectLightingSource? source, long submissionOrdinal);
}
