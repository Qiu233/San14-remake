using System.Numerics;

namespace San14.Data;

public interface ISceneryModelQueries
{
    byte? ReadSceneryKind46(SceneryReference scenery);
    ushort? ReadSceneryHexIndex(SceneryReference scenery);
    ushort? ReadSceneryAngle4A(SceneryReference scenery);
}

public sealed record ModelHexPosition(float X, float Z);

/// <summary>Precise remaining descriptor/geometry operations. Release requests
/// are queued by1C71F0/1C90F0; this interface does not imply immediate GPU free
/// or a resolved asset URI for the numeric model resource descriptor.</summary>
public interface ISceneryModelGraphics
{
    void ConstructDescriptorList(SceneryUnitModel model);
    void RequestDescriptorResourceReleases(SceneryUnitModel model);
    void ClearDescriptorList(SceneryUnitModel model);
    ModelHexPosition? NativeHexPosition187A40(ushort hex);
    Vector4? NativeQuaternion1C58E0(Vector3 euler);
}

/// <summary>CUnitScenery constructor and slots1/2/7/8/3 business. The descriptor
/// selects a resource number, not a file hash. Actual list/queue operations,
/// coordinate parameters, quaternion conversion and later asset creation are
/// explicit graphics dependencies.</summary>
public sealed class SceneryUnitModel
{
    private static readonly int[] ResourceKeys = [96, 92, 91, 93, 94, 89, 90, 95];
    private static readonly Vector4 DefaultTransform = new(0, 0, 0, 1);
    private readonly ISceneryModelQueries _queries;
    private readonly ISceneryModelGraphics _graphics;
    private readonly UnitModelStorage _storage;
    public UnitModelReference Reference { get; }
    public SceneryReference? Association { get; private set; }
    public Vector4? Position10 { get; private set; }
    public Vector4? Quaternion20 { get; private set; }
    public uint? RawField30 { get; private set; }
    public uint? RawField34 { get; private set; }
    public int? DescriptorCount68 { get; private set; }
    public int? DescriptorKey6C { get; private set; }
    public uint? DescriptorFlags70 { get; private set; }
    public Action<SceneryUnitModel>? Slot1Override { get; set; }
    public Action<SceneryUnitModel>? DescriptorSlot1Override { get; set; }

    public SceneryUnitModel(UnitModelReference reference, ISceneryModelQueries queries,
        ISceneryModelGraphics graphics, UnitModelStorage storage)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(queries);
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(storage);
        if (storage.ReadModelKind(reference) != UnitModelKind.Scenery)
            throw new InvalidDataException("Scenery model requires a Scenery pool node.");
        Reference = reference;
        _queries = queries;
        _graphics = graphics;
        _storage = storage;
    }

    public void WriteAssociation(SceneryReference? value)
    {
        if (value?.PhysicalSlot is int physical && physical < 0) throw new InvalidDataException("Negative scenery association.");
        Association = value;
        _storage.WriteModelData(Reference, value is null ? null : new(RegistryKind.Scenery, value.PhysicalSlot));
    }
    public void WriteRawField30(uint? value) => RawField30 = value;
    public void WriteRawField34(uint? value) => RawField34 = value;
    /// <summary>Reached Scenery slot6 ground writer. This updates the actual
    /// Unit position later read by the map label, independently of device-ready
    /// model records. NaN follows the source comparison's445 fallback.</summary>
    public void PublishGroundHeight(float height)
    {
        var p = Position10 ?? throw new InvalidDataException("Current scenery Unit position is unknown.");
        Position10 = new(p.X, height >= 445f ? height : 445f, p.Z, p.W);
    }
    public void WriteDescriptorCount68(int? value) => DescriptorCount68 = value;
    public void WriteDescriptorKey6C(int? value) => DescriptorKey6C = value;
    public void WriteDescriptorFlags70(uint? value) => DescriptorFlags70 = value;

    /// <summary>1BEE00. The actual list constructor/initial clear precedes
    /// descriptor field writes and the second association/transform reset.</summary>
    public void ApplyConstructor()
    {
        ResetTransforms();
        _graphics.ConstructDescriptorList(this);
        DescriptorCount68 = 0;
        DescriptorKey6C = -1;
        DescriptorFlags70 = 0;
        WriteAssociation(new(null));
        ResetTransforms();
        ClearDescriptorResources();
    }
    public int Initialize()
    {
        if (Slot1Override is null) Reset();
        else Slot1Override(this);
        return 1;
    }
    public void Reset()
    {
        WriteAssociation(new(null));
        ResetTransforms();
        ClearDescriptorResources();
    }
    public void Retire()
    {
        var current = Association ?? throw new InvalidDataException("Current scenery model association is unknown.");
        if (!current.PhysicalSlot.HasValue) return;
        WriteAssociation(new(null));
        ClearDescriptorResources();
    }
    public void ReleaseResources() => ClearDescriptorResources();

    public void Bind(SceneryReference scenery)
    {
        ArgumentNullException.ThrowIfNull(scenery);
        WriteAssociation(scenery);
        if (!scenery.PhysicalSlot.HasValue) return;
        byte kind = _queries.ReadSceneryKind46(scenery)
            ?? throw new InvalidDataException("Current scenery kind46 is unknown.");
        if (kind is >= 1 and <= 8)
        {
            DescriptorKey6C = ResourceKeys[kind - 1];
            DescriptorFlags70 = 0; //The source writes qword6C before DWORD68.
            DescriptorCount68 = 1;
        }
        ushort hex = _queries.ReadSceneryHexIndex(scenery)
            ?? throw new InvalidDataException("Current scenery hex44 is unknown.");
        var position = _graphics.NativeHexPosition187A40(hex)
            ?? throw new InvalidDataException("Native scenery position is unknown.");
        Position10 = new(position.X, 0, position.Z, 1);
        RawField30 = 1; //DWORD write preserves the independent high DWORD34.
        var current = Association ?? throw new InvalidDataException("Current scenery model association is unknown after positioning.");
        if (!current.PhysicalSlot.HasValue) throw new InvalidDataException("Current scenery model association is null before its angle read.");
        ushort rawAngle = _queries.ReadSceneryAngle4A(current)
            ?? throw new InvalidDataException("Current scenery angle4A is unknown.");
        float angle = (float)rawAngle * BitConverter.Int32BitsToSingle(0x40490FDB);
        angle /= 180f;
        Vector4 quaternion = _graphics.NativeQuaternion1C58E0(new(0, angle, 0))
            ?? throw new InvalidDataException("Native scenery quaternion is unknown.");
        Quaternion20 = quaternion;
        RawField30 = 1;
    }
    public void ResetDescriptor()
    {
        _graphics.ClearDescriptorList(this);
        DescriptorKey6C = -1;
        DescriptorCount68 = 0;
        DescriptorFlags70 = 0;
    }
    private void ClearDescriptorResources()
    {
        _graphics.RequestDescriptorResourceReleases(this);
        if (DescriptorSlot1Override is null) ResetDescriptor();
        else DescriptorSlot1Override(this);
    }
    private void ResetTransforms()
    { Position10 = Quaternion20 = DefaultTransform; RawField30 = 1; RawField34 = 0; }
}
