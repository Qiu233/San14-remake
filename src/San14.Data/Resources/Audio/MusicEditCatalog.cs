using San14.Data.Formats;

namespace San14.Data;

public sealed record MusicEditGroup(int Index, string Name);
public sealed record MusicEditRow(int Group, int Row, int SourceSlot, string ContextName, int HelpMessageId);
public sealed record MusicEditCandidate(int Index, string Name)
{
    public string Number { get; init; } = "";
}

/// <summary>The ordinary music editor's original directory. Page order and
/// candidate omissions are independent of the playback selector's indices.</summary>
public sealed class MusicEditCatalog
{
    private static readonly int[] RowSlots =
    [2,3,4,5,6,8,23,28,28,28,9,10,11,12,13,14,28,28,28,28,
     7,21,18,20,28,28,28,28,28,28,16,17,19,28,28,28,28,28,28,28];
    private static readonly int[] HelpMessages =
    [0,0,10344,10345,10346,10347,10348,10357,10355,10349,10350,10351,10352,10353,
     10354,0,10358,10360,10361,10362,10363,10359,0,10356,0,0,0,0];
    private static readonly int[] CandidateInput =
    [-1,-1,2,3,4,5,6,7,8,9,10,11,12,13,14,-1,16,17,18,19,20,21,-1,23,-1,-1,-1,-1,-1,
     28,29,30,31,32,33,34,35,36,37,38,39,40,41,42,43,44,45,46,47,48,49,50,51,52,53,54,
     55,56,57,58,59,60,61,62,63,64,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80,81,
     82,83,84,85,86,87,88,89,90,91,92,93,94,95,96,97,98,99,100,101,102,103,104,105,
     106,107,108,109,110,111,112,113,114,115];
    private readonly string[] names;
    private readonly string[] numbers;
    private readonly IReadOnlyList<byte> defaults;
    private readonly HashSet<int> candidates;
    public IReadOnlyList<MusicEditGroup> Groups { get; }
    public IReadOnlyList<MusicEditRow> Rows { get; }
    public IReadOnlyList<MusicEditCandidate> Candidates { get; }

    private MusicEditCatalog(GameResourceRepository repository, string locale)
    {
        var messages = repository.LoadMessages(locale, MessageKind.Data).Messages;
        string Data(int section, int index) => messages.Sections[section].Records[index].Text
            ?? throw new InvalidDataException("Missing original music editor caption.");
        Groups = Array.AsReadOnly(new[] { new MusicEditGroup(0, Data(1, 0x2E0)), new MusicEditGroup(1, Data(1, 0x249)) });
        names = Enumerable.Range(0, AudioMapMusicSelector.MusicIndexCount).Select(index => Data(0, 0x122E + index)).ToArray();
        numbers = Enumerable.Range(0, AudioMapMusicSelector.MusicIndexCount).Select(index => Data(0, 0x12A3 + index)).ToArray();
        defaults = AudioMapMusicSelector.Create(locale).DefaultMusicIndices;
        var rows = new List<MusicEditRow>();
        for (int group = 0; group < 2; group++)
            for (int row = 0; row < 20; row++)
            {
                int slot = RowSlots[row / 2 + (row % 2 + group * 2) * 10];
                if (slot == 28) continue;
                rows.Add(new(group, row, slot, Data(0, 0x1318 + slot), HelpMessages[slot]));
            }
        Rows = rows.AsReadOnly();
        // The CE ordinary eligibility helper returns 1 and writes out=1.
        // This is source behavior, not an inference from installed DLC files.
        Candidates = Array.AsReadOnly(CandidateInput.Where(IsOrdinaryCandidateIndex)
            .Select(index => new MusicEditCandidate(index, names[index]) { Number = numbers[index] }).ToArray());
        candidates = Candidates.Select(row => row.Index).ToHashSet();
    }

    public static MusicEditCatalog Load(GameResourceRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (locale is not ("TC" or "SC")) throw new ArgumentOutOfRangeException(nameof(locale));
        return new(repository, locale);
    }

    public string Name(int musicIndex) => (uint)musicIndex < (uint)names.Length ? names[musicIndex] : "";
    public string Number(int musicIndex) => (uint)musicIndex < (uint)numbers.Length ? numbers[musicIndex] : "";
    public bool CanSelect(int musicIndex) => candidates.Contains(musicIndex);
    public int DefaultIndex(int sourceSlot) => (uint)sourceSlot < 28 ? defaults[sourceSlot]
        : throw new ArgumentOutOfRangeException(nameof(sourceSlot));
    private static bool IsOrdinaryCandidateIndex(int index) => index >= 0;
}
