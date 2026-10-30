using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>A cutin effect's draw-list and dedicated perspective-camera selection.
/// The parent layout owns lifetime; it is not the ordinary map camera.</summary>
public sealed record UniqueTacticEffectLayer(int Selector, int ViewSlot,
    uint DrawListObjectId, uint CameraObjectId, LoadedMapCameraConstructionSource Camera);

/// <summary>Declarations retained by one normal resource generation. Reload after
/// resource overrides change; creating a camera does not allocate or accept an effect.</summary>
public sealed class LoadedUniqueTacticEffects
{
    internal LoadedUniqueTacticEffects(LoadedObjects view, UniqueTacticEffectLayer cutin,
        UniqueTacticEffectLayer foreground)
    {
        View = view; Cutin = cutin; Foreground = foreground;
    }

    public LoadedObjects View { get; }
    public UniqueTacticEffectLayer Cutin { get; }
    public UniqueTacticEffectLayer Foreground { get; }

    public UniqueTacticEffectLayer Layer(int selector) => selector switch
    {
        2 => Cutin,
        3 => Foreground,
        _ => throw new ArgumentOutOfRangeException(nameof(selector))
    };

    /// <summary>The normal UI camera after its actual initialization setters.
    /// Clip profile and reverse depth belong to the renderer's real context.
    /// The source pose and aspect come from the dedicated declaration, not a Hex
    /// or a current map-camera frame. The source's unparented scale starts at one.</summary>
    public MapCameraGeometry CreateCamera(int selector, bool reverseDepth, uint clipProfile,
        NativeCameraTangentDispatch tangentDispatch)
    {
        var declaration = Layer(selector).Camera.Declaration.Perspective;
        var pose = declaration.Pose with { Position80 = new Vector3(0f, 100f, 1000f) };
        var eye = new Vector4(pose.Position80, 1f);
        var vectors = MapCameraConstructionMath.PoseFromQuaternionC45956(
            eye, pose.Quaternion88, declaration.LookDistance, pose.Scale90.Z);
        var lens = declaration.Lens with
        {
            Near = 8f, Far = 10000f,
            VerticalFovRadians = BitConverter.UInt32BitsToSingle(0x3E860A92),
            Flags = reverseDepth ? 2u : 0u,
            OrthographicScale = 0f
        };
        var matrices = MapCameraConstructionMath.CompleteAB6C60(
            eye, vectors.Target20, vectors.Up30, lens, clipProfile, tangentDispatch);
        return new(pose, eye, vectors.Target20, vectors.Up30, lens, matrices);
    }
}

/// <summary>Normal unique-tactic UI-effect camera declarations and finite placement
/// rules. Original executable constants were extracted offline; runtime uses resources only.</summary>
public static class UniqueTacticEffectPublication
{
    public const float InitialCameraDistance = 5000f;
    public const float SecondCameraFocusOffset = 2500f;
    /// <summary>Zero-origin creation selects identity in the effect placement helper.
    /// Parent layout identity is retained separately for ownership and retirement.</summary>
    public static Matrix4x4 InitialEffectTransform => Matrix4x4.Identity;

    public static LoadedUniqueTacticEffects Load(GameResourceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var view = repository.LoadObjects("system", DefaultView0Source.ViewResourceId);
        var cameras = repository.LoadObjects("system", MapCameraConstructionSource.SourceFileId);
        var selected = view.Document.Objects.SingleOrDefault(o => o.ObjectId == DefaultView0Source.ViewObjectId);
        if (selected is null || selected.Kind != DokObjectKind.Inline || selected.ClassId != 0x97AF26FD)
            throw new InvalidDataException("Unique tactic effects require the normal declared View.");
        var fields = selected.Properties.Where(p => p.NameHash == 0xB43036BD).ToArray();
        if (fields.Length != 1 || fields[0].TypeCode != 5 || fields[0].ElementSize != 4 ||
            fields[0].ElementCount <= 4 || fields[0].RawValue.Length != fields[0].ElementCount * 4)
            throw new InvalidDataException("Unique tactic effects require the typed View camera array.");
        var slots = fields[0].RawValue;
        uint cutin = BinaryPrimitives.ReadUInt32LittleEndian(slots.AsSpan(3 * 4, 4));
        uint foreground = BinaryPrimitives.ReadUInt32LittleEndian(slots.AsSpan(4 * 4, 4));
        // These are independent Perspective declarations. A resource override may
        // change their numeric values, but cannot silently route UI effects to Main.
        if (cutin != 0x1F3FB203 || foreground != 0x937EEA62)
            throw new InvalidDataException("Unique tactic effects have an unsupported dedicated camera binding.");
        return new(view,
            new(2, 3, 0x3E3E42CD, cutin, MapCameraConstructionSource.ReadPerspective(cameras, cutin)),
            new(3, 4, 0x012AAC2C, foreground, MapCameraConstructionSource.ReadPerspective(cameras, foreground)));
    }

    public static int ClassEffectKind(int tacticClass) => tacticClass is >= 0 and <= 4
        ? 98 + tacticClass : throw new ArgumentOutOfRangeException(nameof(tacticClass));

    public static int LinkedEffectKind(int portraitCount) => portraitCount is >= 1 and <= 4
        ? 102 + portraitCount : throw new ArgumentOutOfRangeException(nameof(portraitCount));

    public static int ForegroundEffectKind(int tacticClass) => tacticClass is >= 0 and <= 4
        ? 107 + tacticClass : throw new ArgumentOutOfRangeException(nameof(tacticClass));
}
