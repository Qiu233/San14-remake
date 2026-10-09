using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;

namespace San14.Data;

/// <summary>Finite actual V468/V418/Main post-selection inputs to D11190.
/// These are prepared Camera fields; the latter function's stack1950/1958/
/// 1960 arguments remain the separate preparation-arguments product.</summary>
public readonly record struct DeferredEarlyShadowCurrentMainValues(
    DeferredEarlyShadowCascadeMainFields Cascades, int SignedA00, uint DirectionSelectorA04,
    float DirectionScaleA28, uint TargetFormatA38, uint ModeA3C, uint QualityA88);

/// <summary>A current finite producer result attached to the actual same
/// Main V468/V418 receipt. No complete4260 raw Camera or ShadowCPU is inferred.</summary>
public sealed class DeferredEarlyShadowCurrentMainFields
{
    internal DeferredEarlyShadowCurrentMainFields(SkySeasonMainCameraInputs inputs,
        MapCameraConstructionPacket preparedMain, DeferredEarlyShadowCurrentMainValues values, string evidence)
        => (ActualMainInputs, PreparedMain, Values, SourceEvidence) = (inputs, preparedMain, values, evidence);
    public SkySeasonMainCameraInputs ActualMainInputs { get; }
    public MapCameraConstructionPacket PreparedMain { get; }
    public DeferredEarlyShadowCurrentMainValues Values { get; }
    public string SourceEvidence { get; }

    public DeferredEarlyShadowCascadeNumericFields PrepareOwnCascadeNumericFields()
    {
        RequireActualMain(ActualMainInputs, PreparedMain);
        float far144 = BinaryPrimitives.ReadSingleLittleEndian(PreparedMain.Prefix.Span[0x144..]);
        return LoadedDeferredEarlyShadowCascadeSource.PrepareNumericFields(Values.ModeA3C, Values.Cascades, far144);
    }

    public DeferredEarlyShadowPreparationArguments SelectCurrentPreparationArguments(
        DeferredEarlyShadowContextConstructorFields actualCurrentContext)
    {
        RequireActualMain(ActualMainInputs, PreparedMain);
        return LoadedDeferredEarlyShadowPreparationArgumentsSource.SelectCurrentQuality(actualCurrentContext, Values.QualityA88);
    }

    internal static void RequireActualMain(SkySeasonMainCameraInputs actualInputs,
        MapCameraConstructionPacket actualPreparedMain)
        => (actualPreparedMain.CurrentSeasonPostEffectWriteIdentity ??
            throw new InvalidDataException("Shadow Main fields require the actual successful same-Main V468/V418 writer.")).
            RequireActualMainPreparation(actualInputs, actualPreparedMain);
}

/// <summary>C358A0 directly copies the selected shadow fields to Main.
/// Prepared V418 fields follow those copies; Main post-selection writes take
/// precedence when present. Imported selected values are reused normally.</summary>
public static class DeferredEarlyShadowCurrentMainSource
{
    private readonly record struct Binding(int Prepared, int Ordinal, uint Hash, uint Type);
    private static readonly Binding[] bindings =
    [
        new(0x9EC,21,0xEACC4096,8), new(0x9F4,23,0xDE2CE52E,8),
        new(0x9F8,238,0x7E026E9C,8), new(0x9FC,239,0xD7DDD8DD,8),
        new(0xA00,24,0x0611DBC4,4), new(0xA04,1061,0x2CEF0805,4),
        new(0xA28,129,0x0D8D3389,8), new(0xA2C,743,0x9932C840,8),
        new(0xA38,229,0xA3FB1464,5), new(0xA3C,26,0x96E32C4A,4),
        new(0xA84,578,0x7D733C4E,4), new(0xA88,975,0x3E757721,4)
    ];
    private sealed class ImportedSelectedFields
    {
        internal readonly Dictionary<int, uint> Words;
        internal readonly IReadOnlyDictionary<int, uint> ReadOnlyWords;
        internal ImportedSelectedFields(MapSeasonCameraPackage actualPackage)
        {
            var camera = actualPackage.Camera;
            if (camera.ClassId != MapCameraConstructionSource.PerspectiveCameraClassId)
                throw new InvalidDataException("Shadow selected fields require their actual Perspective Camera source.");
            Words = new(bindings.Length);
            foreach (var binding in bindings)
            {
                var field = camera.Properties[binding.Ordinal];
                if (field.Index != binding.Ordinal || field.NameHash != binding.Hash || field.TypeCode != binding.Type ||
                    field.ElementCount != 1 || field.ElementSize != 4 || field.RawValue.Length != 4)
                    throw new InvalidDataException("Current shadow field differs from its selected loader binding at" + binding.Ordinal);
                Words.Add(binding.Prepared, BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue));
            }
            ReadOnlyWords = new ReadOnlyDictionary<int, uint>(Words);
        }
    }
    private static readonly ConditionalWeakTable<MapSeasonCameraPackage, ImportedSelectedFields> imported = new();

    /// <summary>Load the proved C358A0 selected shadow copies once with this
    /// package. A later actual V418 writer takes precedence over these copies.</summary>
    public static IReadOnlyDictionary<int, uint> LoadSelectedWords(MapSeasonCameraPackage actualPackage)
        => imported.GetValue(actualPackage, static package => new(package)).ReadOnlyWords;

    public static DeferredEarlyShadowCurrentMainFields CaptureActualMainPreparation(
        SkySeasonMainCameraInputs actualInputs, MapCameraConstructionPacket actualPreparedMain)
    {
        ArgumentNullException.ThrowIfNull(actualInputs); ArgumentNullException.ThrowIfNull(actualPreparedMain);
        DeferredEarlyShadowCurrentMainFields.RequireActualMain(actualInputs, actualPreparedMain);
        var selected = actualInputs.Source.Selection;
        if (selected.SecondPackage is not null)
            throw new NotSupportedException("Shadow current Main fields need the actual direct V468 copy; paired interpolation has its own writer.");
        var source = imported.GetValue(selected.FirstPackage, static package => new(package));
        uint Word(int prepared) => actualPreparedMain.CurrentSeasonPostEffectScalars!.TryReadWord(prepared, out uint actualWord)
            ? actualWord : source.Words[prepared];
        float F(int prepared) => BitConverter.UInt32BitsToSingle(Word(prepared));
        var cascades = new DeferredEarlyShadowCascadeMainFields(F(0x9EC), F(0x9F4), F(0x9F8), F(0x9FC),
            F(0xA2C), Word(0xA84));
        var values = new DeferredEarlyShadowCurrentMainValues(cascades, unchecked((int)Word(0xA00)),
            Word(0xA04), F(0xA28), Word(0xA38), Word(0xA3C), Word(0xA88));
        return new(actualInputs, actualPreparedMain, values, selected.FirstPackage.SourceEvidence +
            ";TC:C379D1/C37A05/C37A11/C37A35/C37A41/C37A4D/C37AB9/C37AC5/C37AE9/C37AF5/C37B37/C37B46" +
            ";actual-selected-CPU->Main-direct-V468->prepared-V418;actual-MainPostSelection-tail-precedes-D11190;" +
            actualPreparedMain.CurrentSeasonPostEffectWriteIdentity!.SourceEvidence + ";finite-shadow-fields/no-default-whole-raw-Camera");
    }
}
