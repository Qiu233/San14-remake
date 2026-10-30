using System.Globalization;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record EditedEventContentChoice(int Value, string Caption, int? Kind = null,
    bool Enabled = true, string? UnresolvedOwner = null);

/// <summary>Title-owned content directories. No campaign or synthetic persons
/// are constructed to supply editor choices.</summary>
public sealed class EditedEventContentCatalog
{
    public IReadOnlyList<EditedEventContentChoice> Backgrounds { get; }
    public IReadOnlyList<EditedEventContentChoice> Illustrations { get; }
    public IReadOnlyList<EditedEventContentChoice> Music { get; }
    public IReadOnlyList<EditedEventContentChoice> Sounds { get; }
    public IReadOnlyList<EditedEventContentChoice> People { get; }
    public IReadOnlyList<EditedEventContentChoice> Messages { get; }
    public IReadOnlyList<EditedEventContentChoice> MessageStyles { get; } = [];
    public string? BackgroundsUnresolvedOwner => null;
    public string? IllustrationsUnresolvedOwner => null;
    public string? MusicUnresolvedOwner => null;
    public string? SoundsUnresolvedOwner => null;
    public string? PeopleUnresolvedOwner { get; }
    public string? MessagesUnresolvedOwner => null;
    public string? MessageStylesUnresolvedOwner => "event_editor.content.message_style.authoring_selector";

    public static EditedEventContentCatalog Load(GameResourceRepository repository, string locale,
        HistoricalOfficerEditorCatalog officers, EditedEventLibrary library)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(officers);
        ArgumentNullException.ThrowIfNull(library);
        if (locale is not ("SC" or "TC")) throw new ArgumentOutOfRangeException(nameof(locale));
        if (officers.Locale != locale) throw new InvalidDataException("event_editor.content.person_locale");
        library.Validate();
        return new(repository, locale, officers, library);
    }

    private EditedEventContentCatalog(GameResourceRepository repository, string locale,
        HistoricalOfficerEditorCatalog officers, EditedEventLibrary library)
    {
        var messages = repository.LoadMessages(locale, MessageKind.Data).Messages;
        string Text(int section, int index) => messages.Sections[section].Records[index].Text
            ?? throw new InvalidDataException($"event_editor.content.data.{section}.{index}");
        string none = Text(1, 0x53F);
        EditedEventContentChoice[] Stills(int kind, int caption, int last) =>
            Enumerable.Range(0, last + 1).Select(index => new EditedEventContentChoice(index,
                index == 0 ? none : Text(1, caption) + " " + index.ToString(CultureInfo.InvariantCulture), kind)).ToArray();
        //5FCF20/5FD000 descriptors; 5FE0E0 appends a zero entry for
        //each kind, followed by the inclusive first..last interval.
        Backgrounds = Array.AsReadOnly(Stills(15, 0x305, 51));
        Illustrations = Array.AsReadOnly(Stills(13, 0x307, 81)
            .Concat(Stills(14, 0x306, 18)).Concat(Stills(16, 0x308, 7)).ToArray());
        //The 117-int editor table and admission helper are the same normal
        //directory already used by MusicEditCatalog, with -1 slots omitted.
        var music = MusicEditCatalog.Load(repository, locale);
        Music = Array.AsReadOnly(music.Candidates.Select(row =>
            new EditedEventContentChoice(row.Index, row.Name)).ToArray());
        //CSelectSELayout reads precisely [-2,-3,57..144].  Its callback
        //retains negative sentinels and subtracts 56 from positive choices.
        Sounds = Array.AsReadOnly(new[]
        {
            new EditedEventContentChoice(-2, none),
            new EditedEventContentChoice(-3, Text(1, 0x7B7))
        }.Concat(Enumerable.Range(1, 88).Select(index =>
            new EditedEventContentChoice(index, Text(0, 0x1395 + index - 1)))).ToArray());
        //393B40's direct ranges plus the historical catalog's already
        //admitted profile/DLC-dependent persons. Its unresolved candidates
        //remain separate rather than being assumed present or absent.
        People = Array.AsReadOnly(new[] { new EditedEventContentChoice(0, none) }
            .Concat(officers.Officers.Where(row => ContentPerson(row.Id))
                .Select(row => new EditedEventContentChoice(row.Id, row.Name)))
            .DistinctBy(row => row.Value).ToArray());
        PeopleUnresolvedOwner = officers.UnresolvedCandidates.Count == 0 ? null :
            string.Join("; ", officers.UnresolvedCandidates.Select(row => row.UnresolvedOwner).Distinct());
        Messages = Array.AsReadOnly(library.Messages.OrderBy(row => row.Slot)
            .Select(row => new EditedEventContentChoice(row.Slot, row.Text)).ToArray());
    }

    /// <summary>Only changed selections require membership. Unchanged imported
    /// values and the unexposed message style are retained by the row editor.</summary>
    public void ValidateChange(EditedEventContentValues original, EditedEventContentValues edited)
    {
        ArgumentNullException.ThrowIfNull(original); ArgumentNullException.ThrowIfNull(edited);
        void Choice(int before, int after, IReadOnlyList<EditedEventContentChoice> choices, string field)
        {
            if (before != after && !choices.Any(row => row.Value == after && row.Enabled))
                throw new InvalidDataException("event_editor.content." + field + ".selection");
        }
        Choice(original.BackgroundId, edited.BackgroundId, Backgrounds, "background");
        if ((original.IllustrationId, original.IllustrationKind) != (edited.IllustrationId, edited.IllustrationKind) &&
            !Illustrations.Any(row => row.Value == edited.IllustrationId && row.Kind == edited.IllustrationKind && row.Enabled))
            throw new InvalidDataException("event_editor.content.illustration.selection");
        Choice(original.MusicId, edited.MusicId, Music, "music");
        Choice(original.SoundEffectId, edited.SoundEffectId, Sounds, "sound");
        Choice(original.FirstStandingOfficerId, edited.FirstStandingOfficerId, People, "standing.first");
        Choice(original.SecondStandingOfficerId, edited.SecondStandingOfficerId, People, "standing.second");
        Choice(original.ThirdStandingOfficerId, edited.ThirdStandingOfficerId, People, "standing.third");
        Choice(original.SpeakerId, edited.SpeakerId, People, "speaker");
        Choice(original.MessageSlot, edited.MessageSlot, Messages, "message");
        if (original.MessageStyle != edited.MessageStyle)
            throw new InvalidDataException(MessageStylesUnresolvedOwner);
    }

    private static bool ContentPerson(int id) => id is >= 1 and <= 1000 or >= 3001 and <= 3150
        or >= 2001 and <= 2088;
}
