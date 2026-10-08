using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>The actual node+1C0 cached transform pointer. An unread pointer is
/// distinct from the constructor's proven null. It is not a managed scene tree.</summary>
public readonly record struct OriginalUnitWorldParentReference(bool IsKnown, OriginalUnitWorldParentRecord? Value)
{
    public static OriginalUnitWorldParentReference Known(OriginalUnitWorldParentRecord? value) => new(true, value);
    public static OriginalUnitWorldParentReference Unknown => new(false, null);
    internal OriginalUnitWorldParentRecord? Read() => IsKnown ? Value :
        throw new InvalidDataException("Current WorldPQ node+1C0 pointer is unknown.");
}

/// <summary>The native 64-byte record constructed by D82400. +C is an external
/// transform selector consumed by DC7917, not a pointer to another node. Fields
/// may be unknown; only the branch's actually consumed fields are required.</summary>
public sealed record OriginalUnitWorldParentRecord(int? Selector08, int? TransformSelector0C,
    Vector4? Scale10, Quaternion? Quaternion20, Vector4? Position30, string CallerSource)
{
    internal int Selector() => Selector08 ?? throw new InvalidDataException("Current WorldPQ parent selector+8 is unknown.");
    internal int TransformSelector() => TransformSelector0C ?? throw new InvalidDataException("Current WorldPQ parent transform selector+C is unknown.");
    internal Vector3 Scale() => Scale10 is { } s ? new(s.X, s.Y, s.Z) :
        throw new InvalidDataException("Current WorldPQ parent scale+10 is unknown.");
    internal Quaternion Quaternion() => Quaternion20 ?? throw new InvalidDataException("Current WorldPQ parent quaternion+20 is unknown.");
    internal Vector3 Position() => Position30 is { } p ? new(p.X, p.Y, p.Z) :
        throw new InvalidDataException("Current WorldPQ parent position+30 is unknown.");
}

/// <summary>Compact source context SQT: ScaleXYZ at+0, XYZW at+C, PositionXYZ
/// at+1C. C10C80 initializes an additional retained selector at+28; the parent
/// packer does not read that selector. No missing component becomes a default.</summary>
public sealed record OriginalUnitWorldParentSqt40(Vector3 Scale0, Quaternion Quaternion0C,
    Vector3 Position1C, string CallerSource);

/// <summary>The explicit 48-byte block passed to virtual node+8. Native
/// D82400/DA4030 copy all four words of Scale, Quaternion and Position.</summary>
public sealed record OriginalUnitWorldParentPackedSqt(Vector4 Scale, Quaternion Quaternion,
    Vector4 Position, string CallerSource);

public sealed record OriginalUnitWorldParentState(OriginalUnitWorldTransformState LocalFields,
    OriginalUnitWorldParentReference Parent1C0)
{
    public Vector3 GetScale90()
    {
        var parent = SpecialParent();
        return parent is null ? LocalFields.Scale34 : OriginalUnitWorldParentMath.GetScale(LocalFields.Scale34, parent.Scale());
    }
    public Quaternion GetQuaternion88()
    {
        var parent = SpecialParent();
        return parent is null ? LocalFields.Quaternion24 : OriginalUnitWorldParentMath.GetQuaternion(LocalFields.Quaternion24, parent.Quaternion());
    }
    public Vector3 GetPosition80()
    {
        var parent = SpecialParent();
        return parent is null ? LocalFields.Position18 : OriginalUnitWorldParentMath.GetPosition(
            LocalFields.Position18, LocalFields.Quaternion24, LocalFields.Scale34,
            parent.Scale(), parent.Quaternion(), parent.Position());
    }
    /// <summary>DAEA00 gathers Scale, Q, P through three separate getters.
    /// Position uses the raw local/parent Q, independent of the normalized Q getter.</summary>
    public OriginalUnitWorldTransformState WorldFields()
    {
        Vector3 scale = GetScale90(); Quaternion q = GetQuaternion88(); Vector3 p = GetPosition80();
        string parent = Parent1C0.Value is { } record ? record.CallerSource : "known node+1C0 null";
        return LocalFields with { Position18 = p, Quaternion24 = q, Scale34 = scale,
            Basis = LocalFields.Basis + "; native WorldPQ +90/+88/+80 getters; parent " + parent };
    }
    public OriginalMapStaticModelTransform ShaderTransform() => WorldFields().ShaderTransform();
    public OriginalMapStaticModelTransform ShaderTransformWithPrimitiveLocalRows(ReadOnlySpan<float> localRows,
        string localUri, string localSha256) => WorldFields().ShaderTransformWithPrimitiveLocalRows(localRows, localUri, localSha256);

    /// <summary>C13890's actual WorldPQ root-selector output. Getter order is
    /// P, Q, scale; this node's +138 feature method is literal true.</summary>
    public OriginalUnitWorldParentSqt40 RootSqtC13890()
    {
        Vector3 p = GetPosition80(); Quaternion q = GetQuaternion88(); Vector3 s = GetScale90();
        return new(s, q, p, LocalFields.EvidenceUri + "; C13890 explicit current WorldPQ root SQT");
    }
    internal OriginalUnitWorldParentRecord? SpecialParent()
    {
        ArgumentNullException.ThrowIfNull(LocalFields);
        var record = Parent1C0.Read();
        if (record is null) return null;
        if (string.IsNullOrWhiteSpace(record.CallerSource)) throw new InvalidDataException("Current parent record requires its caller source.");
        return record.Selector() == -1 ? record : null;
    }
}

