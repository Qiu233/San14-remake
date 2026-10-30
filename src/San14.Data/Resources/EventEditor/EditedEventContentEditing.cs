using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed record EditedEventContentValues(int BackgroundId, int IllustrationId,
    int IllustrationKind, int MusicId, int SoundEffectId, int FirstStandingOfficerId,
    int SecondStandingOfficerId, int ThirdStandingOfficerId, int SpeakerId,
    int MessageSlot, int MessageStyle);

/// <summary>A detached edit of one six-statement content row. Unedited operands,
/// instruction controls and imported values are retained exactly.</summary>
public static class EditedEventContentEditing
{
    public static EditedEventContentValues Read(EditedEventContent original)
    {
        ArgumentNullException.ThrowIfNull(original);
        original.Validate();
        var rows = original.Program.Instructions;
        if (rows[0].Code != 85 || rows[1].Code != 410 ||
            rows[2].Code is not (91 or 288 or 411) || rows[3].Code != 92 ||
            rows[4].Code != 107 || rows[5].Code != 385)
            throw new InvalidDataException("event_editor.content.six_statement_binding");
        return new(rows[0].Arguments[0], rows[1].Arguments[0], rows[1].Arguments[1],
            rows[2].Code == 288 ? -3 : rows[2].Arguments[0], rows[3].Arguments[0],
            rows[4].Arguments[0], rows[4].Arguments[1], rows[4].Arguments[2],
            rows[5].Arguments[0], rows[5].Arguments[1], rows[5].Arguments[2]);
    }

    public static EditedEventContent Apply(EditedEventContent original, EditedEventContentValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var before = Read(original);
        if (before == values) return original;
        var rows = original.Program.Instructions.ToBuilder();
        void Set(int row, int operand, int oldValue, int newValue)
        {
            if (oldValue == newValue) return;
            rows[row] = rows[row] with { Arguments = rows[row].Arguments.SetItem(operand, newValue) };
        }
        Set(0, 0, before.BackgroundId, values.BackgroundId);
        Set(1, 0, before.IllustrationId, values.IllustrationId);
        Set(1, 1, before.IllustrationKind, values.IllustrationKind);
        if (before.MusicId != values.MusicId)
        {
            // The source music selector writes a normal BGM statement; its
            // special stop selection writes FadeOutBGM(500 ms) instead.
            rows[2] = rows[2] with
            {
                Code = values.MusicId == -3 ? 288 : 91,
                Arguments = rows[2].Arguments.SetItem(0, values.MusicId == -3 ? 500 : values.MusicId)
            };
        }
        Set(3, 0, before.SoundEffectId, values.SoundEffectId);
        Set(4, 0, before.FirstStandingOfficerId, values.FirstStandingOfficerId);
        Set(4, 1, before.SecondStandingOfficerId, values.SecondStandingOfficerId);
        Set(4, 2, before.ThirdStandingOfficerId, values.ThirdStandingOfficerId);
        Set(5, 0, before.SpeakerId, values.SpeakerId);
        Set(5, 1, before.MessageSlot, values.MessageSlot);
        Set(5, 2, before.MessageStyle, values.MessageStyle);
        return original with { Program = original.Program with { Instructions = rows.ToImmutable() } };
    }
}
