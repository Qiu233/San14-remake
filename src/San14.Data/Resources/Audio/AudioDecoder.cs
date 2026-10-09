using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using San14.Data.Formats;

namespace San14.Data;

public sealed record AudioDecodeProvenance(string DecoderPath, string DecoderSha256,
    string WaveSha256, string PcmSha256, string EncodedSha256, string ResourceUri)
{
    public IReadOnlyDictionary<string, string> RuntimeFileSha256 { get; init; } = new Dictionary<string, string>();
    public string DecoderInputSha256 { get; init; } = "";
    public SourceProvenance? EncodedSource { get; init; }
    public SourceProvenance? IndexSource { get; init; }
}
public sealed record AudioSampleFormat(int SampleRate, int Channels, long SampleFrames);
/// <summary>Original range identity and selected decoder container identity remain separate
/// when an internal sound is wrapped in a derived single-sample SRSA container.</summary>
public sealed record AudioDecodeInput(byte[] DecoderBytes, string InputExtension, string Codec,
    string DecoderInputSha256, SourceProvenance EncodedSource, AudioSampleFormat? ExpectedFormat,
    long? LoopStartSample = null, long? LoopEndSample = null)
{
    public LoadedAudioStream? StreamSource { get; init; }
    public SourceProvenance? IndexSource { get; init; }
}
public sealed record DecodedAudio(PcmWave Wave, LoadedAudioStream? Source,
    AudioDecodeProvenance Provenance, long? LoopStartSample, long? LoopEndSample)
{
    public AudioDecodeInput? Input { get; init; }
}

public interface IAudioDecoder
{
    Task<DecodedAudio> DecodeAsync(LoadedAudioStream source, CancellationToken cancellationToken = default);
    Task<DecodedAudio> DecodeInputAsync(AudioDecodeInput input, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This audio decoder does not accept internal sample containers.");
}

/// <summary>One selected stream is decoded by a caller-supplied official vgmstream CLI.
/// Input and PCM output are temporary, no original archive is converted or packaged.
/// Source channel count, delay trimming and loop sample bounds remain explicit.
/// This class does not start audio playback.</summary>
public sealed class VgmstreamAudioDecoder : IAudioDecoder
{
    public string ExecutablePath { get; }
    public string ExecutableSha256 { get; }
    public TimeSpan Timeout { get; }
    private readonly string _temporaryRoot;
    private readonly IReadOnlyDictionary<string, string> _runtimeHashes;

