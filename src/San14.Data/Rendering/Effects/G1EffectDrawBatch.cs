using System.Numerics;

namespace San14.Data;

/// <summary>Prepared effect camera. Projection is the symmetric projection
/// normalized by far distance used by the effect culler, not the ordinary map
/// depth projection. CameraToWorld supplies the current writer basis.</summary>
public sealed record G1EffectDrawCamera(Matrix4x4 ClipMatrix, Matrix4x4 CameraToWorld,
    Matrix4x4 Projection, Vector3 Eye, float NearPlane, float FarPlane)
{
    public float SortBiasScale => (-Projection.M33 - Projection.M34) * 0.5f;
}

/// <summary>Current generic and its actual live slots in native block/head
/// order. The host recursively supplies type-three children in declaration
/// order. RetireCulled must perform the actual particle cleanup, when enabled.</summary>
public sealed record G1EffectParticleRenderSource(LoadedMapEffectResources Resources,
    G1EffectParticleDeclaration Declaration, ReadOnlyMemory<byte> CurrentGenericBytes,
    IEnumerable<G1EffectParticleSlot> Particles, float ModuleOpacity,
    Action<G1EffectParticleSlot>? RetireCulled = null);

public sealed record G1EffectDrawMissing(int GenericFileOffset, uint GenericType,
    string Reason, int ParticleCount);

/// <summary>Source-written packet fields, with managed resource identities in
/// place of native pointers. Stream offsets are assigned after common queue
/// sorting. Configuration six belongs to the reverse physical tail.</summary>
public sealed record G1EffectDrawPacket(LoadedMapEffectResources Resources,
    int GenericFileOffset, uint GenericType, int Configuration, int RenderingType,
    uint HeaderFlags, uint Flags, int TextureSlot, byte AlphaReference,
    int DynamicByteOffset, int DynamicStride, int DynamicCount, int? MeshSubdrawIndex,
    int SortBucket, long SubmissionOrdinal, ReadOnlyMemory<byte> MaterialBytes)
{
    /// <summary>Live CPU submission identity; never encoded in the GPU ABI.</summary>
    public G1EffectParticleRenderSource? Source { get; init; }
    public bool IsTail => Configuration == 6;
    public int DynamicStart32 => DynamicByteOffset >> 5;
    public bool UsesFullMeshLayout => (Flags & 0x3C500) != 0;
}

public sealed record G1EffectDrawBatch(G1EffectDrawCamera Camera, ReadOnlyMemory<byte> DynamicStream,
    IReadOnlyList<G1EffectDrawPacket> Packets, IReadOnlyList<G1EffectDrawMissing> Missing,
    int CulledCount, int CapacityRejectedCount);
