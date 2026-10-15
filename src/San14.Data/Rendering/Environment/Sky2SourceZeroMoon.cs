using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>A nonnull Moon setter with exact zero baseline color and an actual
/// null texture binding. Legacy selected packet identity is optional.
/// Its nonfinite normalization result is retained; shader consumers
/// decide whether the Moon field is unused before accepting that amplitude.</summary>
public sealed class Sky2SourceZeroMoon
{
    public const string AtmosphereProgramSha256 = Sky2NullTextureAdapter.DefaultPixelSourceSha;
    public const string ContributionProgramSha256 = "FA1B12A79D043104E98FE73A57F85D658B8913630802F8C7133A4ECDEAF7FEFA";
    public const string DirectionalAtmosphereProgramSha256 = "3EF36F9A865187ECECF785135F322C5CAFD62D7E0A2EF7B70731FFAD565BBE06";
    private readonly Sky2NullTextureBinding sourceMoonTexture;
    private readonly SkyDirectionalSelectedLightSource? directionalSource;
    private Sky2SourceZeroMoon(Sky2NullTextureBinding moon,
        SkySceneSelectedViewLightIdentity? identity, string provenance,
        SkyDirectionalSelectedLightSource? directional = null)
    {
        sourceMoonTexture = moon; Identity = identity;
        directionalSource = directional;
        SourceProvenance = provenance + ";" + moon.Role.ToString() +
            ";nonnull-Moon-setter118D7E0:+0-max,+Inf-reciprocal,three-native-invalid-products;" +
            "118DC30:raw-MoonRGB,+Inf-amplitude;original-selected-MoonRGB-unused;no-program-transform" +
            (identity is null ? ";typed-zero-Moon;selected-program-MoonRGB-unused-required" :
                directional is null ? ";mode2-PS3/4-MoonDir-unused" : ";" + directional.SourceProvenance +
                ";mode6-source-PS10-MoonDir-used/MoonRGB-unused-or-swapped-PS3;ContributionPS4");
    }
    public SkySceneSelectedViewLightIdentity? Identity { get; }
    public string SourceProvenance { get; }

    public static Sky2SourceZeroMoon Create(Sky2NullTextureBinding sourceMoonTexture, Vector3 baselineMoonRgb)
    {
        RequireMoonTexture(sourceMoonTexture);
        RequireZeroRgb(baselineMoonRgb, 0);
        return new(sourceMoonTexture, null, "actual-null-Moon-binding;exact-positive-zero-baseline");
    }

    public static Sky2SourceZeroMoon Create(
        Sky2NullTextureBinding sourceMoonTexture, MapSourceCreatedLightingPacket acceptedLighting,
        SkySceneSelectedViewLightPacket selectedViewPacket)
    {
        ArgumentNullException.ThrowIfNull(acceptedLighting); ArgumentNullException.ThrowIfNull(selectedViewPacket);
        RequireMoonTexture(sourceMoonTexture);
        var id = selectedViewPacket.Identity; var binding = acceptedLighting.Binding;
        if (id.FrameNumber != acceptedLighting.FrameNumber || id.SelectedViewOwner != binding.ViewIdentity ||
            id.ProviderPacketOwner != binding.LightOwnerIdentity)
            throw new InvalidDataException("Source zero Moon requires the same accepted owned View record and actual lighting generation.");
        ReadOnlySpan<byte> constructed = acceptedLighting.LocalLightData.Span;
        if (acceptedLighting.CameraContextApplied)
        {
            if (constructed[0xF1E] != 2) throw new InvalidDataException("Source mode2 Camera context writer changed.");
        }
        if (constructed[0x10B0] != 2 || Word(constructed, 0xFA0) != 0x3F800000 ||
            Word(constructed, 0xFA4) != 0x3F800000 || Word(constructed, 0xFA8) != 0x3F800000)
            throw new InvalidDataException("Zero Moon capability is limited to the unchanged actual source-created mode2 light; current edits require another producer.");
        RequireZeroRgb(new(BinaryPrimitives.ReadSingleLittleEndian(constructed[0xFB0..]),
            BinaryPrimitives.ReadSingleLittleEndian(constructed[0xFB4..]),
            BinaryPrimitives.ReadSingleLittleEndian(constructed[0xFB8..])), 0);
        return new(sourceMoonTexture, selectedViewPacket.Identity,
            selectedViewPacket.SourceProvenance + ";" + acceptedLighting.SourceProvenance);
    }

    /// <summary>A separate mode6 publication, using actual prepared Sun/Moon
    /// fields. Nonnull zero Moon remains native +Inf/NaN; only exact PS10/4
    /// (or the swapped PS3/4) may ignore its unused RGB amplitude. PS11 reads
    /// MoonRGB and never receives this capability.</summary>
    public static Sky2SourceZeroMoon CreateDirectionalSelected(
        Sky2NullTextureBinding sourceMoonTexture, SkyDirectionalSelectedLightSource source)
    {
        ArgumentNullException.ThrowIfNull(source); source.ValidateCurrent();
        RequireMoonTexture(sourceMoonTexture);
        RequireZeroRgb(source.Fields.MoonRgb, 0);
        return new(sourceMoonTexture, source.SelectedPacket.Identity,
            source.SelectedPacket.SourceProvenance + ";" + source.AcceptedLighting.SourceProvenance, source);
    }

    public void ValidatePacket(SkySceneSelectedViewLightPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet); sourceMoonTexture.Validate(); directionalSource?.ValidateCurrent();
        if (Identity is null || packet.Identity != Identity)
            throw new InvalidDataException("Source zero Moon capability belongs to one immutable accepted View packet.");
    }

    public void ValidateLighting(MapSourceCreatedLightingPacket lighting)
    {
        ArgumentNullException.ThrowIfNull(lighting); sourceMoonTexture.Validate(); directionalSource?.ValidateCurrent();
        if (Identity is not { } identity || lighting.FrameNumber != identity.FrameNumber ||
            lighting.Binding.ViewIdentity != identity.SelectedViewOwner ||
            lighting.Binding.LightOwnerIdentity != identity.ProviderPacketOwner)
            throw new InvalidDataException("Source Moon zero cache belongs to the same accepted current lighting frame and complete source record packet.");
    }

    internal void ValidateZeroRgb(Vector3 rgb, float maximum)
    {
        sourceMoonTexture.Validate();
        RequireZeroRgb(rgb, maximum);
    }

    private static void RequireMoonTexture(Sky2NullTextureBinding sourceMoonTexture)
    {
        ArgumentNullException.ThrowIfNull(sourceMoonTexture);
        sourceMoonTexture.Validate();
        if (sourceMoonTexture.Role != SkySamplingRole.Moon)
            throw new InvalidDataException("Zero Moon lighting requires its captured Moon texture binding.");
    }

    private static void RequireZeroRgb(Vector3 rgb, float maximum)
    {
        if (Bits(rgb.X) != 0 || Bits(rgb.Y) != 0 || Bits(rgb.Z) != 0 || Bits(maximum) != 0)
            throw new InvalidDataException("Source zero Moon capability requires the exact native +0 RGB/max operands.");
    }

    internal void ValidatePreparedRgb(Vector4 rgb)
    {
        ValidateZeroRgb(new(rgb.X, rgb.Y, rgb.Z), 0);
        if (Bits(rgb.W) != 0x7F800000)
            throw new InvalidDataException("Source zero Moon amplitude must preserve the native positive infinity result.");
    }

    private static uint Bits(float value) => BitConverter.SingleToUInt32Bits(value);
    private static uint Word(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(at, 4));
}
