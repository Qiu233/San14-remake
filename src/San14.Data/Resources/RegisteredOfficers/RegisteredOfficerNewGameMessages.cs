using System.Text;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The original management confirmations: literals and the bound
/// City owner's Force0 P2 name. P2 uses ruler + army suffix, even for a kingdom.</summary>
public static class RegisteredOfficerNewGameMessages
{
    public static string Format(GameResourceRepository repository, string locale, int id,
        RegisteredOfficerNewGameDraft? draft = null, int? cityId = null)
    {
        if (id is < 0x27CB or > 0x27EB) throw new ArgumentOutOfRangeException(nameof(id));
        var record = repository.LoadMessages(locale, MessageKind.Game).Messages.Sections[id / 10000].Records[id % 10000];
        var output = new StringBuilder(); var bytes = record.RawBytes; int at = 0;
        while (at < bytes.Length)
        {
            if (Has(7, 7, 1))
            {
                var literal = record.Literals.Single(value => value.Offset == at + 3);
                output.Append(literal.Text); at += 3 + literal.ByteLength;
                if (!Has(7, 7, 2)) throw Invalid(); at += 3;
            }
            else if (Has(2, 0x50, 0x32))
            {
                if (draft is null || cityId is null) throw Invalid();
                var custom = draft.Selection.NewForces.FirstOrDefault(force => force.CityId == cityId || force.AdditionalCityIds.Contains(cityId.Value));
                string name;
                if (custom is not null) name = draft.Officer(custom.RulerOfficerId).Name;
                else
                {
                    var city = draft.Cities.FirstOrDefault(value => value.Slot == cityId) ?? throw Invalid();
                    if (city.ForceSlot is < 1 or > 45) throw Invalid();
                    name = draft.Scenario.Snapshot.Forces.FirstOrDefault(force => force.Slot == city.ForceSlot)?.RulerName ?? throw Invalid();
                }
                string suffix = repository.LoadMessages(locale, MessageKind.Data).Messages.Sections[1].Records[0x34F].Text ?? throw Invalid();
                output.Append(CityDomesticMessageFormatting.FormatCityNameField(name + suffix)); at += 3;
            }
            else if (Has(5, 5, 5)) { at += 3; if (at != bytes.Length) throw Invalid(); break; }
            else throw Invalid();
        }
        return output.ToString();
        bool Has(byte a, byte b, byte c) => at + 3 <= bytes.Length && bytes[at] == a && bytes[at + 1] == b && bytes[at + 2] == c;
        InvalidDataException Invalid() => new($"registered_officers.new_game.message.{id:X}.at.{at}");
    }
}