public sealed record OriginalUnitWorldParentBindResult(bool Accepted, OriginalUnitWorldParentState State,
    bool RecordCreated);
/// <summary>The virtual +418 invalidation argument belongs to the source
/// publication; this projection does not run the remaining renderer cache update.</summary>
public sealed record OriginalUnitWorldParentSetterResult(OriginalUnitWorldParentState State, int InvalidateMask418);

public static class OriginalUnitWorldParent
{
    public const string NativeContractId = "ce-20261002-worldpq-cached-parent-record";

    /// <summary>Use only at the actual D9F540 constructor checkpoint. This
    /// method does not infer null for a later current node.</summary>
    public static OriginalUnitWorldParentState FromConstructor(OriginalUnitWorldTransformState fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        return new(fields, OriginalUnitWorldParentReference.Known(null));
    }

    public static OriginalUnitWorldParentRecord ReadCurrentRecord(ReadOnlySpan<byte> completeRecord64,
        string callerSource, string language, ulong explicitImageBase)
    {
        Label(callerSource);
        if (completeRecord64.Length != 64 || explicitImageBase == 0)
            throw new InvalidDataException("Current native parent record requires all 64 bytes and its explicit image base.");
        uint rva = language switch { "SC" => 0x16CB0D8, "TC" => 0x16CA428,
            _ => throw new InvalidDataException("Unsupported native parent record language profile.") };
        if (explicitImageBase > ulong.MaxValue - rva)
            throw new InvalidDataException("Current native parent image base overflows its vtable address.");
        if (BinaryPrimitives.ReadUInt64LittleEndian(completeRecord64) != explicitImageBase + rva)
            throw new InvalidDataException("Current parent record vtable differs from the proven D82400 type.");
        Vector4 scale = Vector(completeRecord64[0x10..]), q = Vector(completeRecord64[0x20..]), p = Vector(completeRecord64[0x30..]);
        return new(BinaryPrimitives.ReadInt32LittleEndian(completeRecord64[8..]),
            BinaryPrimitives.ReadInt32LittleEndian(completeRecord64[12..]), scale, new(q.X, q.Y, q.Z, q.W), p,
            callerSource + "; explicit64 SHA256=" + Convert.ToHexString(SHA256.HashData(completeRecord64)));
    }

