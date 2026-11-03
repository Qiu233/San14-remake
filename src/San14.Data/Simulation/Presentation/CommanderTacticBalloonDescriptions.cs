using System.Collections.ObjectModel;
using San14.Data.Formats;

namespace San14.Data;

public sealed record CommanderTacticBalloonDescription(int Opcode, string Text, int TextPreset);

/// <summary>The compact tactic popup's shared localized descriptions. Startup
/// prepares these same strings in the real layout profiles; current queries
/// select the actual opcodes without retaining a World or a local resource cache.</summary>
public static class CommanderTacticBalloonDescriptions
{
    public static IReadOnlyDictionary<int, CommanderTacticBalloonDescription> Read(
        GameResourceRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var data = repository.LoadMessages(locale, MessageKind.Data).Messages;
        var result = new Dictionary<int, CommanderTacticBalloonDescription>(23);
        for (int opcode = 1; opcode <= 23; opcode++)
            result.Add(opcode, Format(opcode, ReadData));
        return new ReadOnlyDictionary<int, CommanderTacticBalloonDescription>(result);

        string ReadData(int section, int id)
        {
            if ((uint)section >= (uint)data.Sections.Count ||
                (uint)id >= (uint)data.Sections[section].Records.Count ||
                data.Sections[section].Records[id].Text is not string text)
                throw new InvalidDataException("commander-hud.balloon.data_text");
            return text;
        }
    }

    internal static CommanderTacticBalloonDescription Format(int opcode, Func<int, int, string> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var (description, preset) = Description(opcode);
        return new(opcode, data(1, description) + data(0, 0x151 + opcode), preset);
    }

    //One source selector/tag table serves both actual consumers. Resource IDs
    //belong to this compact description boundary, not to the full balloon.
    private static (int Description, int TextPreset) Description(int opcode) => opcode switch
    {
        1 => (0x647, 16),
        2 => (0x64A, 16),
        3 => (0x645, 16),
        4 => (0x646, 16),
        5 => (0x63E, 15),
        6 => (0x63D, 15),
        7 => (0x64E, 15),
        8 => (0x64D, 15),
        9 => (0x64F, 15),
        10 => (0x63E, 16),
        11 => (0x63D, 16),
        12 => (0x64E, 16),
        13 => (0x64D, 16),
        14 => (0x64F, 16),
        15 => (0x644, 15),
        16 => (0x644, 16),
        17 => (0x648, 16),
        18 => (0x64C, 16),
        19 => (0x650, 15),
        20 => (0x643, 15),
        21 => (0x642, 15),
        22 => (0x642, 16),
        23 => (0x651, 16),
        _ => throw new InvalidDataException("commander-hud.balloon.effect_opcode")
    };
}