    public VgmstreamAudioDecoder(string executablePath, string? expectedExecutableSha256 = null,
        string? temporaryRoot = null, TimeSpan? timeout = null,
        IReadOnlyDictionary<string, string>? runtimeFileSha256 = null)
    {
        if (!OperatingSystem.IsWindows() || System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture !=
            System.Runtime.InteropServices.Architecture.X64)
            throw new PlatformNotSupportedException("Selected original audio runtime requires Windows x64.");
        ExecutablePath = Path.GetFullPath(executablePath);
        ExecutableSha256 = Hash(File.ReadAllBytes(ExecutablePath));
        if (expectedExecutableSha256 is not null &&
            !ExecutableSha256.Equals(expectedExecutableSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Selected audio decoder does not match its pinned SHA256.");
        _temporaryRoot = Path.GetFullPath(temporaryRoot ?? Path.Combine(Path.GetTempPath(), "San14Remake", "audio"));
        Timeout = timeout ?? TimeSpan.FromSeconds(60);
        if (Timeout <= TimeSpan.Zero || Timeout > TimeSpan.FromMinutes(5)) throw new ArgumentOutOfRangeException(nameof(timeout));
        _runtimeHashes = (runtimeFileSha256 ?? new Dictionary<string, string>()).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        VerifyRuntimeFiles();
    }

    /// <summary>Use the retained official release manifest and all executable/DLL hashes.
    /// The caller chooses the manifest path; discovery never downloads a decoder.</summary>
    public static VgmstreamAudioDecoder FromManifest(string manifestPath,
        string? temporaryRoot = null, TimeSpan? timeout = null)
    {
        string fullPath = Path.GetFullPath(manifestPath);
        using JsonDocument json = JsonDocument.Parse(File.ReadAllBytes(fullPath));
        if (json.RootElement.GetProperty("upstream").GetString() != "https://github.com/vgmstream/vgmstream")
            throw new InvalidDataException("Selected audio decoder manifest has no official upstream identity.");
        var hashes = json.RootElement.GetProperty("runtimeFiles").EnumerateArray().ToDictionary(
            row => row.GetProperty("name").GetString()!, row => row.GetProperty("sha256").GetString()!,
            StringComparer.OrdinalIgnoreCase);
        if (!hashes.TryGetValue("vgmstream-cli.exe", out string? executableHash) || hashes.Count < 2)
            throw new InvalidDataException("Audio decoder manifest does not pin its executable and runtime files.");
        return new(Path.Combine(Path.GetDirectoryName(fullPath)!, "vgmstream-cli.exe"), executableHash,
            temporaryRoot, timeout, hashes);
    }

    public Task<DecodedAudio> DecodeAsync(LoadedAudioStream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        if (source.Stream.Codec is not ("KTAC" or "KOVS"))
            throw new NotSupportedException($"Audio codec {source.Stream.Codec} has no decoder contract.");
        if (source.Bytes.Length != source.Stream.EncodedBytes || Hash(source.Bytes) != source.EncodedSha256)
            throw new InvalidDataException("Selected encoded audio bytes changed after the source range was read.");
        KtacInfo? shape = source.Stream.Ktac;
        return DecodeInputAsync(new(source.Bytes, source.Stream.Codec == "KTAC" ? ".ktac" : ".kvs", source.Stream.Codec,
            source.EncodedSha256, source.EncodedSource, shape is null ? null :
                new(shape.SampleRate, shape.Channels, shape.PlayableSamples),
            shape?.HasLoop == true ? shape.LoopStartSample : null, shape?.HasLoop == true ? shape.LoopEndSample : null)
            { StreamSource = source, IndexSource = source.Directory.IndexSource }, cancellationToken);
    }

    public async Task<DecodedAudio> DecodeInputAsync(AudioDecodeInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        bool supported = (input.Codec, input.InputExtension) is ("KTAC", ".ktac") or ("KOVS", ".kvs") or ("SRSA-MSADPCM", ".srsa");
        if (!supported) throw new NotSupportedException($"Audio {input.Codec}/{input.InputExtension} has no selected-container decoder contract.");
        if (input.DecoderBytes.Length is < 4 or > 64 * 1024 * 1024 || Hash(input.DecoderBytes) != input.DecoderInputSha256)
            throw new InvalidDataException("Selected decoder container changed or exceeds the encoded read budget.");
        if (input.ExpectedFormat is { } expected && (expected.SampleRate is < 1 or > 192000 || expected.Channels is < 1 or > 8 ||
            expected.SampleFrames < 1 || expected.SampleFrames > (PcmWaveReader.MaximumWaveBytes - 128L) / (expected.Channels * 2)))
            throw new InvalidDataException("Selected audio's declared PCM shape exceeds the decode budget.");
        if (input.Codec == "SRSA-MSADPCM" && input.ExpectedFormat is null)
            throw new InvalidDataException("Internal sound decode requires its exact original PCM sample shape.");
        if ((input.LoopStartSample is null) != (input.LoopEndSample is null) || input.LoopStartSample < 0 ||
            input.LoopStartSample >= input.LoopEndSample || input.LoopEndSample > input.ExpectedFormat?.SampleFrames)
            throw new InvalidDataException("Selected audio loop interval is inconsistent with its original PCM shape.");
        if (Hash(File.ReadAllBytes(ExecutablePath)) != ExecutableSha256)
            throw new InvalidDataException("Audio decoder changed after its identity was captured.");
        VerifyRuntimeFiles();
        string directory = Path.Combine(_temporaryRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string inputPath = Path.Combine(directory, "selected" + input.InputExtension);
        string outputPath = Path.Combine(directory, "decoded.wav");
        try
        {
            await File.WriteAllBytesAsync(inputPath, input.DecoderBytes, cancellationToken).ConfigureAwait(false);
            var info = new ProcessStartInfo(ExecutablePath)
            {
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = directory,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            info.ArgumentList.Add("-i"); // Ignore looping: produce exactly one original decoded sample range.
            info.ArgumentList.Add("-W"); info.ArgumentList.Add("1"); // Fixed PCM16, preserving all source channels.
            info.ArgumentList.Add("-o"); info.ArgumentList.Add(outputPath); info.ArgumentList.Add(inputPath);
            using Process process = Process.Start(info) ?? throw new IOException("Audio decoder did not start.");
            Task<string> output = DrainBoundedAsync(process.StandardOutput);
            Task<string> error = DrainBoundedAsync(process.StandardError);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Timeout);
            try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) when (process.HasExited) { }
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                await Task.WhenAll(output, error).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"Selected audio decoder exceeded {Timeout.TotalSeconds:0} seconds.");
            }
            await Task.WhenAll(output, error).ConfigureAwait(false);
            if (process.ExitCode != 0 || !File.Exists(outputPath))
                throw new InvalidDataException($"Audio decoder rejected {input.EncodedSource.ResourceUri} (exit {process.ExitCode}): {error.Result} {output.Result}");
            long waveLength = new FileInfo(outputPath).Length;
            if (waveLength is < 12 or > PcmWaveReader.MaximumWaveBytes)
                throw new InvalidDataException("Selected audio decoder output exceeds the PCM read budget.");
            byte[] waveBytes = await File.ReadAllBytesAsync(outputPath, cancellationToken).ConfigureAwait(false);
            PcmWave wave = PcmWaveReader.Read(waveBytes);
            VerifyRuntimeFiles();
            if (input.ExpectedFormat is { } format && (wave.SampleRate != format.SampleRate || wave.Channels != format.Channels ||
                wave.SampleFrames != format.SampleFrames))
                throw new InvalidDataException("Decoded audio rate, channels or trimmed sample count differs from its original header.");
            return new(wave, input.StreamSource, new(ExecutablePath, ExecutableSha256, Hash(waveBytes), Hash(wave.Pcm16),
                input.EncodedSource.Sha256, input.EncodedSource.ResourceUri)
                { RuntimeFileSha256 = _runtimeHashes, DecoderInputSha256 = input.DecoderInputSha256,
                    EncodedSource = input.EncodedSource, IndexSource = input.IndexSource },
                input.LoopStartSample, input.LoopEndSample) { Input = input };
        }
        finally
        {
            // All deletion targets are our fixed filenames in the freshly created private directory.
            if (File.Exists(inputPath)) File.Delete(inputPath);
            if (File.Exists(outputPath)) File.Delete(outputPath);
            Directory.Delete(directory, recursive: false);
        }
    }

    private static async Task<string> DrainBoundedAsync(StreamReader reader)
    {
        var text = new StringBuilder();
        var buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer).ConfigureAwait(false)) != 0)
            if (text.Length < 16384) text.Append(buffer, 0, Math.Min(count, 16384 - text.Length));
        return text.ToString();
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private void VerifyRuntimeFiles()
    {
        foreach (var entry in _runtimeHashes)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || Path.GetFileName(entry.Key) != entry.Key ||
                entry.Key.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidDataException("Audio decoder runtime name must be a leaf file under the tool directory.");
            string path = Path.Combine(Path.GetDirectoryName(ExecutablePath)!, entry.Key);
            if (!Hash(File.ReadAllBytes(path)).Equals(entry.Value, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Audio decoder runtime {entry.Key} differs from its retained manifest.");
        }
    }
}
