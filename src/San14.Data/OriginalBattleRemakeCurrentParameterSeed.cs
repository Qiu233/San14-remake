using System.Buffers.Binary;
using System.Globalization;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using San14.Core;

namespace San14.Data;

public sealed record OriginalBattleRemakeCurrentParameterField(int SemanticScRva,
    int PhysicalRva, int Value, string Sha256);
public sealed record OriginalBattleRemakeCurrentLiteral(string Name, int SemanticScRva,
    int PhysicalRva, int Bytes, string Sha256);

/// <summary>A bounded source seed for a newly allocated owned parameter state.
/// Load reads inert PE data only. InstallFresh never restores omitted save fields
/// or replaces any existing current parameter value.</summary>
public sealed class OriginalBattleRemakeCurrentParameterSeed
{
    public string Language { get; }
    public SourceProvenance Source => DamageSource.Source;
    public OriginalBattleDamageSourceParameters DamageSource { get; }
    public OriginalBattleUnitSourceProfile SourceCodeProfile { get; }
    public IReadOnlyList<OriginalBattleRemakeCurrentParameterField> Fields { get; }
    public IReadOnlyList<OriginalBattleRemakeCurrentLiteral> Literals { get; }
    public double CommonPowerExponent { get; }
    public float PercentDenominator { get; }
    public float One { get; }
    public float FlagNumerator { get; }
    public float FlagDenominator { get; }

    private OriginalBattleRemakeCurrentParameterSeed(string language,
        OriginalBattleDamageSourceParameters damage,
        OriginalBattleRemakeCurrentParameterField[] fields,
        OriginalBattleRemakeCurrentLiteral[] literals,
        double power, float percent, float one, float flagNumerator, float flagDenominator)
    {
        Language = language; DamageSource = damage;
        SourceCodeProfile = OriginalBattleUnitSourceCodeSources.ForLanguage(language);
        Fields = Array.AsReadOnly(fields); Literals = Array.AsReadOnly(literals);
        CommonPowerExponent = power; PercentDenominator = percent; One = one;
        FlagNumerator = flagNumerator; FlagDenominator = flagDenominator;
    }

    public static OriginalBattleRemakeCurrentParameterSeed Load(OriginalGameRepository repository,
        string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        //Existing bounded damage reader verifies the complete executable and
        //its twelve source globals. Code profile is evidence, not executed code.
        OriginalBattleDamageSourceParameters damage = OriginalBattleDamageSource.LoadParameters(repository, language);
        using var stream = new FileStream(damage.Source.PhysicalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is < 4096 or > 128 * 1024 * 1024 || stream.Length != damage.Source.Bytes)
            throw new InvalidDataException("Owned battle source exceeds its inspected executable bound.");
        byte[] bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes);
        if (Convert.ToHexString(SHA256.HashData(bytes)) != damage.Source.Sha256)
            throw new InvalidDataException("Owned battle source changed between bounded reads.");
        using var pe = new PEReader(new MemoryStream(bytes, writable: false));
        if (pe.PEHeaders.PEHeader is not { Magic: PEMagic.PE32Plus, ImageBase: 0x140000000UL } ||
            pe.PEHeaders.CoffHeader.Machine != Machine.Amd64)
            throw new InvalidDataException("Owned battle source requires the inspected AMD64 PE32+ ABI.");

        var fields = new List<OriginalBattleRemakeCurrentParameterField>();
        foreach (OriginalBattleDamageSourceField field in damage.Fields)
        {
            int semantic = language == "TC" ? field.Rva + 0x1030 : field.Rva;
            fields.Add(new(semantic, field.Rva, field.Value, field.Sha256));
        }
        foreach (int semantic in MutableScRvas)
        {
            int physical = semantic - (language == "TC" ? 0x1030 : 0);
            ReadOnlySpan<byte> value = Read(physical, 4, ".data");
            fields.Add(new(semantic, physical, BinaryPrimitives.ReadInt32LittleEndian(value),
                Convert.ToHexString(SHA256.HashData(value))));
        }
        if (fields.Select(field => field.SemanticScRva).Distinct().Count() != fields.Count)
            throw new InvalidDataException("Owned battle source seed contains overlapping semantic keys.");

        var literals = new List<OriginalBattleRemakeCurrentLiteral>();
        double power = ReadDouble("commonPowerExponent", 0x1356750, 2.0);
        float percent = ReadSingle("percentDenominator", 0x1344C4C, 100f);
        float one = ReadSingle("one", 0x1344B98, 1f);
        float flagNumerator = ReadSingle("flagNumerator", 0x13B08F0, 8f);
        float flagDenominator = ReadSingle("flagDenominator", 0x1344C40, 10f);
        return new(language, damage, fields.OrderBy(field => field.SemanticScRva).ToArray(),
            literals.ToArray(), power, percent, one, flagNumerator, flagDenominator);