    /// <summary>DA4030 rejects every requested selector other than -1 before
    /// reading node+1C0. Allocation outcome is consumed only for an absent record;
    /// failed allocation and rejected selectors preserve the entire prior state.</summary>
    public static OriginalUnitWorldParentBindResult BindDa4030(OriginalUnitWorldParentState current,
        int requestedSelector08, int transformSelector0C, OriginalUnitWorldParentPackedSqt? callerSqt,
        bool? allocationSucceeded)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (requestedSelector08 != -1) return new(false, current, false);
        var previous = current.Parent1C0.Read();
        bool created = previous is null;
        if (created)
        {
            bool allocated = allocationSucceeded ?? throw new InvalidDataException("Current WorldPQ parent allocation result is unknown.");
            if (!allocated) return new(false, current, false);
        }
        if (callerSqt is null) throw new InvalidDataException("Accepted WorldPQ parent binding requires the caller's complete SQT48 block.");
        Label(callerSqt.CallerSource);
        var record = new OriginalUnitWorldParentRecord(-1, transformSelector0C, callerSqt.Scale,
            callerSqt.Quaternion, callerSqt.Position, callerSqt.CallerSource + "; D82400/DA4030 copied SQT48");
        return new(true, current with { Parent1C0 = OriginalUnitWorldParentReference.Known(record) }, created);
    }

    public static OriginalUnitWorldParentState ClearDad050(OriginalUnitWorldParentState current)
    {
        ArgumentNullException.ThrowIfNull(current); _ = current.Parent1C0.Read();
        return current with { Parent1C0 = OriginalUnitWorldParentReference.Known(null) };
    }

    /// <summary>DC7917 only refreshes an existing record. Failed source +F0
    /// transform resolution preserves it. Successful compact SQT40 packing forces
    /// ScaleW=+0 and PositionW=1 before the native -1 binding call.</summary>
    public static OriginalUnitWorldParentState RefreshDc7917(OriginalUnitWorldParentState current,
        bool? sourceTransformResolved, OriginalUnitWorldParentSqt40? sourceOutput)
    {
        ArgumentNullException.ThrowIfNull(current);
        var parent = current.Parent1C0.Read();
        if (parent is null) return current;
        int selector = parent.TransformSelector();
        bool resolved = sourceTransformResolved ?? throw new InvalidDataException("Current parent +F0 transform result is unknown.");
        if (!resolved) return current;
        if (sourceOutput is null) throw new InvalidDataException("Successful parent refresh requires the actual compact SQT output.");
        Label(sourceOutput.CallerSource);
        var packed = new OriginalUnitWorldParentPackedSqt(new(sourceOutput.Scale0, 0f), sourceOutput.Quaternion0C,
            new(sourceOutput.Position1C, 1f), sourceOutput.CallerSource + "; DC7917 source SQT40 pack");
        return BindDa4030(current, -1, selector, packed, null).State;
    }

    /// <summary>Finite Model::WorldPQ selector -1 chain: DAF080→C130A0→C13890
    /// obtains the source parent's current world SQT, then DC7917 packs it into
    /// the child record. Caller establishes the actual source parent relationship
    /// and ordered refresh; no RDOK definition reference becomes a parent edge.</summary>
    public static OriginalUnitWorldParentState RefreshFromRootModel(OriginalUnitWorldParentState child,
        OriginalUnitWorldParentState explicitSourceParent)
    {
        ArgumentNullException.ThrowIfNull(child); ArgumentNullException.ThrowIfNull(explicitSourceParent);
        var record = child.Parent1C0.Read();
        if (record is null) return child;
        if (record.TransformSelector() != -1)
            throw new InvalidDataException("Non-root parent transform selector requires its actual current joint/context SQT result.");
        return RefreshDc7917(child, true, explicitSourceParent.RootSqtC13890());
    }

    public static OriginalUnitWorldParentSetterResult SetPosition68(OriginalUnitWorldParentState current, Vector3 worldPosition)
    {
        ArgumentNullException.ThrowIfNull(current);
        var parent = current.SpecialParent();
        Vector3 value = parent is null ? worldPosition : OriginalUnitWorldParentMath.SetPosition(worldPosition,
            current.GetQuaternion88(), current.GetScale90(), parent.Quaternion(), parent.Position());
        return new(current with { LocalFields = current.LocalFields.PublishPosition(value) }, 1);
    }
    public static OriginalUnitWorldParentSetterResult SetQuaternion70(OriginalUnitWorldParentState current, Quaternion worldQuaternion)
    {
        ArgumentNullException.ThrowIfNull(current);
        var parent = current.SpecialParent();
        Quaternion value = parent is null ? worldQuaternion : OriginalUnitWorldParentMath.SetQuaternion(worldQuaternion, parent.Quaternion());
        return new(current with { LocalFields = current.LocalFields.PublishQuaternion(value) }, 2);
    }
    public static OriginalUnitWorldParentSetterResult SetScale78(OriginalUnitWorldParentState current, Vector3 worldScale)
    {
        ArgumentNullException.ThrowIfNull(current);
        var parent = current.SpecialParent();
        Vector3 value = parent is null ? worldScale : OriginalUnitWorldParentMath.SetScale(worldScale, parent.Scale());
        return new(current with { LocalFields = current.LocalFields.PublishScale(value) }, 4);
    }
    private static void Label(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException("Native parent input requires an explicit caller source.");
    }
    private static Vector4 Vector(ReadOnlySpan<byte> bytes) => new(
        BinaryPrimitives.ReadSingleLittleEndian(bytes), BinaryPrimitives.ReadSingleLittleEndian(bytes[4..]),
        BinaryPrimitives.ReadSingleLittleEndian(bytes[8..]), BinaryPrimitives.ReadSingleLittleEndian(bytes[12..]));
}
