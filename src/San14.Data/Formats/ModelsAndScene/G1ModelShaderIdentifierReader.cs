using System.Globalization;

namespace San14.Data.Formats;

public sealed record G1DrawShaderIdentifier(G1DrawRecord Draw, G1DrawGroup Group,
    uint ShaderIdentifier);
public sealed record G1ModelShaderIdentifiers(G1ModelDrawBindings DrawBindings,
    IReadOnlyList<G1DrawShaderIdentifier> Draws);

/// <summary>The ModelData 563BDEF1 loader installs its original name converter.
/// Its group label becomes the base shader identifier copied into a prepared
/// 0x70 (112-byte) primitive at +0x18. Disk draw +12/+20/+24 are not shader IDs.</summary>
public static class G1ModelShaderIdentifierReader
{
    public static G1ModelShaderIdentifiers Read(G1ModelGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        G1ModelDrawBindings bindings = G1ModelDrawReader.Read(geometry);
        var result = new G1DrawShaderIdentifier[bindings.Draws.Count];
        foreach (G1DrawGroup group in bindings.Groups)
        {
            uint identifier = ConvertModelDataLabel(group.Label);
            foreach (int index in group.DrawIndices)
                result[index] = new(bindings.Draws[index], group, identifier);
        }
        return new(bindings, Array.AsReadOnly(result));
    }

    public static uint ConvertModelDataLabel(string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        if (label.Length == 0)
            throw new InvalidDataException("Original ModelData group label is empty.");
        if (label[0] != '@') return ModelShaderIdentifierHash.Compute(label);
        // This is the verified installed label profile. Native ABD750 uses
        // strtoul(label+1, null, 16); noncanonical labels are rejected explicitly.
        if (label.Length != 9 || label.AsSpan(1).ContainsAnyExcept("0123456789ABCDEFabcdef".AsSpan()) ||
            !uint.TryParse(label.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint value))
            throw new InvalidDataException($"Unsupported original ModelData encoded group label '{label}'.");
        return value;
    }
}
