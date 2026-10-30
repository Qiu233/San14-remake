using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record CampaignEventCommandImage(string TexturePack, string SpriteName, int Kind, int Index);
public sealed record CampaignEventCommandMarker(int NodeIndex, CampaignEventCommandImage Image);

/// <summary>Resource decoration for the ordinary Events row. These are world
/// occurrence/result flags, independent from Collection's profile unlocks.</summary>
public sealed record CampaignEventCommandRowPresentation(int? DisplayNumber,
    CampaignEventCommandImage? Thumbnail, bool? ShowThumbnailLock,
    IReadOnlyList<CampaignEventCommandMarker> Markers, IReadOnlyList<string> UnresolvedOwners)
{
    public CampaignEventCommandRowProgress? Progress { get; init; }
}

public sealed record CampaignEventCommandPersonSlot(int PortraitNode, int FrameNode, int NameNode);

/// <summary>The mode-zero event list's immutable resource consumer. Call once
/// while publishing the catalog; it does not load resources or evaluate rules.</summary>
public static partial class CampaignEventCommandPresentation
{
    public const byte FreshFilterMask = 0;
    public const string PersonStripLayoutName = "09_01_01_event_list_offset_02";
    public const string PersonFaceLayoutName = "cmn_face_type_04";
    public const int HiddenPersonStripTriggerNode = 19;
    public static IReadOnlyList<CampaignEventCommandPersonSlot> PersonSlots { get; } =
        Array.AsReadOnly(new[] { new CampaignEventCommandPersonSlot(4, 5, 6),
            new(8, 9, 10), new(12, 13, 14), new(16, 17, 18) });

    internal static IReadOnlyDictionary<int, int> ReadDisplayNumbers(S14EventDocument document)
    {
        // The source builds this map before command filtering, including row0.
        // Its CE content-availability gate is the frozen constant-zero leaf.
        var numbers = new Dictionary<int, int>();
        int next = 0;
        foreach (var record in document.Events.OrderBy(record => record.RawField34))
            numbers[record.RawField34] = next++;
        return numbers;
    }

    internal static CampaignEventCommandRowPresentation Read(S14EventRecord record,
        CampaignEventCommandRow row, IReadOnlyDictionary<int, int> numbers, string locale)
    {
        string language = locale switch { "TC" => "tw", "SC" => "ch", _ => throw new ArgumentOutOfRangeException(nameof(locale)) };
        var markers = new List<CampaignEventCommandMarker>();
        var unresolved = new List<string>();
        int? number = record.Id < 900 ? numbers[record.RawField34] : null;
        CampaignEventCommandImage? thumbnail = null;
        bool? thumbnailLock = row.Occurred.HasValue ? !row.Occurred.Value : null;
        if (row.Occurred == true)
        {
            int type = Word(record, 0x18), index = Word(record, 0x1C);
            if (index == 0) index = 1;
            // Ordinary mode0 hides unsupported types; there is no Collection
            // ending fallback image or profile-progress visibility substitute.
            (int Kind, string Suffix)? route = type switch
            { 1 => (15, "bg_"), 2 => (14, "d_"), 3 => (13, ""), 4 => (16, "komono_"), _ => null };
            if (route is { } image)
                thumbnail = new($"texture_stl_event_{image.Suffix}{index:D3}",
                    $"stl_event_{image.Suffix}{index:D3}", image.Kind, index);
        }
        else if (row.Occurred is null) unresolved.Add("campaign_event.presentation.world_occurrence");
        switch (row.Kind)
        { case 1: Add(19, 0x2D6, "000_cmn_mrk_ev_a"); break;
          case 2: Add(20, 0x2D7, "000_cmn_mrk_ev_b"); break;
          case 3: Add(21, 0x2D8, "000_cmn_mrk_ev_c"); break;
          case 4: Add(22, 0x332, "000_cmn_mrk_ev_l", expansion: true); break; }
        if (row.Repeatable && row.Occurred == true) Add(23, 0x2E1, "000_cmn_mrk_finish");
        if (row.Status.HasValue)
        {
            switch ((int)row.Status.Value)
            { case 0: Add(33, 0x2D9, "000_cmn_mrk_ev_d"); break;
              case 1: Add(34, 0x2DA, "000_cmn_mrk_ev_e"); break;
              case 2: Add(35, 0x2DB, "000_cmn_mrk_ev_f"); break;
              case 3: Add(36, 0x2DC, "000_cmn_mrk_ev_g"); break; }
            if (!row.Repeatable && (int)row.Status.Value == 3) Add(27, 0x2E2, "000_cmn_mrk_jikkou");
            if ((int)row.Status.Value == 0)
            {
                if (row.Acknowledged == false) Add(24, 0x2E2, "000_cmn_mrk_jikkou");
                else if (row.Acknowledged is null) unresolved.Add("campaign_event.presentation.world_result");
            }
        }
        else unresolved.Add("campaign_event.presentation.eligibility_status");
        switch (row.TriggerKind)
        { case 2: Add(37, 0x2DD, "000_cmn_mrk_ev_h"); break;
          case 1: Add(38, 0x2DE, "000_cmn_mrk_ev_i"); break; }
        if (row.Repeatable) Add(40, 0x2E0, "000_cmn_mrk_ev_k");
        else Add(39, 0x2DF, "000_cmn_mrk_ev_j");
        return new(number, thumbnail, thumbnailLock, markers.AsReadOnly(), unresolved.AsReadOnly());

        void Add(int node, int index, string name, bool expansion = false) => markers.Add(new(node,
            new($"texture_{(expansion ? "ce_" : "")}lang_{language}", $"{language}_{name}", expansion ? 5 : 3, index)));
    }

    private static int Word(S14EventRecord record, int offset)
        => BinaryPrimitives.ReadInt32LittleEndian(record.RawBytes.AsSpan(offset, 4));
}
