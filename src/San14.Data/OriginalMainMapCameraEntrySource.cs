using System.Buffers.Binary;

namespace San14.Data;

/// <summary>The three null gates reached by the original title-exit clip writer.
/// The caller owns a live Game object; its Main Data/CPU and Host+210 are separate inputs.</summary>
public readonly record struct OriginalMainMapCameraEntryInputs(
    bool MainDataPresent, bool HostGate210Present, bool MainCpuPresent);

public readonly record struct OriginalMainMapCameraEntryFarWrite(
    bool Applied, float SourceFar14, bool NativeViewportNotifyReached);

/// <summary>Static source for one normal entry into the playable map.
/// Retain this source and the resulting Far14; no per-frame native loading is needed.</summary>
public sealed class LoadedOriginalMainMapCameraEntrySource
{
    internal LoadedOriginalMainMapCameraEntrySource(string locale, string executableSha256,
        int farInteger, string evidence)
        => (Locale, ExecutableSha256, TitleExitFarInteger, SourceEvidence) =
            (locale, executableSha256, farInteger, evidence);

    public string Locale { get; }
    public string ExecutableSha256 { get; }
    public int TitleExitFarInteger { get; }
    public string SourceEvidence { get; }

    /// <summary>CTitleState.V10 supplies a signed integer, converted by MOVD/CVTDQ2PS.
    /// A rejected native null gate preserves the prior raw float. The optional B320
    /// viewport notification is reported, not invoked; subsequent Camera preparation
    /// publishes this retained source far into the lens and projection matrices.</summary>
    public OriginalMainMapCameraEntryFarWrite ApplyTitleExitFar(
        OriginalMainMapCameraEntryInputs inputs, float retainedSourceFar14)
    {
        if (!inputs.MainDataPresent || !inputs.HostGate210Present || !inputs.MainCpuPresent)
            return new(false, retainedSourceFar14, false);
        return new(true, (float)TitleExitFarInteger, true);
    }
}

public static class OriginalMainMapCameraEntrySource
{
    public static LoadedOriginalMainMapCameraEntrySource Load(OriginalGameRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.LoadMainMapCameraEntrySource(repository.SourceDirectory, locale);
    }
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMainMapCameraEntrySource LoadMainMapCameraEntrySource(
        string sourceDirectory, string locale)
    {
        var profile = Profile(locale);
        byte[] exe = File.ReadAllBytes(Path.Combine(sourceDirectory, $"SAN14CE_{locale}.exe"));
        VerifyHash(exe, profile.ExeHash, "Main map entry executable");
        var image = new NativeImage(exe);
        byte[] text = image.DecodeText(out int textRva);
        VerifyHash(text, profile.TextHash, "Main map entry decoded text");
        bool tc = locale == "TC";
        int callerRva = tc ? 0x4FA7D3 : 0x4FA753;
        int writerRva = tc ? 0x156900 : 0x1566B0;
        int getterRva = tc ? 0x14CC30 : 0x14C9D0;
        ReadOnlySpan<byte> Window(int rva, int bytes, string sha, string name)
        {
            var window = text.AsSpan(rva - textRva, bytes);
            VerifyHash(window, sha, name);
            return window;
        }
        var caller = Window(callerRva, 18, tc
            ? "258DBBF62B7903297ED12776F602B61A4E65E7BBDC98970543BBCFC5684BA746"
            : "BB11533E813F842A80FB8E75D0189A9E6B31B9CC289C3B88263CB6C2439BB236", "title-exit Far call");
        Window(writerRva, 56, tc
            ? "9711992AA259CD916C50932045646F1E132BEC6224CA30F25F6C6B292DCFF7B6"
            : "5289BD207DC74D4F9D8426CEF350B058C1D59C415597F5DF16E7CD4BDF551EE8", "Main Far null-gate/store leaf");
        Window(getterRva, 8, tc
            ? "2FCA46FC8C4D9549B16C2A967BF3DF0AC38D8630CD610039B3652408B04FA427"
            : "3838D65637721866C18D4A21E1EA160036DF0A61933B86F922C8956E7111559D", "title-exit Host getter");
        Window(0xB320, 77, tc
            ? "03611E196B15C4C70CCE4AA687A024DEA44AA2CDECC67DC214B9D467AB5A9FA8"
            : "D0D014B52DD24656C9E245CB19C4397D8B0EECF654B36708F52DE1F49475EF54", "optional viewport notification");

        // These reads belong to the accepted original CALL/MOV/CALL bytes at Load.
        int farInteger = BinaryPrimitives.ReadInt32LittleEndian(caller.Slice(9, 4));
        return new(locale, profile.ExeHash, farInteger,
            $"CTitleState.V10;{locale}:{callerRva:X8}->Host{getterRva:X8}->EDX{farInteger}->" +
            $"{writerRva:X8};Game78/Data20;Host210-null-gate;MainCPU14;MOVD/CVTDQ2PS;" +
            "B320-optional-NDC-to-screen-notification;one-map-entry;original-process-not-observed");
    }
}
