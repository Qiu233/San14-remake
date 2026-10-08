using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>One explicitly owned source node. Identities are caller keys, not
/// claims about native pointer addresses. Zero NextNodeIdentity means list end.
/// The fields preserve the native resource +14/+10 readiness and kind gates.</summary>
public sealed record OriginalArmyFlagPoolNode(ulong NodeIdentity, ulong NextNodeIdentity,
    int ResourceReadyField14, int ResourceKind10, bool ResolvedObjectPointerNonNull,
    bool ModelPointerNonNull)
{
    // Native code dereferences these pointers without null guards. They must be
    // present when reached; kind-1's wrapper is reached only after its kind gate.
    public bool ResourcePointerNonNull { get; init; } = true;
    public bool Kind1WrapperPointerNonNull { get; init; } = true;
    // Wrapper+20 is guarded; its +30 getter supplies the resolved object.
    public bool Kind1ReferencePointerNonNull { get; init; } = true;
}

/// <summary>Explicit source list head and node links. Nodes may be supplied in
/// any storage order; the writer follows HeadNodeIdentity and NextNodeIdentity.
/// CallerSource describes ownership and readiness evidence for these keys.</summary>
public sealed record OriginalArmyFlagLinkedPool(int DescriptorOffset, ulong HeadNodeIdentity,
    IReadOnlyList<OriginalArmyFlagPoolNode> Nodes, string CallerSource);

/// <summary>Explicit current int32 little-endian record-index stream. A longer
/// source stream requires its own caller evidence; it is never inferred from a
/// resource request or by repeating the three pinned static entries.</summary>
public sealed record OriginalArmyFlagRecordIndexSnapshot(byte[] RawLittleEndianIndices,
    string CallerSource);

public sealed record OriginalArmyFlagPoolVisit(int DescriptorOffset, int DescriptorInstanceIndex,
    ulong NodeIdentity, int? RecordIndex, OriginalArmyInstancePoseWrite? Write,
    string? UnavailableReason)
{
    public bool ModelResolved => Write is not null;
}

public sealed record OriginalArmyFlagPoolPublicationResult(
    IReadOnlyList<OriginalArmyFlagPoolVisit> Visits,
    IReadOnlyList<OriginalArmyInstancePoseWrite> Writes,
    IReadOnlyList<OriginalArmyFlagPoolVisit> MissingNodes,
    string RecordSource, string BannerPoolSource, string FlagPoolSource,
    OriginalArmyFlagRecordIndexSnapshot FlagIndexStream,
    OriginalArmyInstancePoseCodeSource CodeSource)
{
    public bool Complete => MissingNodes.Count == 0;
    public bool OriginalCurrentInstanceStateVerified => false;
}

/// <summary>SC 1CD57E..1CD837 / TC 1CD58E..1CD847 list-driven WORLD flag
/// publication. The native loops have no numerical cap. Invalid links and
/// unsafe dereferences reject; native null readiness guards skip a node. Flag
/// table position advances for every visited node, while an unavailable node
/// never reads its table entry. Scale, position, then raw yaw are published.</summary>
public static class OriginalArmyFlagPoolPublication
{
    public static OriginalArmyFlagPoolPublicationResult Publish(
        OriginalArmyInstancePoseSnapshot snapshot, int explicitCurrentMode,
        OriginalArmyFlagLinkedPool bannerPool, OriginalArmyFlagLinkedPool flagPool,
        OriginalArmyFlagRecordIndexSnapshot? explicitFlagRecordIndices = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(bannerPool);
        ArgumentNullException.ThrowIfNull(flagPool);
        if (snapshot.CodeSource is null || snapshot.Records is null ||
            snapshot.Records.Count != OriginalArmyInstancePose.RecordCount ||
            snapshot.CodeSource != OriginalArmyInstancePoseCodeSources.ForLanguage(snapshot.CodeSource.Language) ||
            explicitCurrentMode is < 0 or > 3)
            throw Error("requires a supported nineteen-record source snapshot and explicit mode");
        Label(snapshot.CallerSource);
        OriginalArmyFlagRecordIndexSnapshot indices = IndexSnapshot(explicitFlagRecordIndices);
        var visits = new List<OriginalArmyFlagPoolVisit>();
        var writes = new List<OriginalArmyInstancePoseWrite>();
        var missing = new List<OriginalArmyFlagPoolVisit>();
        Walk(bannerPool, 0x7F0);
        Walk(flagPool, 0x7C8);
        return new(visits.AsReadOnly(), writes.AsReadOnly(), missing.AsReadOnly(),
            snapshot.CallerSource, bannerPool.CallerSource, flagPool.CallerSource, indices, snapshot.CodeSource);

        void Walk(OriginalArmyFlagLinkedPool pool, int descriptor)
        {
            if (pool.DescriptorOffset != descriptor || pool.Nodes is null)
                throw Error("requires separate descriptor7F0 and descriptor7C8 linked pools");
            Label(pool.CallerSource);
            var nodes = new Dictionary<ulong, OriginalArmyFlagPoolNode>();
            foreach (OriginalArmyFlagPoolNode node in pool.Nodes)
                if (node is null || node.NodeIdentity == 0 || !nodes.TryAdd(node.NodeIdentity, node))
                    throw Error("has a null, zero or duplicate owned node identity");
            var visited = new HashSet<ulong>();
            ulong identity = pool.HeadNodeIdentity;
            int instance = 0;
            while (identity != 0)
            {
                if (!visited.Add(identity)) throw Error("linked pool contains a cycle");
                if (!nodes.TryGetValue(identity, out OriginalArmyFlagPoolNode? node))
                    throw Error("linked pool contains a dangling head or next identity");
                string? reason = Unavailable(node);
                if (reason is not null)
                {
                    var skipped = new OriginalArmyFlagPoolVisit(descriptor, instance, identity, null, null, reason);
                    visits.Add(skipped); missing.Add(skipped);
                }
                else
                {
                    int recordIndex = OriginalArmyInstancePoseCodeSources.BannerRecordIndex;
                    if (descriptor == 0x7C8)
                    {
                        if (instance >= indices.RawLittleEndianIndices.Length / 4)
                            throw Error("ready flag node exceeds the explicit record-index source stream");
                        recordIndex = BinaryPrimitives.ReadInt32LittleEndian(indices.RawLittleEndianIndices.AsSpan(instance * 4));
                    }
                    if ((uint)recordIndex >= OriginalArmyInstancePose.RecordCount)
                        throw Error("consumed flag record index is outside the nineteen source records");
                    OriginalArmyInstancePoseRecord record = snapshot.Records[recordIndex];
                    ValidateRecord(record, recordIndex);
                    float y = descriptor == 0x7F0 && explicitCurrentMode == 1
                        ? record.CurrentPosition.Y : Add(record.CurrentPosition.Y, OriginalArmyInstancePoseCodeSources.FlagHeightOffset);
                    if (!float.IsFinite(y)) throw Error("height addition produced a nonfinite argument");
                    var (sin, cos) = OriginalArmyAnimationSampler.SourceSinCos(Product(record.Heading, .5f));
                    var write = new OriginalArmyInstancePoseWrite(recordIndex, descriptor, instance,
                        new(record.CurrentPosition.X, y, record.CurrentPosition.Z, 1), new(0, sin, 0, cos),
                        descriptor == 0x7F0 ? OriginalArmyInstancePoseCodeSources.BannerScaleArgument
                            : OriginalArmyInstancePoseCodeSources.FlagScaleArgument);
                    visits.Add(new(descriptor, instance, identity, recordIndex, write, null));
                    writes.Add(write);
                }
                identity = node.NextNodeIdentity;
                instance++;
            }
        }
    }