        ReadOnlySpan<byte> Read(int rva, int length, string sectionName)
        {
            var sections = pe.PEHeaders.SectionHeaders.Where(section => section.Name == sectionName &&
                rva >= section.VirtualAddress && (long)rva + length <= (long)section.VirtualAddress + section.SizeOfRawData).ToArray();
            if (sections.Length != 1)
                throw new InvalidDataException("Owned battle source field has no single expected raw PE section.");
            long offset = (long)sections[0].PointerToRawData + rva - sections[0].VirtualAddress;
            if (offset < 0 || offset > bytes.Length - length)
                throw new InvalidDataException("Owned battle source field exceeds executable bytes.");
            return bytes.AsSpan((int)offset, length);
        }
        float ReadSingle(string name, int semantic, float expected)
        {
            int physical = semantic - (language == "TC" ? 0x1000 : 0);
            ReadOnlySpan<byte> raw = Read(physical, 4, ".rdata");
            float value = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(raw));
            if (BitConverter.SingleToInt32Bits(value) != BitConverter.SingleToInt32Bits(expected))
                throw new InvalidDataException("Owned battle source literal differs from its inspected numeric identity.");
            literals.Add(new(name, semantic, physical, 4, Convert.ToHexString(SHA256.HashData(raw))));
            return value;
        }
        double ReadDouble(string name, int semantic, double expected)
        {
            int physical = semantic - (language == "TC" ? 0x1000 : 0);
            ReadOnlySpan<byte> raw = Read(physical, 8, ".rdata");
            double value = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(raw));
            if (BitConverter.DoubleToInt64Bits(value) != BitConverter.DoubleToInt64Bits(expected))
                throw new InvalidDataException("Owned battle source literal differs from its inspected numeric identity.");
            literals.Add(new(name, semantic, physical, 8, Convert.ToHexString(SHA256.HashData(raw))));
            return value;
        }
    }

    /// <summary>Append only before the first update and before Army allocation.
    /// A populated key of either numeric type rejects the entire installation.</summary>
    public GameState InstallFresh(GameState state, string revision)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (string.IsNullOrWhiteSpace(revision) ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } clock ||
            clock.AnchorDate != state.Date || state.Units is null || state.Units.Count != 0 ||
            state.NativeArmySimulation is not { Parameters: { } current } simulation ||
            current.Language != Language || !string.Equals(current.ExecutableSha256, Source.Sha256, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(current.Revision) || current.Int32Values is null || current.Float32Values is null)
            throw new InvalidDataException("Owned battle parameters require a fresh matching empty simulation owner.");
        foreach (OriginalBattleRemakeCurrentParameterField field in Fields)
        {
            string key = OriginalTacticRemakeParameterSeed.Key(field.SemanticScRva);
            if (current.Int32Values.Keys.Concat(current.Float32Values.Keys)
                .Any(existing => string.Equals(existing, key, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Owned battle parameter key already has a current value; installation cannot replace it.");
        }
        var integers = new Dictionary<string, int>(current.Int32Values, current.Int32Values.Comparer);
        foreach (OriginalBattleRemakeCurrentParameterField field in Fields)
            integers.Add(OriginalTacticRemakeParameterSeed.Key(field.SemanticScRva), field.Value);
        string fingerprint = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            string.Join(";", Fields.Select(field => $"{field.SemanticScRva:X8}={field.Value.ToString(CultureInfo.InvariantCulture)}")))));
        var parameters = current with
        {
            Revision = $"{current.Revision}|{revision}:battle-source-seed:{fingerprint}",
            Int32Values = integers,
            Float32Values = new(current.Float32Values, current.Float32Values.Comparer)
        };
        GameState result = state with { NativeArmySimulation = simulation with { Parameters = parameters } };
        NativeSubdayStateValidation.Validate(result);
        return result;
    }

    //Only reached current globals of common factor, field attack and defense,
    //plus their formation-percentage/age subqueries. No runtime-unbacked BSS
    //field is guessed from an implicit zero initialized image.
    private static readonly int[] MutableScRvas =
    [
        0x1A4C674, 0x1A4C680, 0x1A4C68C, 0x1A4C698, 0x1A4C6A4,
        0x1A4C6B0, 0x1A4C6BC, 0x1A4A050, 0x1A4A05C,
        0x1A4C6D4, 0x1A4A068, 0x1A4C668, 0x1A4A074,
        0x1A4A8A0, 0x1A49C78, 0x1A4A900, 0x1A4A8C4, 0x1A4A8D0, 0x1A4A930,
        0x1A49C98, 0x1A4A90C, 0x1A4ABA4, 0x1A4ABB0,
        0x1A49CB0, 0x1A49CBC, 0x1A49CC8, 0x1A49CEC, 0x1A4A918,
        0x1A4A8AC, 0x1A49D04, 0x1A4A924, 0x1A4A8DC, 0x1A4A8E8,
        0x1A4A8B8, 0x1A4ABBC, 0x1A4A93C,
        0x1A4B3D8, 0x1A4C774, 0x1A4C780, 0x1A4C788, 0x1A4C794, 0x1A4C7A0,
        0x1A4C6F8, 0x1A4C704, 0x1A4C710, 0x1A4C71C, 0x1A4C728,
        0x1A4C734, 0x1A4C740, 0x1A4C74C, 0x1A4C754, 0x1A49C34
    ];
}
