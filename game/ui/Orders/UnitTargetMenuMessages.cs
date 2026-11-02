using System.Globalization;
using System.Text;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>Original56A9/56C1's finite literal/integer-argument dialect. The
/// immutable resource program is parsed once per page and locale.</summary>
internal sealed class UnitTargetMenuMessages(GameResourceRepository repository, string locale)
{
    //7E5180 reads editor captions through30D050/Data1, but action names
    //through30AE60/Data0. The option's index belongs to that actual bank.
    internal static string Caption(GameResourceRepository repository,string locale,UnitTargetMenuOption option)
    {
        int section=option.ActionKey is "build" or "repair" or "demolish" or "occupy" or "fire" or "capture"?0:1;
        string? value=repository.LoadMessages(locale,MessageKind.Data).Messages.Sections[section]
            .Records[option.CaptionDataIndex].Text;
        return !string.IsNullOrEmpty(value)?value:
            throw new InvalidDataException($"Missing unit command caption: {option.ActionKey}, Data{section}/{option.CaptionDataIndex:X4}.");
    }

    private readonly MessageDocument messages = repository.LoadMessages(locale, MessageKind.Game).Messages;
    private string?[]? fireParts;
    private string?[]? falseReportParts;

    internal static bool AllowsPresentation(UnitTargetMenuOption option) =>
        option.PresentationUnresolvedInput is null || option is
        { ActionKey: "capture", Action: { ActionKey: "capture", Target.Kind: DeploymentTargetKind.Unit },
            TooltipMessageId: 0x56C1, PresentationUnresolvedInput: "unit.target_menu.false_report_probability" };

    internal string FireHint(int successRate)
    {
        var parts = fireParts ??= ReadParts(0x56A9);
        return Format(parts, successRate.ToString(CultureInfo.InvariantCulture));
    }

    internal string FalseReportHint(int? successRate, string unknown)
    {
        var parts = falseReportParts ??= ReadParts(0x56C1);
        return Format(parts, successRate?.ToString(CultureInfo.InvariantCulture) ?? unknown);
    }

    private static string Format(string?[] parts, string argument)
    {
        var text = new StringBuilder();
        foreach (var part in parts)
            text.Append(part ?? argument);
        return text.ToString();
    }

    private string?[] ReadParts(int globalId)
    {
        var record = messages.Sections[globalId / 10000].Records[globalId % 10000];
        var bytes = record.RawBytes;
        var parts = new List<string?>();
        int at = 0;
        while (at < bytes.Length)
        {
            if (Has(7, 7, 1))
            {
                var literal = record.Literals.Single(item => item.Offset == at + 3);
                parts.Add(literal.Text); at += 3 + literal.ByteLength;
                if (!Has(7, 7, 2)) throw Invalid();
                at += 3;
            }
            else if (at + 2 <= bytes.Length && bytes[at] == 2 && bytes[at + 1] == (byte)'2')
            { parts.Add(null); at += 2; }
            else if (Has(5, 5, 5))
            {
                at += 3;
                if (at != bytes.Length || parts is not [not null, null, not null]) throw Invalid();
                return parts.ToArray();
            }
            else throw Invalid();
        }
        throw Invalid();

        bool Has(byte first, byte second, byte third) => at + 3 <= bytes.Length &&
            bytes[at] == first && bytes[at + 1] == second && bytes[at + 2] == third;
        InvalidDataException Invalid() => new($"Unsupported unit target message {globalId} at {at}.");
    }
}
