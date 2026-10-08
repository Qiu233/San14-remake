using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Immutable depth words retained by one owned backend generation
/// and bound to the same accepted Main Camera/View frame.</summary>
public sealed class OriginalMainMapViewportDepthPacket
{
    private readonly OriginalMainMapViewportDepthSource owner;
    private readonly OriginalDeferredF103SelectedSource acceptedSelection;
    internal OriginalMainMapViewportDepthPacket(OriginalMainMapViewportDepthSource owner,
        OriginalDeferredF103SelectedSource selected, long frame, uint minimum, uint maximum,
        string backend, string view, long revision, string provenance)
    { this.owner = owner; acceptedSelection = selected;
      Locale = owner.Locale; FrameNumber = frame; MinimumDepthBits = minimum; MaximumDepthBits = maximum;
      BackendOwnerIdentity = backend; ViewOwnerIdentity = view; WriterRevision = revision;
      SourceProvenance = provenance; }
    public string Locale { get; }
    public long FrameNumber { get; }
    public uint MinimumDepthBits { get; }
    public uint MaximumDepthBits { get; }
    public float ViewportMinimumDepth => BitConverter.UInt32BitsToSingle(MinimumDepthBits);
    public float ViewportMaximumDepth => BitConverter.UInt32BitsToSingle(MaximumDepthBits);
    public string BackendOwnerIdentity { get; }
    public string ViewOwnerIdentity { get; }
    public long WriterRevision { get; }
    public string SourceProvenance { get; }

    /// <summary>Validate this captured word revision at the consuming Main
    /// frame. A later setter/invalidation or another selection is rejected.</summary>
    public void ValidateFrame(OriginalDeferredF103SelectedSource selected, string ownedViewOwner)
    {
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownedViewOwner);
        selected.Validate();
        if (!ReferenceEquals(selected, acceptedSelection) || ownedViewOwner != ViewOwnerIdentity ||
            selected.Camera.FrameNumber != FrameNumber || selected.Current.FrameNumber != FrameNumber ||
            selected.Camera.ExecutableLocale != Locale)
            throw new InvalidDataException("Viewport depth packet belongs to another accepted Camera/View selection.");
        owner.ValidateCurrentRevision(WriterRevision, MinimumDepthBits, MaximumDepthBits);
    }
}

/// <summary>Source-created backend depth owner. Its initial words are read
/// from the verified95DC10/966EA0 constructor recipe. D75E50 copies the current
/// getter24B and changes only XYWH before95C070, preserving these two words.
/// Every later depth setter must be recorded, or this owner invalidated.</summary>
public sealed class OriginalMainMapViewportDepthSource
{
    private uint minimum, maximum;
    private bool known = true;
    private long revision, lastPublishedFrame;
    private string writerProvenance;

    private OriginalMainMapViewportDepthSource(OriginalMainViewportNativeRecipe recipe, string identity)
    {
        Locale = recipe.Locale; BackendOwnerIdentity = identity; NativeSourceProvenance = recipe.SourceProvenance;
        minimum = recipe.MinimumBits; maximum = recipe.MaximumBits;
        writerProvenance = "fresh-owned-backend-construction;" + recipe.SourceProvenance;
    }

    public string Locale { get; }
    public string BackendOwnerIdentity { get; }
    public string NativeSourceProvenance { get; }
    public bool CurrentDepthKnown => known;

    internal void ValidateCurrentRevision(long packetRevision, uint packetMinimum, uint packetMaximum)
    {
        if (!known || packetRevision != revision || packetMinimum != minimum || packetMaximum != maximum)
            throw new InvalidDataException("Viewport depth packet predates the current backend word revision.");
    }

    /// <summary>Create a new owned source backend generation, with no inherited
    /// current original-process state. This reads inert source bytes only.</summary>
    public static OriginalMainMapViewportDepthSource CreateFreshBackend(
        OriginalGameRepository repository, string locale, string ownedBackendId)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownedBackendId);
        return new(OriginalDeferredSamplerSources.LoadMainViewportNativeRecipe(repository.SourceDirectory, locale), ownedBackendId);
    }

    /// <summary>Record the exact native24B Width/Height/X/Y/min/max setter
    /// input. Both95C070 and967B20 transport the endpoint bits without math.</summary>
    public void ApplyNativeDepthSetter(ReadOnlySpan<byte> full24B, string writerProvenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(writerProvenance);
        if (full24B.Length != 24)
            throw new InvalidDataException("Native viewport depth setter requires its complete24-byte input.");
        minimum = BinaryPrimitives.ReadUInt32LittleEndian(full24B.Slice(16, 4));
        maximum = BinaryPrimitives.ReadUInt32LittleEndian(full24B.Slice(20, 4));
        known = true; revision = checked(revision + 1);
        this.writerProvenance = writerProvenance + ";native-depth-setter=95C070/967B20;rawV10/V14";
    }

    /// <summary>An untracked writer makes this generation unavailable until
    /// an explicit complete setter input establishes both current words.</summary>
    public void InvalidateCurrentDepth(string unknownWriterProvenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unknownWriterProvenance);
        known = false; revision = checked(revision + 1); writerProvenance = unknownWriterProvenance;
    }

    public OriginalMainMapViewportDepthPacket PrepareSourceMainFrame(
        OriginalDeferredF103SelectedSource selected, string ownedViewOwner)
    {
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownedViewOwner);
        selected.Validate();
        var camera = selected.Camera; var source = selected.SourcePlan;
        var reference = source.ShaderReference;
        if (!known)
            throw new InvalidDataException("Main viewport depth is unavailable after an untracked current writer: " + writerProvenance);
        if (camera.ExecutableLocale != Locale || camera.FrameNumber <= 0 ||
            camera.FrameNumber != selected.Current.FrameNumber || camera.FrameNumber < lastPublishedFrame ||
            camera.Source.Camera.ObjectId != OriginalMainMapCameraAdapter.MainCameraObjectId ||
            source.ViewId != 0 || source.Node.Index != 166 || source.Node.Call.FactoryClassId != 0xF103DB00 ||
            reference.ShaderObject.ObjectId != 0xC96F6A61 ||
            reference.ShaderObject.ClassId != OriginalRenderGraphShaderReferences.ShaderObjectClassId ||
            reference.ShaderFileHash != 0x9D8E811F || selected.Current.NativeColorMode != 0x60 ||
            (selected.OwnedFeatureSource is { } feature && feature.CurrentView.ViewIdentity != ownedViewOwner))
            throw new InvalidDataException("Viewport depth requires this same backend profile and acceptedMain38D/View0/Class868/Color60 frame.");
        lastPublishedFrame = camera.FrameNumber;
        return new(this, selected, camera.FrameNumber, minimum, maximum, BackendOwnerIdentity, ownedViewOwner, revision,
            camera.FrameProvenance + ";" + writerProvenance + ";backend=" + BackendOwnerIdentity +
            ";View=" + ownedViewOwner + ";Main-crop=TC:D75E50/SC:D76C10;getter24B-XYWH-only;depth-preserved");
    }
}

