using System.Text;
using San14.Data;
using San14.Data.Formats;

/// <summary>The finite domestic narration dialect: source literals and the
/// name of Area0, Person0 or the bound City x2 field. Resource bytecode is parsed once, never discarded.</summary>
internal sealed class AdministrationMessages
{
    private enum NameKind { Area, Person, City }
    private sealed record Part(string? Literal, NameKind Kind = NameKind.Area);
    private readonly MessageDocument messages;
    private readonly Dictionary<int, Part[]> programs = [];
    internal AdministrationMessages(GameResourceRepository repository, string locale) =>
        messages = repository.LoadMessages(locale, MessageKind.Game).Messages;

    internal string Format(int globalId, string? areaName = null, string? officerName = null,
        Func<AreaAdministrationQuery<string?>>? cityName = null)
    {
        if (!programs.TryGetValue(globalId, out var program))
        {
            int section = globalId / 10000, index = globalId % 10000;
            var record = messages.Sections[section].Records[index];
            var bytes = record.RawBytes;
            var parts = new List<Part>();
            int at = 0;
            while (at < bytes.Length)
            {
                if (Has(7, 7, 1))
                {
                    var literal = record.Literals.Single(value => value.Offset == at + 3);
                    parts.Add(new(literal.Text)); at += 3 + literal.ByteLength;
                    if (!Has(7, 7, 2)) throw Invalid();
                    at += 3;
                }
                else if (Has(2, 0x96, 0x33)) { parts.Add(new(null)); at += 3; }
                else if (Has(2, 0x46, 0x33)) { parts.Add(new(null, NameKind.Person)); at += 3; }
                else if (Has(2, 0x78, 0x32)) { parts.Add(new(null, NameKind.City)); at += 3; }
                else if (Has(5, 5, 5)) { at += 3; if (at != bytes.Length) throw Invalid(); break; }
                else throw Invalid();
            }
            program = parts.ToArray(); programs.Add(globalId, program);
            bool Has(byte a, byte b, byte c) => at + 3 <= bytes.Length && bytes[at] == a && bytes[at + 1] == b && bytes[at + 2] == c;
            InvalidDataException Invalid() => new($"Unsupported domestic narration resource {globalId} at {at}.");
        }
        var text = new StringBuilder();
        foreach (var part in program)
        {
            if (part.Literal is not null) { text.Append(part.Literal); continue; }
            if (part.Kind == NameKind.City)
            {
                var name = cityName?.Invoke() ?? throw new InvalidDataException($"Missing domestic city argument for {globalId}.");
                if (!name.Available) throw new InvalidDataException(name.UnresolvedInput);
                // Known null is the source raw-argument rejection. A valid
                // empty name still executes all three independent appends.
                if (name.Value is not null) text.Append(CityDomesticMessageFormatting.FormatCityNameField(name.Value));
            }
            else text.Append((part.Kind == NameKind.Person ? officerName : areaName) ??
                throw new InvalidDataException($"Missing domestic narration name argument for {globalId}."));
        }
        return text.ToString();
    }

}
