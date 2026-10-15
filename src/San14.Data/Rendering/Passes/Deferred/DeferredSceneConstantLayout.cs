using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>One imported program's destinations for the restored scene setters.
/// Current values and caller template bytes are never retained by this layout.</summary>
internal sealed class DeferredSceneConstantLayout
{
    private sealed record FieldSchema(string Name, int Offset, ushort Class, ushort Rows,
        ushort Columns, ushort Elements, ushort BaseType = 3)
    {
        internal int Count => Math.Max(1, (int)Elements);
        internal int ElementBytes => Rows * Columns * 4;
        internal int Stride => Elements == 0 ? ElementBytes : 16;
        internal int ByteSize => (Count - 1) * Stride + ElementBytes;
    }

    // The restored PS1 setter ABI. This is field metadata, not current values.
    private static readonly FieldSchema[] Schema =
    [
        new("litDir", 0, 1, 1, 4, 4),
        new("litCol", 64, 1, 1, 4, 4),
        new("litOcc", 128, 1, 1, 4, 1),
        new("ambLit", 144, 1, 1, 4, 0),
        new("nDirLit", 160, 0, 1, 1, 0, 2),
        new("vEye", 176, 1, 1, 4, 0),
        new("mW2P", 592, 2, 4, 4, 0),
        new("mWV", 1824, 1, 1, 4, 3),
        new("mV2W", 2336, 2, 4, 4, 0),
        new("mP2W", 2400, 2, 4, 4, 0),
        new("fog", 2240, 1, 1, 4, 0),
        new("cFog", 2256, 1, 1, 4, 0),
        new("Scat", 2272, 1, 1, 4, 4),
        new("fAmbMapsDORate", 2556, 0, 1, 1, 0),
        new("vAmbDifScl", 2560, 1, 1, 4, 0),
        new("vAmbSpcScl", 2576, 1, 1, 4, 0),
        new("hsRampPrm", 2496, 1, 1, 4, 0),
        new("hsAOPrm", 2512, 1, 1, 4, 0),
        new("hsUVMat", 2528, 1, 1, 3, 2),
        new("vP2T", 2688, 1, 1, 4, 0),
        new("gVfogInfo", 1808, 1, 1, 4, 0),
        new("vCameraPos", 2128, 1, 1, 3, 0),
        new("vCameraDir", 2144, 1, 1, 3, 0),
        new("vCameraViewY", 2160, 1, 1, 3, 0),
        new("vD2Z", 2176, 1, 1, 2, 0),
        new("mRrW2T", 2192, 1, 1, 4, 3),
        new("mP2V", 2608, 2, 4, 4, 0),
        new("vAmbSpcLclOcc", 2676, 1, 1, 2, 0),
        new("vTsbViewPort", 2592, 1, 1, 4, 0),
        new("hFogType", 704, 0, 1, 1, 3, 2),
        new("cHFog", 752, 1, 1, 4, 3),
        new("hFogParam", 800, 1, 1, 4, 3),
        new("hFogFlucPrms", 848, 0, 1, 1, 60),
        new("vOccRate", 192, 1, 1, 2, 0),
        new("vClstZPrm", 656, 1, 1, 4, 0),
        new("nClstPtch", 672, 0, 1, 1, 2, 2),
        new("fClstScl", 692, 0, 1, 1, 0),
        new("vFadeDist", 696, 1, 1, 2, 0),
        new("vLclAmbFogDecayRange", 2480, 1, 1, 2, 0),
        new("vLclAmbDecay", 2468, 1, 1, 2, 0),
        new("fSdmFade", 2464, 0, 1, 1, 0),
        new("cRimTbl", 1872, 1, 1, 4, 16),
        new("nRimExt", 256, 1, 1, 4, 16, 2),
        new("rampType", 200, 0, 1, 1, 0, 2),
        new("rampTexHeight", 512, 0, 1, 1, 0, 2),
        new("rampPkPrms", 208, 1, 1, 4, 3),
        new("rampRimPkPrms", 560, 1, 1, 4, 2),
        new("rampSecondaryRimPkPrms", 528, 1, 1, 4, 2),
        new("rampRimThres", 2672, 0, 1, 1, 0)
    ];

    private sealed record Field(FieldSchema Schema, DxbcRdefVariable? Target, string? Error);
    private readonly Dictionary<string, Field> fields;

    private DeferredSceneConstantLayout(DxbcRdefBuffer globals, Dictionary<string, Field> fields)
        => (Globals, this.fields) = (globals, fields);

    internal DxbcRdefBuffer Globals { get; }

    internal static DeferredSceneConstantLayout Canonical(DxbcResourceDefinitions definitions)
        => Create(definitions, definitions);