    private static string? Unavailable(OriginalArmyFlagPoolNode node)
    {
        if (!node.ResourcePointerNonNull)
            throw Error("native node resource dereference would be unsafe");
        if (node.ResourceReadyField14 == 0) return "resource +14 is zero";
        if (node.ResourceKind10 is not (1 or 2)) return "resource +10 is not kind 1 or 2";
        if (node.ResourceKind10 == 1)
        {
            if (!node.Kind1WrapperPointerNonNull)
                throw Error("native kind-1 wrapper dereference would be unsafe");
            if (!node.Kind1ReferencePointerNonNull) return "kind-1 wrapper +20 reference is null";
        }
        if (!node.ResolvedObjectPointerNonNull) return "resolved resource object is null";
        return node.ModelPointerNonNull ? null : "resolved object +20 model is null";
    }

    private static OriginalArmyFlagRecordIndexSnapshot IndexSnapshot(OriginalArmyFlagRecordIndexSnapshot? supplied)
    {
        if (supplied is not null)
        {
            Label(supplied.CallerSource);
            if (supplied.RawLittleEndianIndices is null || supplied.RawLittleEndianIndices.Length % 4 != 0)
                throw Error("record-index source stream requires complete int32 little-endian words");
            return new(supplied.RawLittleEndianIndices.ToArray(), supplied.CallerSource);
        }
        var native = OriginalArmyInstancePoseCodeSources.FlagRecordIndices;
        byte[] raw = new byte[native.Count * 4];
        for (int i = 0; i < native.Count; i++) BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(i * 4), native[i]);
        var source = OriginalArmyInstancePoseCodeSources.FlagDataWindows.Single(w => w.Name == "FlagRecordIndices");
        if (raw.Length != source.Bytes || Convert.ToHexString(SHA256.HashData(raw)) != source.Sha256)
            throw Error("pinned default record-index source bytes differ from their paired data contract");
        return new(raw, "pinned SC/TC three-entry native flag table; flag-contract.json");
    }

    private static void ValidateRecord(OriginalArmyInstancePoseRecord record, int index)
    {
        if (record is null || record.RecordIndex != index || record.RawBytes is null ||
            record.RawBytes.Length != OriginalArmyInstancePose.RecordBytes ||
            !Same(record.CurrentPosition.X, Read(record.RawBytes, 0x10)) ||
            !Same(record.CurrentPosition.Y, Read(record.RawBytes, 0x14)) ||
            !Same(record.CurrentPosition.Z, Read(record.RawBytes, 0x18)) ||
            !Same(record.CurrentPosition.W, Read(record.RawBytes, 0x1C)) ||
            !Same(record.Heading, Read(record.RawBytes, 0x30)) ||
            !float.IsFinite(record.CurrentPosition.X) || !float.IsFinite(record.CurrentPosition.Y) ||
            !float.IsFinite(record.CurrentPosition.Z) || !float.IsFinite(record.Heading))
            throw Error("consumed record differs from its complete finite current source snapshot");
    }

    private static float Read(byte[] bytes, int offset) =>
        BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset)));
    private static bool Same(float a, float b) => BitConverter.SingleToUInt32Bits(a) == BitConverter.SingleToUInt32Bits(b);
    private static void Label(string label)
    { if (string.IsNullOrWhiteSpace(label)) throw Error("requires explicit caller ownership/source labels"); }
    private static InvalidDataException Error(string message) => new($"Original Army flag linked-pool publication {message}.");
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Product(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Add(float a, float b) => a + b;
}
