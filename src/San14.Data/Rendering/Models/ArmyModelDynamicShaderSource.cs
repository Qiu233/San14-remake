using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The two DWORD offsets written to Model90 by C34C97/C34C9A.
/// Raw draw ordinal, shader identifier ordinal and parameter ordinal are
/// different input domains.</summary>
public readonly record struct ModelDynamicShaderDrawRow(uint ShaderOffset0, uint ParameterOffset4);

/// <summary>A present shader Data can expose a null typed record. This remains
/// a successful C243C0 selection; absent Data returns false.</summary>
public sealed record ModelDynamicShaderData<TShaderRecord>(TShaderRecord? TypedRecord)
    where TShaderRecord : class;

public sealed record ModelDynamicShaderSelection<TShaderRecord, TParameterRecord>(
    bool Selected, TShaderRecord? ShaderRecord, TParameterRecord? ParameterRecord)
    where TShaderRecord : class where TParameterRecord : class;

/// <summary>Current fields produced by the actual Model registration/ready
/// owner. C243C0 does not read MaterialC8 or invoke a material callback.</summary>
public sealed record ModelDynamicShaderCurrentFields<TShaderRecord, TParameterRecord>(
    uint SelectorCount1D0, byte? ShaderStride29, ushort? ParameterStride2A,
    IReadOnlyList<ushort>? SelectorRemap88,
    IReadOnlyList<ModelDynamicShaderDrawRow>? DrawRows90,
    IReadOnlyList<ModelDynamicShaderData<TShaderRecord>?>? ShaderData78,
    IReadOnlyList<TParameterRecord?>? ParameterRecords80)
    where TShaderRecord : class where TParameterRecord : class
{
    /// <summary>The actual one-based World2F8 selector and raw draw ordinal.
    /// Failed source gates leave the caller's prior outputs untouched.</summary>
    public ModelDynamicShaderSelection<TShaderRecord, TParameterRecord> Select(
        uint actualWorldSelector2F8, uint actualRawDrawOrdinal)
    {
        if (actualWorldSelector2F8 == 0 || DrawRows90 is null)
            return new(false, null, null);
        uint index = actualWorldSelector2F8 - 1;
        if (SelectorCount1D0 <= index) return new(false, null, null);
        ushort mapped = SelectorRemap88 is null ? unchecked((ushort)index) : SelectorRemap88[checked((int)index)];
        if (mapped == ushort.MaxValue) return new(false, null, null);
        var row = DrawRows90[checked((int)actualRawDrawOrdinal)];
        uint shaderIndex = unchecked((uint)(ShaderStride29!.Value * mapped) + row.ShaderOffset0);
        var data = ShaderData78![checked((int)shaderIndex)];
        if (data is null) return new(false, null, null);
        uint parameterIndex = unchecked((uint)(ParameterStride2A!.Value * mapped) + row.ParameterOffset4);
        return new(true, data.TypedRecord, ParameterRecords80![checked((int)parameterIndex)]);
    }
}

/// <summary>Source proof for the selected Army models' fresh registration.
/// It is a constructor result, not an assertion about a later modified Model.</summary>
public sealed class LoadedArmyModelDynamicShaderSource
{
    internal LoadedArmyModelDynamicShaderSource(LoadedArmyCatalog catalog, DokObject model)
    { Catalog = catalog; Model = model; }
    public LoadedArmyCatalog Catalog { get; }
    public DokObject Model { get; }
    public string SourceEvidence => Catalog.Dictionary.Source.ResourceUri + "@" +
        Catalog.Dictionary.Source.DecodedSha256 + ";Model=" + Model.ObjectId.ToString("X8") +
        ";C1BB40-ordinal10/14/24-empty-25-zero;C16240-clear78/80/88/90;C2B310-nonnull-variant-count-zero";

    /// <summary>After source-created Model constructor and C2B310 registration,
    /// before any later dynamic table writer. No per-request source read.</summary>
    public ModelDynamicShaderCurrentFields<TShaderRecord, TParameterRecord> ConstructFreshRegisteredFields
        <TShaderRecord, TParameterRecord>() where TShaderRecord : class where TParameterRecord : class
        => new(0, null, null, null, null, null, null);
}

public static class ArmyModelDynamicShaderSource
{
    /// <summary>Runs once when the actual Army source is imported. A nonempty
    /// swap declaration needs the real nonempty registration owner.</summary>
    public static LoadedArmyModelDynamicShaderSource ImportFresh(LoadedArmyCatalog actualCatalog,
        int actualCategory)
    {
        ArgumentNullException.ThrowIfNull(actualCatalog);
        if ((uint)actualCategory >= CeArmyModels.KnownSourceCategories.Count)
            throw new ArgumentOutOfRangeException(nameof(actualCategory));
        var category = CeArmyModels.KnownSourceCategories[actualCategory];
        var definition = actualCatalog.Definitions.Single(d => d.Definition.ObjectId == category.DefinitionId);
        var model = definition.ModelObject;
        if (actualCatalog.Dictionary.Source.ResourceUri != GameResourceId.Rdb("KIDSSystemResource", CeArmyModels.DictionaryId).ToString() ||
            model.ObjectId != category.ModelObjectId || model.ClassId != 0xD40B3C8F || model.Kind != DokObjectKind.Inline)
            throw new InvalidDataException("Army dynamic shader source requires its actual original Model declaration.");
        DokProperty Field(int ordinal, uint hash)
        {
            var field = model.Properties[ordinal];
            if (field.Index != ordinal || field.NameHash != hash || field.TypeCode != 5)
                throw new InvalidDataException("Army dynamic shader source field differs from its original loader input.");
            return field;
        }
        foreach (var (ordinal, hash) in new (int, uint)[] { (10, 0x2A444325), (14, 0x1E9DEBBD), (24, 0x07AAF542) })
        {
            var field = Field(ordinal, hash);
            if (field.ElementCount != 0 || field.RawValue.Length != 0)
                throw new NotSupportedException("Nonempty Army shader-swap/collision declarations require their actual registration producer.");
        }
        var common = Field(25, 0x0B534ABC);
        if (common.ElementCount != 1 || common.RawValue.Length != 4 ||
            BinaryPrimitives.ReadUInt32LittleEndian(common.RawValue) != 0)
            throw new NotSupportedException("Army common shader-swap dependency requires its actual nonempty producer.");
        return new(actualCatalog, model);
    }
}