internal sealed record OriginalMainViewportNativeRecipe(string Locale, uint MinimumBits, uint MaximumBits,
    string SourceProvenance);

public static partial class OriginalDeferredSamplerSources
{
    // Reuse the existing inert PE decoder inside this partial type. Each
    // viewport window is independently pinned by the paired static audit.
    internal static OriginalMainViewportNativeRecipe LoadMainViewportNativeRecipe(string sourceDirectory, string locale)
    {
        var profile = Profile(locale);
        string path = Path.GetFullPath(Path.Combine(sourceDirectory, $"SAN14CE_{locale}.exe"));
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is < 4096 or > 128 * 1024 * 1024)
            throw new InvalidDataException("Main viewport source executable size differs.");
        byte[] exe = new byte[checked((int)stream.Length)]; stream.ReadExactly(exe);
        VerifyHash(exe, profile.ExeHash, "viewport executable");
        byte[] code = new NativeImage(exe).DecodeText(out int textRva);
        VerifyHash(code, profile.TextHash, "viewport decoded text");
        (int Constructor, int Reset, int Crop, int Setter, int Getter, string ConstructorHash, string CropHash) p = locale switch
        {
            "TC" => (0x95DC10, 0x966EA0, 0xD75E50, 0x95C070, 0x963D80,
                "A072EF4A3E3E0A814D48B99FA052561066929C9898277595C0C86BED9E8AF7A7",
                "E0BE331D2C448928336000A45B69CFB316EFE1FCA2FEF2057BEDE69F7530B9C8"),
            "SC" => (0x95ECC0, 0x968020, 0xD76C10, 0x95D140, 0x964E30,
                "4CA5B91FA5A658D32875107CA149593A5173BC976095CD7FDE0E859DA6296342",
                "FB713A719A83B017B29F2EC5D306FB940D69ECCCD66B3C1A9E5C56F293122732"),
            _ => throw new InvalidDataException("Main viewport requires a pinnedSC/TC profile.")
        };
        void Window(int rva, int count, string hash) => VerifyHash(code.AsSpan(checked(rva - textRva), count), hash, "viewport window");
        Window(p.Constructor, 1220, p.ConstructorHash);
        Window(p.Reset, 730, "96CF43A335C06277B910CA515C71D79FD79311D253EFBD351A227C2EE1BA2C9E");
        Window(p.Crop, 614, p.CropHash);
        Window(p.Setter, 244, "510DA55498D60ACD0BE1E3C51B7CA72E6713AD605AF1B09ACE46B99C9C02ECFF");
        Window(p.Getter, 82, "610918DB7207637292D312F195D8EB7A71FEE6B1C8DB45EAFBBA215FF47C2835");
        var minWriter = code.AsSpan(p.Reset - textRva + 0xFC, 11);
        var maxWriter = code.AsSpan(p.Reset - textRva + 0x18E, 10);
        if (!minWriter[..7].SequenceEqual(Convert.FromHexString("48C78138010000")) ||
            !maxWriter[..6].SequenceEqual(Convert.FromHexString("C7817C010000")))
            throw new InvalidDataException("Main viewport constructor endpoint writer operands differ.");
        // MOV qword[138], imm32 sign-extends. Minimum at13C is its upper word.
        long qword = BinaryPrimitives.ReadInt32LittleEndian(minWriter[7..]);
        uint minimum = unchecked((uint)((ulong)qword >> 32));
        uint maximum = BinaryPrimitives.ReadUInt32LittleEndian(maxWriter[6..]);
        if (minimum != 0 || maximum != 0x3F800000)
            throw new InvalidDataException("Main viewport constructor depth words differ.");
        return new(locale, minimum, maximum,
            $"san14://file/SAN14CE_{locale}.exe@{profile.ExeHash};decoded-text={profile.TextHash};" +
            $"backend-ctor={p.Constructor:X8};reset={p.Reset:X8};min-write={p.Reset + 0xFC:X8};max-write={p.Reset + 0x18E:X8};" +
            "proof=ce-main-viewport-depth-source-20261003;owned-source-generation");
    }
}
