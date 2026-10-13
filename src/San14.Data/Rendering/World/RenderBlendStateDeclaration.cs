using San14.Data.Formats;

namespace San14.Data;

/// <summary>Authored blend bytes and their resource-pool value. This CPU record
/// is independent of device blend objects and material texture-stage parameters.</summary>
public sealed record RenderBlendStateDeclaration(byte Enable, byte SourceColor, byte DestinationColor,
    byte ColorOperation, byte SourceAlpha, byte DestinationAlpha, byte AlphaOperation, byte WriteMask)
{
    public const uint ClassId = 0x78E5679D;
    public ulong PackedValue => Enable == 0 ? (ulong)WriteMask << 56 :
        ((ulong)Enable << 8) | ((ulong)SourceColor << 16) | ((ulong)DestinationColor << 24) |
        ((ulong)ColorOperation << 32) | ((ulong)SourceAlpha << 36) | ((ulong)DestinationAlpha << 44) |
        ((ulong)AlphaOperation << 52) | ((ulong)WriteMask << 56);

    public static RenderBlendStateDeclaration Read(DokObject declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        if (declaration.Kind != DokObjectKind.Inline || declaration.ClassId != ClassId)
            throw new NotSupportedException("Blend-state CPU records require their inline blend declaration.");
        byte Value(uint name)
        {
            var field = declaration.Properties.SingleOrDefault(property => property.NameHash == name);
            if (field is null) return 0;
            if (field.TypeCode != 1 || field.ElementCount != 1 || field.RawValue.Length != 1)
                throw new InvalidDataException($"Blend-state field {name:X8} is not its declared byte.");
            return field.RawValue[0];
        }
        return new(Value(0x214E83EC), Value(0x286E2DED), Value(0xE647DF71), Value(0x7672290E),
            Value(0xD78BB36F), Value(0x1ADB0A2F), Value(0xCEACEB4C), Value(0x3A2F627C));
    }
}
