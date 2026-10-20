using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Whole-stream observations of one exact decoder input. This is
/// separate from the WAV header's optional mask and never changes that header.</summary>
public sealed class DecodedSpeakerLayout
{
    public uint NativeChannelMask { get; }
    public int Channels { get; }
    public int SampleRate { get; }
    public long SampleFrames { get; }
    public long ObservedAudioFrames { get; }
    public string DecoderInputSha256 { get; }
    public string PcmSha256 { get; }
    public string DecoderSha256 { get; }
    public string ValidatorPath { get; }
    public string ValidatorSha256 { get; }
    public string CodecSha256 { get; }
    public string UtilitySha256 { get; }
    public string FormatSha256 { get; }

    internal DecodedSpeakerLayout(uint mask, PcmWave wave, AudioDecodeProvenance source,
        string validatorPath, string validatorHash, string codecHash, string utilityHash, string formatHash, long observedFrames)
    {
        NativeChannelMask = mask; Channels = wave.Channels; SampleRate = wave.SampleRate;
        SampleFrames = wave.SampleFrames; ObservedAudioFrames = observedFrames;
        DecoderInputSha256 = source.DecoderInputSha256; PcmSha256 = source.PcmSha256;
        DecoderSha256 = source.DecoderSha256; ValidatorPath = validatorPath;
        ValidatorSha256 = validatorHash; CodecSha256 = codecHash; UtilitySha256 = utilityHash;
        FormatSha256 = formatHash;
    }

    public bool AppliesTo(DecodedAudio audio) => audio.Wave.Channels == Channels &&
        audio.Wave.SampleRate == SampleRate && audio.Wave.SampleFrames == SampleFrames &&
        audio.Provenance.DecoderInputSha256 == DecoderInputSha256 &&
        audio.Provenance.PcmSha256 == PcmSha256 && audio.Provenance.DecoderSha256 == DecoderSha256 &&
        audio.Provenance.RuntimeFileSha256.GetValueOrDefault("vgmstream-cli.exe") == DecoderSha256 &&
        audio.Provenance.RuntimeFileSha256.GetValueOrDefault("avformat-vgmstream-59.dll") == FormatSha256 &&
        audio.Provenance.RuntimeFileSha256.GetValueOrDefault("avcodec-vgmstream-59.dll") == CodecSha256 &&
        audio.Provenance.RuntimeFileSha256.GetValueOrDefault("avutil-vgmstream-57.dll") == UtilitySha256;
}

/// <summary>Observes every AAC output frame using the pinned decoder libraries.
/// The auxiliary executable has its own manifest; it is not an upstream runtime
/// file. Its proof relates to the fixed CLI's packet/interleaving contract, not
/// an instrumentation hook inside the CLI. No PCM is produced or replaced.</summary>
public sealed class AudioKtacLayoutValidator
{
    private const string FixedCli = "29DF08C557ADA8269C92A6ABFE3886AD2F65849B0B26BA78A0819B363BBC5B85";
    private const string FixedFormat = "0DDE66EA268CCCB67B1AA219C1CE879602B839E561A79A00C8215E2688F1A4B2";
    private const string FixedCodec = "4D6CF54E7B3C26EF06E95BDB515A10D1E1BED3EC1FFD87EC3E5C4FBCD2A883F6";
    private const string FixedUtility = "A999856C17CFDCE3C22B92B6640A7ADA6C4AB4C3C6841A863770B72FA0BC19B4";
    public string ExecutablePath { get; }
    public string ExecutableSha256 { get; }
    public TimeSpan Timeout { get; }

