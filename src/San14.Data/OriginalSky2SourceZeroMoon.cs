using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>One actual source-created mode2 View packet, whose nonnull Moon
/// setter receives +0 RGB. The two default original Sky2 programs never read
/// MoonRGB/MoonDir. The native NaN cache and +Inf amplitude are retained; this
/// capability permits only that unused amplitude in those exact programs.</summary>
public sealed class OriginalSky2SourceZeroMoon
{
    public const string AtmosphereProgramSha256 = OriginalSky2NullTextureAdapter.DefaultPixelSourceSha;
    public const string ContributionProgramSha256 = "FA1B12A79D043104E98FE73A57F85D658B8913630802F8C7133A4ECDEAF7FEFA";
    public const string AtmosphereRdefSha256 = "07968ADE85829BC7AF1467C579650502E8A21F0A22C0FCCDA6CC62357B95C110";
    public const string ContributionRdefSha256 = "26AB110240A9ED99C4EF11E77799A232A4C844462E3DD1FE0E3984B5F6E45915";
    public const string DirectionalAtmosphereProgramSha256 = "3EF36F9A865187ECECF785135F322C5CAFD62D7E0A2EF7B70731FFAD565BBE06";
    public const string DirectionalAtmosphereRdefSha256 = "806CE4187E93332191C3FF86EE89D0C73AF23244245E729C2B9F18AE2AF398BC";
    private readonly OriginalSky2SourceNull2D sourceMoonTexture;
    private readonly string packetSha256;
    private readonly string allowedAtmosphereRdefSha256;
    private readonly OriginalSkyDirectionalSelectedLightSource? directionalSource;
    private OriginalSky2SourceZeroMoon(OriginalSky2SourceNull2D moon,
        OriginalSkySceneSelectedViewLightPacket packet, OriginalMapSourceCreatedLightingPacket lighting,
        OriginalSkyDirectionalSelectedLightSource? directional = null, string? atmosphereRdef = null)
    {
        sourceMoonTexture = moon; Identity = packet.Identity;
        directionalSource = directional; allowedAtmosphereRdefSha256 = atmosphereRdef ?? AtmosphereRdefSha256;
        packetSha256 = Hash(packet.CompleteLightData.Span);
        SourceProvenance = packet.SourceProvenance + ";" + lighting.SourceProvenance + ";" + moon.SourceEvidence +
            ";nonnull-Moon-setter118D7E0:+0-max,+Inf-reciprocal,three-native-invalid-products;" +
            "118DC30:raw-MoonRGB,+Inf-amplitude;original-selected-MoonRGB-unused;no-program-transform" +
            (directional is null ? ";mode2-PS3/4-MoonDir-unused" : ";" + directional.SourceProvenance +
                ";mode6-source-PS10-MoonDir-used/MoonRGB-unused-or-swapped-PS3;ContributionPS4");
    }
    public OriginalSkySceneSelectedViewLightIdentity Identity { get; }
    public string SourceProvenance { get; }

    public static OriginalSky2SourceZeroMoon Create(LoadedShaderContainer shader,
        OriginalSky2SourceNull2D sourceMoonTexture, OriginalMapSourceCreatedLightingPacket acceptedLighting,
        OriginalSkySceneSelectedViewLightPacket selectedViewPacket)
    {
        ArgumentNullException.ThrowIfNull(shader); ArgumentNullException.ThrowIfNull(sourceMoonTexture);
        ArgumentNullException.ThrowIfNull(acceptedLighting); ArgumentNullException.ThrowIfNull(selectedViewPacket);
        sourceMoonTexture.Validate();
        if (sourceMoonTexture.CameraReference.Role != OriginalSkySamplingRole.Moon ||
            sourceMoonTexture.Scope != OriginalSkyTextureStateScope.AppliedSourceSeasonOnFreshShader ||
            shader.Source.OverrideSource is not null ||
            Hash(shader.Container.GetProgramBytes(shader.Source.Bytes, 3)) != AtmosphereProgramSha256 ||
            Hash(shader.Container.GetProgramBytes(shader.Source.Bytes, 4)) != ContributionProgramSha256 ||
            Fingerprint(shader.ReadResourceDefinitions(3)) != AtmosphereRdefSha256 ||
            Fingerprint(shader.ReadResourceDefinitions(4)) != ContributionRdefSha256)
            throw new InvalidDataException("Source zero Moon requires the exact applied-source fresh shader and both unchanged default original programs.");
        var id = selectedViewPacket.Identity; var binding = acceptedLighting.Binding;
        if (id.FrameNumber != acceptedLighting.FrameNumber || id.SelectedViewOwner != binding.ViewIdentity ||
            id.ProviderPacketOwner != binding.LightOwnerIdentity ||
            !selectedViewPacket.CompleteLightData.Span.SequenceEqual(acceptedLighting.LocalLightData.Span))
            throw new InvalidDataException("Source zero Moon requires the same accepted owned View record and actual lighting generation.");
        byte[] constructed = acceptedLighting.LocalLightData.ToArray();
        if (acceptedLighting.CameraContextApplied)
        {
            if (constructed[0xF1E] != 2) throw new InvalidDataException("Source mode2 Camera context writer changed.");
            constructed[0xF1E] = 1;
        }
        if (Hash(constructed) != acceptedLighting.SourceIdentity.CreatedLightDataSha256 ||
            constructed[0x10B0] != 2 || Word(constructed, 0xFA0) != 0x3F800000 ||
            Word(constructed, 0xFA4) != 0x3F800000 || Word(constructed, 0xFA8) != 0x3F800000 ||
            Enumerable.Range(0, 3).Any(i => Word(constructed, 0xFB0 + i * 4) != 0))
            throw new InvalidDataException("Zero Moon capability is limited to the unchanged actual source-created mode2 light; current edits require another producer.");
        return new(sourceMoonTexture, selectedViewPacket, acceptedLighting);
    }

