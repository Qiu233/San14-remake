using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>Explicit identities for the same selected View's +1B20 record and
/// that record's +8 complete LightData. These are remake/captured owner tokens,
/// never process addresses or a Camera declaration selected by analogy.</summary>
public sealed record SkySceneSelectedViewLightIdentity(long FrameNumber,
    string SelectedViewOwner, string ProviderRecordOwner, string ProviderPacketOwner);

public sealed class SkySceneSelectedViewLightPacket
{
    private readonly byte[] bytes;
    internal SkySceneSelectedViewLightPacket(SkySceneSelectedViewLightIdentity identity, byte[] bytes)
    {
        Identity = identity; this.bytes = bytes;
        SourceProvenance = $"selected-View={identity.SelectedViewOwner};record1B20={identity.ProviderRecordOwner};" +
            $"record8-LightData={identity.ProviderPacketOwner};frame={identity.FrameNumber};" +
            SkySceneSelectedViewLightSource.NativeContract;
    }
    public SkySceneSelectedViewLightIdentity Identity { get; }
    public string SourceProvenance { get; }
    public ReadOnlyMemory<byte> CompleteLightData => bytes;
}

/// <summary>Exact D7 selected View provider packet to Sun/Moon setter inputs.
/// The caller resolves the View/record/packet identities before capture. The
/// source selection predicate and any preceding LightData writers stay with
/// that owner; Camera3/4 and ordinary directional rows do not select it.</summary>
public static class SkySceneSelectedViewLightSource
{
    public const string NativeContract =
        "TC-D78B4B:View1B20=selected-kind1-record;D7CEEB-D7CEFB:record8;" +
        "D7CF08-D7CF46:F80-FA0-F90-FB0;D7CF4E:118DA90;D7CF5D:118D7E0";

    public static SkySceneSelectedViewLightPacket Capture(
        ReadOnlySpan<byte> explicitComplete10C0, SkySceneSelectedViewLightIdentity explicitIdentity)
    {
        ArgumentNullException.ThrowIfNull(explicitIdentity);
        if (explicitComplete10C0.Length != CameraDirectionalLightData.LightDataBytes ||
            explicitIdentity.FrameNumber < 1 || new[] { explicitIdentity.SelectedViewOwner,
                explicitIdentity.ProviderRecordOwner, explicitIdentity.ProviderPacketOwner }.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Selected View sky lights require one explicit frame, actual record/packet identities and complete10C0 bytes.");
        return new(explicitIdentity, explicitComplete10C0.ToArray());
    }

    /// <summary>The remake's accepted F103 selected kind1 record also supplies
    /// the sky producer. Its original record+8 packet stays separate from the
    /// F103 modifier's effective copied packet. This is an owned selected View
    /// publication, not proof of a live original-process View+1B20 pointer.</summary>
    public static SkySceneSelectedViewLightPacket CaptureOwnedKind1(
        MapSourceCreatedF103FeaturePacket feature,
        MapSourceCreatedLightingPacket lighting, string ownedSelectedRecordIdentity)
    {
        ArgumentNullException.ThrowIfNull(feature); ArgumentNullException.ThrowIfNull(lighting);
        feature.ValidateFrame(feature.AcceptedCamera, lighting);
        if (string.IsNullOrWhiteSpace(ownedSelectedRecordIdentity) ||
            feature.CurrentView.SelectedRecordLightOwnerIdentity != lighting.Binding.LightOwnerIdentity)
            throw new InvalidDataException("Owned sky publication requires the same accepted F103 kind1 record and its explicit record identity.");
        return Capture(lighting.LocalLightData.Span, new(lighting.FrameNumber,
            feature.CurrentView.ViewIdentity, ownedSelectedRecordIdentity, lighting.Binding.LightOwnerIdentity));
    }

    /// <summary>D7 calls both setters with nonnull copied global records after
    /// the provider gate succeeds. Zero color is not light absence: native max
    /// zero divides by zero, and the finite cache domain explicitly rejects it.
    /// Package+10 light position/owner is supplied independently for SkyInfo1.</summary>
    public static SkySceneLights PrepareSkyLights(SkySceneSelectedViewLightPacket selectedPacket,
        Vector4 explicitPackageLightPosition, string explicitPackageLightOwner)
    {
        ArgumentNullException.ThrowIfNull(selectedPacket);
        if (string.IsNullOrWhiteSpace(explicitPackageLightOwner))
            throw new InvalidDataException("Sky package+10 light owner must be explicit.");
        foreach (float value in new[] { explicitPackageLightPosition.X, explicitPackageLightPosition.Y,
            explicitPackageLightPosition.Z, explicitPackageLightPosition.W }) Finite(value);
        ReadOnlySpan<byte> bytes = selectedPacket.CompleteLightData.Span;
        Vector4 sun = Vector(bytes, 0xF80), moon = Vector(bytes, 0xF90);
        Vector3 sunRgb = Rgb(bytes, 0xFA0), moonRgb = Rgb(bytes, 0xFB0);
        var sunCache = Sky2CameraLightPreparation.PrepareRgbCache(sunRgb);
        var moonCache = Sky2CameraLightPreparation.PrepareRgbCache(moonRgb);
        return new(selectedPacket.SourceProvenance + $";package10-light={explicitPackageLightOwner}",
            true, sun, sunCache, true, moon, moonRgb, moonCache.Maximum, explicitPackageLightPosition);
    }

    /// <summary>Actual source mode2 Moon setter remains nonnull and computes
    /// its native zero cache. Only the locked default programs can consume the
    /// resulting unused Moon amplitude; arbitrary current packets stay strict.</summary>
    public static SkySceneLights PrepareSourceZeroMoonLights(
        SkySceneSelectedViewLightPacket selectedPacket, Vector4 packagePosition,
        string packageOwner, Sky2SourceZeroMoon proof)
    {
        ArgumentNullException.ThrowIfNull(proof); proof.ValidatePacket(selectedPacket);
        if (string.IsNullOrWhiteSpace(packageOwner)) throw new InvalidDataException("Sky package light owner is required.");
        Finite(packagePosition.X); Finite(packagePosition.Y); Finite(packagePosition.Z); Finite(packagePosition.W);
        ReadOnlySpan<byte> b = selectedPacket.CompleteLightData.Span;
        Vector3 moonRgb = Rgb(b, 0xFB0);
        var moonCache = Sky2CameraLightPreparation.PrepareSourceZeroMoonCache(moonRgb, proof);
        return new(selectedPacket.SourceProvenance + ";" + proof.SourceProvenance + $";package10-light={packageOwner}",
            true, Vector(b, 0xF80), Sky2CameraLightPreparation.PrepareRgbCache(Rgb(b, 0xFA0)),
            true, Vector(b, 0xF90), moonRgb, moonCache.Maximum, packagePosition)
            { SourceZeroMoon = proof, SourceMoonRgbCache1470 = moonCache };
    }

    private static Vector4 Vector(ReadOnlySpan<byte> b, int at) => new(Read(b, at), Read(b, at+4), Read(b, at+8), Read(b, at+12));
    private static Vector3 Rgb(ReadOnlySpan<byte> b, int at) => new(Read(b, at), Read(b, at+4), Read(b, at+8));
    private static float Read(ReadOnlySpan<byte> b, int at)
    { float value = BinaryPrimitives.ReadSingleLittleEndian(b.Slice(at, 4)); Finite(value); return value; }
    private static void Finite(float value)
    { if (!float.IsFinite(value)) throw new InvalidDataException("Selected View sky light input is outside the finite consumed packet domain."); }
}
