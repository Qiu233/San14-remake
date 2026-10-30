using System.Globalization;
using System.Text;
using San14.Data;
using San14.Data.Formats;

/// <summary>The portable-officer messages bind only their actual integer0 or
/// string0 argument. Unknown source instructions remain unsupported.</summary>
internal static class RegisteredOfficerExchangeMessages
{
    internal static string Format(GameResourceRepository repository, string locale, int id,
        int? count = null, string? fileName = null)
    {
        var record = repository.LoadMessages(locale, MessageKind.Game).Messages.Sections[id / 10000].Records[id % 10000];
        byte[] bytes = record.RawBytes; int offset = 0; var output = new StringBuilder();
        while (offset < bytes.Length)
        {
            if (Has(7, 7, 1))
            {
                var literal = record.Literals.Single(literal => literal.Offset == offset + 3);
                output.Append(literal.Text); offset += 3 + literal.ByteLength;
                if (!Has(7, 7, 2)) throw Invalid(); offset += 3;
            }
            else if (Has(2, 0x32))
            { output.Append((count ?? throw Invalid()).ToString(CultureInfo.InvariantCulture)); offset += 2; }
            else if (Has(2, 0x3C)) { output.Append(fileName ?? throw Invalid()); offset += 2; }
            else if (Has(5, 5, 5)) { offset += 3; if (offset != bytes.Length) throw Invalid(); break; }
            else throw Invalid();
        }
        return output.ToString();
        bool Has(params byte[] prefix) => offset + prefix.Length <= bytes.Length && bytes.AsSpan(offset, prefix.Length).SequenceEqual(prefix);
        InvalidDataException Invalid() => new($"Unsupported registered-officer exchange message {id:X} at {offset}.");
    }
}