    /// <summary>A separate mode6 publication, using actual prepared Sun/Moon
    /// fields. Nonnull zero Moon remains native +Inf/NaN; only exact PS10/4
    /// (or the swapped PS3/4) may ignore its unused RGB amplitude. PS11 reads
    /// MoonRGB and never receives this capability.</summary>
    public static OriginalSky2SourceZeroMoon CreateDirectionalSelected(LoadedShaderContainer shader,
        OriginalSky2SourceNull2D sourceMoonTexture, OriginalSkyDirectionalSelectedLightSource source)
    {
        ArgumentNullException.ThrowIfNull(shader); ArgumentNullException.ThrowIfNull(sourceMoonTexture);
        ArgumentNullException.ThrowIfNull(source); source.ValidateCurrent(); sourceMoonTexture.Validate();
        bool alternate = (source.SelectedPacket.CompleteLightData.Span[0xFDC] & 2) != 0;
        int atmosphereIndex = alternate ? 10 : 3;
        string programSha = alternate ? DirectionalAtmosphereProgramSha256 : AtmosphereProgramSha256;
        string rdefSha = alternate ? DirectionalAtmosphereRdefSha256 : AtmosphereRdefSha256;
        if (!source.HasExactZeroMoonRgb || sourceMoonTexture.CameraReference.Role != OriginalSkySamplingRole.Moon ||
            sourceMoonTexture.Scope != OriginalSkyTextureStateScope.AppliedSourceSeasonOnFreshShader ||
            shader.Source.OverrideSource is not null ||
            Hash(shader.Container.GetProgramBytes(shader.Source.Bytes, atmosphereIndex)) != programSha ||
            Hash(shader.Container.GetProgramBytes(shader.Source.Bytes, 4)) != ContributionProgramSha256 ||
            Fingerprint(shader.ReadResourceDefinitions(atmosphereIndex)) != rdefSha ||
            Fingerprint(shader.ReadResourceDefinitions(4)) != ContributionRdefSha256)
            throw new InvalidDataException("Directional zero Moon requires its exact same-frame source and original MoonRGB-unused program scope.");
        return new(sourceMoonTexture, source.SelectedPacket, source.AcceptedLighting, source, rdefSha);
    }

    public void ValidatePacket(OriginalSkySceneSelectedViewLightPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet); sourceMoonTexture.Validate(); directionalSource?.ValidateCurrent();
        if (packet.Identity != Identity || Hash(packet.CompleteLightData.Span) != packetSha256)
            throw new InvalidDataException("Source zero Moon capability belongs to one immutable accepted View packet.");
    }

    public void ValidateLighting(OriginalMapSourceCreatedLightingPacket lighting)
    {
        ArgumentNullException.ThrowIfNull(lighting); sourceMoonTexture.Validate(); directionalSource?.ValidateCurrent();
        if (lighting.FrameNumber != Identity.FrameNumber || lighting.Binding.ViewIdentity != Identity.SelectedViewOwner ||
            lighting.Binding.LightOwnerIdentity != Identity.ProviderPacketOwner || Hash(lighting.LocalLightData.Span) != packetSha256)
            throw new InvalidDataException("Source Moon zero cache belongs to the same accepted current lighting frame and complete source record packet.");
    }

    internal void ValidateZeroRgb(Vector3 rgb, float maximum)
    {
        sourceMoonTexture.Validate();
        if (Bits(rgb.X) != 0 || Bits(rgb.Y) != 0 || Bits(rgb.Z) != 0 || Bits(maximum) != 0)
            throw new InvalidDataException("Source zero Moon capability requires the exact native +0 RGB/max operands.");
    }

    internal void ValidatePreparedRgb(Vector4 rgb)
    {
        ValidateZeroRgb(new(rgb.X, rgb.Y, rgb.Z), 0);
        if (Bits(rgb.W) != 0x7F800000)
            throw new InvalidDataException("Source zero Moon amplitude must preserve the native positive infinity result.");
    }

    internal void ValidateDefinitions(DxbcResourceDefinitions definitions)
    {
        string hash = Fingerprint(definitions);
        if (hash != allowedAtmosphereRdefSha256 && hash != ContributionRdefSha256)
            throw new InvalidDataException("Nonfinite source Moon amplitude is unused only in the two exact default original Sky2 programs.");
    }

    private static uint Bits(float value) => BitConverter.SingleToUInt32Bits(value);
    private static uint Word(byte[] bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at, 4));
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string Fingerprint(DxbcResourceDefinitions definitions) => Hash(JsonSerializer.SerializeToUtf8Bytes(definitions));
}
