using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using San14.Core;

namespace San14.Data;

/// <summary>Fresh owned allocation of the four reached2B5C10 weights.
/// The radius3 Army weight is PE zero-fill storage, not a query default.</summary>
public sealed class OriginalArmyMarchPenaltyParameterSeed
{
    public const string Revision = "san14:ce-current-march-penalty-weights-20261003";
    public const int RadiusOneWeightScRva = 0x1A4AC3C;
    public const int RadiusTwoWeightScRva = 0x1A4AC48;
    public const int RadiusThreeWeightScRva = 0x27D47A4;
    public const int TerritoryWeightScRva = 0x1A4AC5C;
    public string Language { get; }
    public SourceProvenance Source { get; }
    public IReadOnlyList<OriginalBattleRemakeCurrentParameterField> Fields { get; }
    private OriginalArmyMarchPenaltyParameterSeed(string language, SourceProvenance source,
        OriginalBattleRemakeCurrentParameterField[] fields)
    { Language = language; Source = source; Fields = Array.AsReadOnly(fields); }

    public static OriginalArmyMarchPenaltyParameterSeed Load(OriginalGameRepository repository, string language)
    {
        var pinned = OriginalTacticRemakeParameterSeed.LoadPinnedSeed(repository, language);
        byte[] bytes = File.ReadAllBytes(pinned.Source.PhysicalPath);
        if (Convert.ToHexString(SHA256.HashData(bytes)) != pinned.Source.Sha256)
            throw new InvalidDataException("Pinned penalty executable changed during source reads.");
        using var pe = new PEReader(new MemoryStream(bytes, false));
        var fields = new List<OriginalBattleRemakeCurrentParameterField>();
        foreach (int semantic in new[] { RadiusOneWeightScRva, RadiusTwoWeightScRva, TerritoryWeightScRva })
        {
            int physical = semantic - (language == "TC" ? 0x1030 : 0);
            var matches = pe.PEHeaders.SectionHeaders.Where(s => s.Name == ".data" && physical >= s.VirtualAddress &&
                (long)physical + 4 <= (long)s.VirtualAddress + s.SizeOfRawData).ToArray();
            if (matches.Length != 1) throw new InvalidDataException("Penalty weight is outside its pinned .data source.");
            int offset = checked(matches[0].PointerToRawData + physical - matches[0].VirtualAddress);
            if (offset < 0 || offset > bytes.Length - 4) throw new InvalidDataException("Penalty source exceeds image bytes.");
            ReadOnlySpan<byte> raw = bytes.AsSpan(offset, 4);
            fields.Add(new(semantic, physical, BinaryPrimitives.ReadInt32LittleEndian(raw), Convert.ToHexString(SHA256.HashData(raw))));
        }
        int zeroPhysical = language == "TC" ? 0x27D3744 : RadiusThreeWeightScRva;
        var zeroSections = pe.PEHeaders.SectionHeaders.Where(s => zeroPhysical >= (long)s.VirtualAddress + s.SizeOfRawData &&
            (long)zeroPhysical + 4 <= (long)s.VirtualAddress + s.VirtualSize).ToArray();
        if (zeroSections.Length != 1) throw new InvalidDataException("Penalty radius3 storage is not pinned PE loader zero-fill.");
        fields.Add(new(RadiusThreeWeightScRva, zeroPhysical, 0, Convert.ToHexString(SHA256.HashData(new byte[4]))));
        return new(language, pinned.Source, fields.ToArray());
    }

    public GameState InstallFresh(GameState state)
    {
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            state.NativeArmySimulation is not { } simulation || simulation.Parameters.Language != Language ||
            simulation.Parameters.ExecutableSha256 != Source.Sha256 || Fields.Any(f =>
                simulation.Parameters.Int32Values.ContainsKey(OriginalTacticRemakeParameterSeed.Key(f.SemanticScRva)) ||
                simulation.Parameters.Float32Values.ContainsKey(OriginalTacticRemakeParameterSeed.Key(f.SemanticScRva))))
            throw new InvalidDataException("Fresh penalty allocation cannot replace current or omitted saved owners.");
        var integers = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        foreach (var field in Fields) integers.Add(OriginalTacticRemakeParameterSeed.Key(field.SemanticScRva), field.Value);
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = integers } } };
    }
}