    internal static DeferredSceneConstantLayout Create(DxbcResourceDefinitions canonical,
        DxbcResourceDefinitions target, bool allowAdditional = false)
    {
        ArgumentNullException.ThrowIfNull(canonical); ArgumentNullException.ThrowIfNull(target);
        var canonicalBuffers = canonical.Buffers.Where(b => b.Name.Text == "$Globals").ToArray();
        var canonicalBindings = canonical.Bindings.Where(b => b.Name.Text == "$Globals" && b.InputType == 0).ToArray();
        if (canonicalBuffers.Length != 1 || canonicalBuffers[0].ByteSize != DeferredSceneConstants.ByteSize ||
            canonicalBuffers[0].Variables.Count != 49 || canonicalBindings.Length != 1 ||
            canonicalBindings[0].BindPoint != 0 || canonicalBindings[0].BindCount != 1)
            throw new InvalidDataException("Scene writer requires the original PS1 cb0 layout and complete 2704-byte template.");
        var originalGlobals = canonicalBuffers[0];
        bool isCanonical = ReferenceEquals(canonical, target);
        var targetBuffers = target.Buffers.Where(b => b.Name.Text == "$Globals").ToArray();
        var targetBindings = target.Bindings.Where(b => b.Name.Text == "$Globals" && b.InputType == 0).ToArray();
        if (targetBuffers.Length != 1 || targetBindings.Length != 1 ||
            targetBindings[0].BindPoint != 0 || targetBindings[0].BindCount != 1 || targetBuffers[0].ByteSize < 0 ||
            (!isCanonical && targetBuffers[0].Variables.Select(v => v.Name.Text).Distinct(StringComparer.Ordinal).Count() != targetBuffers[0].Variables.Count))
            throw new InvalidDataException("Selected scene writer requires the complete exact original cb0 extent and unique named fields.");
        var globals = targetBuffers[0];
        if (!isCanonical)
        {
            foreach (var variable in globals.Variables)
            {
                var originals = originalGlobals.Variables.Where(v => v.Name.Text == variable.Name.Text).ToArray();
                if (allowAdditional && originals.Length == 0) continue;
                if (originals.Length != 1 || originals[0].ByteSize != variable.ByteSize ||
                    variable.StartOffset < 0 || (long)variable.StartOffset + variable.ByteSize > globals.ByteSize ||
                    !SameType(canonical, originals[0], target, variable))
                    throw new InvalidDataException("Selected named field differs from the restored native semantic shape: " + variable.Name.Text);
            }
        }
        var fields = new Dictionary<string, Field>(StringComparer.Ordinal);
        foreach (var schema in Schema)
        {
            var originals = originalGlobals.Variables.Where(v => v.Name.Text == schema.Name).ToArray();
            string? error = CanonicalError(canonical, schema, originals);
            var destinations = globals.Variables.Where(v => v.Name.Text == schema.Name).ToArray();
            fields.Add(schema.Name, new(schema, destinations.Length == 1 ? destinations[0] : null, error));
        }
        return new(globals, fields);
    }

    internal void RequireTemplate(int length)
    {
        if (length != Globals.ByteSize)
            throw new InvalidDataException("Scene writer requires the complete exact current cb0 template.");
    }

    internal void Write(string name, uint[] words, byte[] result, List<DeferredSceneWrite> writes,
        bool allowHatchingIeee = false)
    {
        var field = fields[name];
        if (field.Error is { } error) throw new InvalidDataException(error);
        var schema = field.Schema;
        bool hatchingIeee = allowHatchingIeee && name is "hsRampPrm" or "hsAOPrm" or "hsUVMat";
        if ((long)words.Length * 4 != schema.Count * schema.ElementBytes ||
            (schema.BaseType == 3 && !hatchingIeee && words.Any(w => !float.IsFinite(BitConverter.UInt32BitsToSingle(w)))))
            throw new InvalidDataException("Unsupported scene layout or nonfinite value: " + name);
        // Absent declarations still consume and validate their original inputs.
        if (field.Target is not { } target) return;
        for (int element = 0; element < schema.Count; element++)
            for (int word = 0; word < schema.ElementBytes / 4; word++)
                BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(target.StartOffset + element * schema.Stride + word * 4, 4),
                    words[element * schema.ElementBytes / 4 + word]);
        writes.Add(new(name, target.StartOffset, schema.Count, schema.Stride, schema.ElementBytes));
    }

    internal IReadOnlyList<string> RetainedVariables(IEnumerable<DeferredSceneWrite> writes)
    {
        var names = writes.Select(w => w.Name).ToHashSet(StringComparer.Ordinal);
        return Array.AsReadOnly(Globals.Variables.Where(v => !names.Contains(v.Name.Text)).Select(v => v.Name.Text).ToArray());
    }

    private static string? CanonicalError(DxbcResourceDefinitions definitions, FieldSchema schema,
        DxbcRdefVariable[] variables)
    {
        if (variables.Length != 1) return "Missing or duplicate scene variable: " + schema.Name;
        var variable = variables[0];
        var types = definitions.Types.Where(t => t.Offset == variable.TypeOffset).ToArray();
        if (types.Length != 1) return "Missing or duplicate scene type: " + schema.Name;
        var type = types[0];
        return variable.StartOffset != schema.Offset || variable.ByteSize != schema.ByteSize ||
            type.Class != schema.Class || type.BaseType != schema.BaseType || type.Rows != schema.Rows ||
            type.Columns != schema.Columns || type.Elements != schema.Elements || type.Members.Count != 0 ||
            schema.Offset < 0 || (long)schema.Offset + variable.ByteSize > DeferredSceneConstants.ByteSize
            ? "Unsupported scene layout or nonfinite value: " + schema.Name : null;
    }

    private static bool SameType(DxbcResourceDefinitions originalDefinitions, DxbcRdefVariable original,
        DxbcResourceDefinitions targetDefinitions, DxbcRdefVariable target)
    {
        var originals = originalDefinitions.Types.Where(t => t.Offset == original.TypeOffset).ToArray();
        var targets = targetDefinitions.Types.Where(t => t.Offset == target.TypeOffset).ToArray();
        if (originals.Length != 1 || targets.Length != 1) return false;
        var a = originals[0]; var b = targets[0];
        return a.Class == b.Class && a.BaseType == b.BaseType && a.Rows == b.Rows &&
            a.Columns == b.Columns && a.Elements == b.Elements && a.Members.Count == 0 && b.Members.Count == 0;
    }
}
