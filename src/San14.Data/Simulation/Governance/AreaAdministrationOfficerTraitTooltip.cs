using System.Globalization;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Current trait descriptions for the officer card. Definition text
/// and Data templates are read only when a marker is hovered. No description
/// cache outlives the repository's current resource/override selection.</summary>
internal sealed class AreaAdministrationOfficerTraitTooltip(SourceWorldContext current,
    GameResourceRepository repository, string language)
{
    private MessageDocument? _messages;
    private ResourceStamp _messageStamp;

    internal AreaAdministrationQuery<string?> Read(int lookup)
    {
        try
        {
            if (lookup is < 1 or > 355) return Known(null);
            var trait = current.ReadPersonalityTableReference(lookup) ?? throw Missing("pointer");
            if (!(current.ReadSkillPersonalityValidity(trait) ?? throw Missing("validity"))) return Known(null);
            int physical = current.ReadPersonalityPhysicalSlot(trait) ?? throw Missing("identity");
            if (physical is >= 256 and <= 355)
                return Known(Dynamic(trait, current, Messages()));
            var description = new StringBuilder(51);
            for (int unit = 0; unit < 51; unit++)
            {
                ushort code = current.ReadPersonalityWord(trait, 0x3C + unit * 2) ?? throw Missing("description");
                if (code == 0) return Known(WrapFixed(description.ToString()));
                description.Append((char)code);
            }
            throw Missing("description_terminator");
        }
        catch (InvalidDataException unknown)
        { return AreaAdministrationQuery<string?>.Unknown(unknown.Message); }
    }

    private MessageDocument Messages()
    {
        if (language is not ("TC" or "SC")) throw Missing("language");
        string relative = $"0001_{language}/msgdata_exce.bin";
        string? overridden = repository.OverrideDirectory is string directory
            ? Path.Combine(directory, GameResourceId.File(relative).OverrideRelativePath) : null;
        var file = new FileInfo(overridden is not null && File.Exists(overridden)
            ? overridden : Path.Combine(repository.SourceDirectory, relative));
        if (!file.Exists) throw Missing("message_resource");
        var stamp = new ResourceStamp(file.FullName, file.Length, file.LastWriteTimeUtc);
        if (_messages is not null && _messageStamp == stamp) return _messages;
        var loaded = repository.LoadMessages(language, MessageKind.Data);
        _messages = loaded.Messages; _messageStamp = stamp;
        return _messages;
    }

    private readonly record struct ResourceStamp(string Path, long Length, DateTime LastWriteTimeUtc);

    private static string Dynamic(PersonalityReference trait, SourceWorldContext current, MessageDocument messages)
    {
        byte firstCondition = Byte(0xA4), secondCondition = Byte(0xAC);
        string condition;
        if (secondCondition == 0) condition = Text(1, 0x816) + " ";
        else
        {
            string first = Text(0, 0x16CC + firstCondition);
            string second = firstCondition is not (9 or 10) && secondCondition is 1 or 37
                ? Text(1, 0x81B) : Text(0, 0x16CC + secondCondition);
            condition = Format(Text(1, 0x815), first, second);
        }
        string firstEffect = Effect(Byte(0xB6), Amount(0xB8));
        byte secondCode = Byte(0xBA);
        string secondEffect = secondCode == 0 ? "" : Effect(secondCode, Amount(0xBC));
        return condition + "\n" + firstEffect + (secondEffect.Length == 0 ? "" : "\n" + secondEffect);

        byte Byte(int offset) => current.ReadPersonalityByte(trait, offset) ?? throw Missing("parameter_" + offset.ToString("X"));
        int Amount(int offset) => Math.Clamp((int)unchecked((short)(current.ReadPersonalityWord(trait, offset)
            ?? throw Missing("parameter_" + offset.ToString("X")))), -100, 2000);
        string Text(int section, int index)
        {
            if ((uint)section >= messages.Sections.Count || (uint)index >= messages.Sections[section].Records.Count)
                throw Missing("template_index");
            return messages.Sections[section].Records[index].Text ?? throw Missing("template_text");
        }
        string Effect(byte code, int amount)
        {
            string name = Text(0, 0x164D + code);
            string number = Number(code, amount, Text(1, 0x353));
            return number == Text(1, 0x353) || number.Length == 0 ? name : name + " " + number;
        }
    }

    // The card's multi-line path uses selector3/4; selector1 descriptions and
    // colour outputs have no consumer here. These are effect codes, not IDs
    // of definitions or display-row positions.
    internal static string Number(int effect, int amount, string absent)
    {
        if (effect is 71 or 72 or 109 or 110) return "";
        bool numeric = effect is >= 4 and <= 20 or 26 or 27 or 28 or >= 30 and <= 39 or
            >= 44 and <= 48 or 57 or 62 or 68 or 70 or >= 74 and <= 77 or 79 or 80 or
            82 or 94 or 95 or 97 or 98 or 99 or 103 or 111 or 112 or 113 or 114 or
            116 or 118 or 119 or 120 or 122 or 123;
        return numeric ? (effect == 98 ? -amount : amount).ToString(CultureInfo.InvariantCulture) : absent;
    }

    private static string Format(string format, params string[] arguments)
    {
        var result = new StringBuilder(); int argument = 0;
        for (int index = 0; index < format.Length; index++)
        {
            if (format[index] != '%') { result.Append(format[index]); continue; }
            if (++index == format.Length) throw Missing("template_format");
            if (format[index] == '%') { result.Append('%'); continue; }
            if (format[index] != 's' || argument == arguments.Length) throw Missing("template_format");
            result.Append(arguments[argument++]);
        }
        if (argument != arguments.Length) throw Missing("template_arguments");
        return result.ToString();
    }

    private static readonly HashSet<char> Head = new(new int[] {
        33,37,41,44,46,58,59,63,93,125,162,176,8217,8221,8240,8242,8243,8451,
        12289,12290,12293,12297,12299,12301,12303,12305,12309,12443,12444,12445,
        12446,12526,12533,12534,12540,12541,12542,65281,65285,65289,65292,65294,
        65306,65307,65311,65341,65373,65377,65379,65380,65381,65383,65384,65385,
        65386,65387,65388,65389,65390,65391,65392,65438,65439,65504
    }.Select(value => (char)value));
    private static readonly HashSet<char> Tail = new(new int[] {
        36,40,91,92,123,163,165,8216,8220,12296,12298,12300,12302,12304,12308,
        65284,65288,65339,65371,65378,65505,65509
    }.Select(value => (char)value));

    internal static string WrapFixed(string source)
    {
        var result = new StringBuilder(); int cursor = 0, width = 0;
        while (cursor < source.Length)
        {
            char code = source[cursor++]; result.Append(code);
            width += IsHalf(code) ? 1 : 2;
            if (width < 32) continue;
            bool noRetreat = true;
            for (int adjustment = 0; adjustment < 2; adjustment++)
            {
                bool acceptableHead = cursor == source.Length || !Head.Contains(source[cursor]);
                if (!acceptableHead) { result.Append(source[cursor++]); width++; }
                if (Tail.Contains(source[cursor - 1]))
                { result.Length--; cursor--; width--; noRetreat = false; }
                else if (acceptableHead) break;
            }
            if (cursor != source.Length) result.Append('\n');
            width = noRetreat ? 0 : 32 - width;
        }
        return result.ToString();
    }

    private static bool IsHalf(char code) => unchecked((ushort)(code - 0x20)) < 0x5F ||
        unchecked((ushort)(code + 0x9F)) < 0x3F;
    private static AreaAdministrationQuery<string?> Known(string? value) => AreaAdministrationQuery<string?>.Known(value);
    private static InvalidDataException Missing(string input) => new("area-draft.trait_tooltip_" + input);
}