    public AudioKtacLayoutValidator(string executablePath, string expectedSha256, TimeSpan? timeout = null)
    {
        ExecutablePath = Path.GetFullPath(executablePath);
        ExecutableSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ExecutablePath)));
        if (!ExecutableSha256.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Audio layout validator differs from its own build manifest.");
        Timeout = timeout ?? TimeSpan.FromSeconds(60);
        if (Timeout <= TimeSpan.Zero || Timeout > TimeSpan.FromMinutes(5)) throw new ArgumentOutOfRangeException(nameof(timeout));
    }

    public static AudioKtacLayoutValidator FromManifest(string manifestPath)
    {
        string full = Path.GetFullPath(manifestPath);
        using var json = JsonDocument.Parse(File.ReadAllBytes(full));
        var value = json.RootElement;
        if (value.GetProperty("format").GetString() != "san14-ktac-layout-validator" || value.GetProperty("version").GetInt32() != 1)
            throw new InvalidDataException("Audio layout validator manifest has an unsupported format.");
        string name = value.GetProperty("executable").GetString() ?? "";
        if (string.IsNullOrWhiteSpace(name) || Path.GetFileName(name) != name || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidDataException("Audio layout validator must be a leaf file beside its manifest.");
        return new(Path.Combine(Path.GetDirectoryName(full)!, name), value.GetProperty("sha256").GetString() ?? "");
    }

    internal async Task<DecodedSpeakerLayout> ObserveAsync(string inputPath, string runtimeDirectory,
        AudioDecodeInput input, PcmWave wave, AudioDecodeProvenance provenance, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (input.Codec != "KTAC" || wave.Channels != 6 || wave.SampleRate != 48000)
            throw new NotSupportedException("Whole-stream speaker observation supports six-channel KTAC at 48000 Hz.");
        var runtime = provenance.RuntimeFileSha256;
        if (provenance.DecoderSha256 != FixedCli || runtime.GetValueOrDefault("vgmstream-cli.exe") != FixedCli ||
            runtime.GetValueOrDefault("avformat-vgmstream-59.dll") != FixedFormat ||
            runtime.GetValueOrDefault("avcodec-vgmstream-59.dll") != FixedCodec ||
            runtime.GetValueOrDefault("avutil-vgmstream-57.dll") != FixedUtility)
            throw new NotSupportedException("This speaker observation requires the source-verified fixed CLI and FFmpeg pipeline.");
        if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ExecutablePath))) != ExecutableSha256)
            throw new InvalidDataException("Audio layout validator changed before execution.");
        string outputPath = Path.Combine(Path.GetDirectoryName(inputPath)!, "layout.json");
        try
        {
            var start = new ProcessStartInfo(ExecutablePath)
            {
                UseShellExecute = false, CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(inputPath)!,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (string argument in new[] { "--input", inputPath, "--runtime-dir", runtimeDirectory,
                "--expected-pcm-frames", wave.SampleFrames.ToString(CultureInfo.InvariantCulture), "--output", outputPath })
                start.ArgumentList.Add(argument);
            using Process process = Process.Start(start) ?? throw new IOException("Audio layout validator did not start.");
            Task<string> stdout = DrainAsync(process.StandardOutput), stderr = DrainAsync(process.StandardError);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Timeout);
            try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) when (process.HasExited) { }
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException("Audio layout validator exceeded its operation timeout.");
            }
            await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (process.ExitCode != 0 || !File.Exists(outputPath))
                throw new InvalidDataException($"Audio layout observation is unavailable (exit {process.ExitCode}): {stderr.Result} {stdout.Result}");
            if (new FileInfo(outputPath).Length is < 2 or > 65536)
                throw new InvalidDataException("Audio layout observation exceeds its bounded result format.");
            byte[] bytes = await File.ReadAllBytesAsync(outputPath, cancellationToken).ConfigureAwait(false);
            return ReadProof(bytes, input, wave, provenance);
        }
        finally { if (File.Exists(outputPath)) File.Delete(outputPath); }
    }

    private DecodedSpeakerLayout ReadProof(byte[] bytes, AudioDecodeInput input, PcmWave wave, AudioDecodeProvenance provenance)
    {
        using var json = JsonDocument.Parse(bytes);
        var r = json.RootElement;
        void Need(bool value) { if (!value) throw new InvalidDataException("Incomplete or mismatched whole-stream audio layout proof."); }
        Need(r.GetProperty("proofVersion").GetInt32() == 1 && r.GetProperty("mode").GetString() == "whole-stream-ktac-layout" &&
            r.GetProperty("result").GetString() == "PASS" && r.GetProperty("canonicalLayoutAttested").GetBoolean() &&
            r.GetProperty("failureCode").GetString() == "" && r.GetProperty("detail").GetString() == "");
        var helper = r.GetProperty("helper"); var source = r.GetProperty("input"); var runtime = r.GetProperty("runtime");
        Need(helper.GetProperty("sha256").GetString() == ExecutableSha256 &&
            source.GetProperty("sha256").GetString() == input.DecoderInputSha256 &&
            source.GetProperty("bytes").GetInt64() == input.DecoderBytes.Length);
        Need(runtime.GetProperty("avcodecSha256").GetString() == FixedCodec && runtime.GetProperty("avutilSha256").GetString() == FixedUtility &&
            runtime.GetProperty("avcodecVersion").GetUInt32() == ((59u << 16) | (37u << 8) | 100u) &&
            runtime.GetProperty("avutilVersion").GetUInt32() == ((57u << 16) | (28u << 8) | 100u));
        var p = r.GetProperty("profile"); var o = r.GetProperty("observation");
        Need(p.GetProperty("ktacType").GetInt32() is 0 or 2 && p.GetProperty("sampleRate").GetInt32() == wave.SampleRate &&
            p.GetProperty("channels").GetInt32() == wave.Channels && p.GetProperty("frameSamples").GetInt32() == 1024 &&
            p.GetProperty("audioSpecificConfigHex").GetString() == "11B0");
        long packets = p.GetProperty("declaredPackets").GetInt64(), observed = o.GetProperty("observedFrames").GetInt64();
        long raw = o.GetProperty("rawSamples").GetInt64();
        long delay = p.GetProperty("encoderDelay").GetInt64(), padding = p.GetProperty("endPadding").GetInt64();
        Need(packets > 0 && packets <= 0x3FE00 && o.GetProperty("completedPackets").GetInt64() == packets &&
            observed > 0 && o.GetProperty("acceptedCanonicalFrames").GetInt64() == observed &&
            o.GetProperty("flushSent").GetBoolean() && o.GetProperty("eof").GetBoolean());
        Need(raw == p.GetProperty("headerRawSamples").GetInt64() && raw == checked(packets * 1024) &&
            delay >= 0 && padding >= 0 && delay <= ushort.MaxValue && padding <= ushort.MaxValue &&
            raw - delay - padding == wave.SampleFrames && p.GetProperty("headerPcmFrames").GetInt64() == wave.SampleFrames &&
            p.GetProperty("expectedProductionPcmFrames").GetInt64() == wave.SampleFrames);
        uint mask = 0;
        foreach (string name in new[] { "firstLayout", "lastLayout" })
        {
            var layout = o.GetProperty(name); mask = layout.GetProperty("mask").GetUInt32();
            Need(layout.GetProperty("order").GetInt32() == 1 && layout.GetProperty("channels").GetInt32() == wave.Channels &&
                layout.GetProperty("sampleRate").GetInt32() == wave.SampleRate && mask == 0x3F);
        }
        Need(r.GetProperty("syntheticChecks").GetArrayLength() == 0 && r.GetProperty("waveChannelMaskWasNotChanged").GetBoolean() &&
            r.GetProperty("pcmWasNotWrittenOrReordered").GetBoolean());
        return new(mask, wave, provenance, ExecutablePath, ExecutableSha256, FixedCodec, FixedUtility, FixedFormat, observed);
    }

    private static async Task<string> DrainAsync(StreamReader reader)
    {
        var result = new StringBuilder(); var buffer = new char[4096]; int count;
        while ((count = await reader.ReadAsync(buffer).ConfigureAwait(false)) != 0)
            if (result.Length < 16384) result.Append(buffer, 0, Math.Min(count, 16384 - result.Length));
        return result.ToString();
    }
}
