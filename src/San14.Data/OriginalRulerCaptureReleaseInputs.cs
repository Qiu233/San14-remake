using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Once-loaded2391C0 signed Person word inputs of2F86F0.
/// Current queries read the campaign's saved fields, including current edits.</summary>
public sealed class OriginalRulerCaptureReleaseInputs
{
    public const string VersionKey = "ce-ruler-capture-release-current:version";
    public static string PersonWordKey(int physical, int tcOffset) => $"ce-ruler-capture-release-current:person:{physical}:{tcOffset:X}";
    private static readonly int[] WordOffsets = [0x16E, 0x176, 0x178, 0x17A];
    private readonly LoadedScenario source;
    private readonly string language, hash;
    private readonly short[][] words;
    private OriginalRulerCaptureReleaseInputs(LoadedScenario scenario, string locale, string executableHash, short[][] values)
        => (source, language, hash, words) = (scenario, locale, executableHash, values);

    public static OriginalRulerCaptureReleaseInputs Load(OriginalGameRepository repository, LoadedScenario source, string language)
    {
        string hash = language switch {
            "SC" => OriginalCeHexWorldCoordinates.VerifiedScExecutableSha256,
            "TC" => OriginalCeHexWorldCoordinates.VerifiedTcExecutableSha256,
            _ => throw new InvalidDataException("Ruler capture inputs require CE SC/TC.") };
        if (source.Metadata.FormatMarker != 0x66 || source.Snapshot.Locale != language || source.Snapshot.Characters.Count != 1851)
            throw new InvalidDataException("Ruler capture inputs require complete CE Person rows.");
        string path = string.IsNullOrWhiteSpace(source.ScenarioSource.PhysicalPath)
            ? Path.Combine(repository.SourceDirectory, source.ScenarioSource.SourcePath) : source.ScenarioSource.PhysicalPath;
        byte[] bytes = File.ReadAllBytes(path);
        if (Convert.ToHexString(SHA256.HashData(bytes)) != source.ScenarioSource.Sha256)
            throw new InvalidDataException("Ruler release scenario changed while loading.");
        var document = S14Reader.Read(bytes, true);
        var table = S14ScenarioTables.ReadCharacterNames(document, language);
        int delta = language == "SC" ? 56 : 0;
        var words = new short[1851][];
        for (int p = 0; p < words.Length; p++) {
            ReadOnlySpan<byte> row = document.DecodedPayload.AsSpan(table.RecordOffset + p * table.RecordStride, table.RecordStride);
            if (BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(150 + delta, 2)) != source.Snapshot.Characters[p].CharacterId)
                throw new InvalidDataException("Ruler release Person serializer identity differs.");
            words[p] = new short[4];
            for (int i = 0; i < 4; i++) words[p][i] = BinaryPrimitives.ReadInt16LittleEndian(row.Slice(276 + delta + WordOffsets[i] - 0x16A, 2));
        }
        return new(source, language, hash, words);
    }

    public GameState InstallFresh(GameState state)
    {
        if (state.NativeArmySimulation is not { } native || native.Parameters.Language != language || native.Parameters.ExecutableSha256 != hash ||
            state.ContentHashes.GetValueOrDefault(source.ScenarioSource.ResourceUri) != source.ScenarioSource.Sha256 ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            native.Parameters.Int32Values.ContainsKey(VersionKey) || source.Snapshot.WorldParameters?.RawField48 == 0x13)
            throw new InvalidDataException("Ruler capture inputs require their unused ordinary fresh owner.");
        var values = new Dictionary<string, int>(native.Parameters.Int32Values) { [VersionKey] = 1 };
        for (int p = 0; p < 1851; p++)
            for (int i = 0; i < 4; i++) values.Add(PersonWordKey(p, WordOffsets[i]), words[p][i]);
        return state with { NativeArmySimulation = native with { Parameters = native.Parameters with { Int32Values = values } } };
    }

    public static short? ReadCurrentWord(GameState state, int physical, int tcOffset) =>
        physical is >= 0 and < 1851 && state.NativeArmySimulation?.Parameters.Int32Values is { } bank && bank.GetValueOrDefault(VersionKey) == 1 &&
        bank.TryGetValue(PersonWordKey(physical, tcOffset), out int word) && word is >= short.MinValue and <= short.MaxValue ? (short)word : null;
}
