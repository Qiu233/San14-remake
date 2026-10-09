
namespace San14.Data;

public sealed record OriginalAudioSelectorQuery(int ReturnValue, int OutValue)
{
    public bool SucceededWithNonzeroValue => ReturnValue != 0 && OutValue != 0;
}

/// <summary>Raw input to the original conditional ruler-definition branch. The field's
/// game meaning is deliberately not assigned before its native definition reader is closed.</summary>
public sealed record OriginalAudioRulerSlotInput(bool UseDefinitionBranch,
    bool RulerObjectIsValid, int? RawDefinitionIndex);
public sealed record OriginalAudioMusicSourceSlot(bool ShouldSubmit, int? SourceSlot,
    OriginalAudioRulerSlotInput Input);

/// <summary>CueHash is the stock identity-map table result. An explicit native mapping
/// entry can change the commit request; use PrepareCommit when that mapping is supplied.</summary>
public sealed record OriginalAudioPreparedMusicSelection(int SourceSlot, sbyte SignedSetting,
    int ConfiguredMusicIndex, int EffectiveMusicIndex, bool UsedSettingSentinel,
    bool UsedQueryFallback, bool CanQueue, uint? CueHash,
    OriginalAudioSelectorQuery FirstQuery, OriginalAudioSelectorQuery? IndexedQuery);
public sealed record OriginalAudioMusicCueMapping(int RawKind, int RawTableRowIndex);
public sealed record OriginalAudioPreparedMusicCommit(bool ShouldCommit, int MusicIndex,
    uint? RequestOpcode, uint? CueHash, OriginalAudioMusicCueMapping? Mapping);

/// <summary>
/// Ports the verified original source-slot, signed-setting and query-result branches as a
/// pure prepared selection. Selection rules and cue IDs are implemented by this project. It does not infer the caller's current scenario, ruler definition,
/// season, custom music remap, playback state, gain or audio mixing.
/// </summary>
public sealed class OriginalAudioMapMusicSelector
{
    public const int SourceSlotCount = 28;
    public const int MusicIndexCount = 117;
    public IReadOnlyList<byte> DefaultMusicIndices { get; }
    public IReadOnlyList<uint> CueHashes { get; }
    private OriginalAudioMapMusicSelector(byte[] defaults, uint[] hashes)
        => (DefaultMusicIndices, CueHashes) = (Array.AsReadOnly(defaults), Array.AsReadOnly(hashes));

    public static OriginalAudioMapMusicSelector Create(string locale)
    {
        ValidateLocale(locale);
        // These are resource cue IDs and selection rules, not audio bytes.
        // The audio payload is resolved from the original sound bank.
        byte[] defaults = Enumerable.Range(0, SourceSlotCount).Select(i => (byte)i).ToArray();
        uint[] cueIds =
        [
            1449521591u, 1478150742u, 1506779893u, 1535409044u, 1564038195u, 1592667346u, 1621296497u, 1649925648u,
            1678554799u, 1421815961u, 1450445112u, 1479074263u, 1507703414u, 1536332565u, 1564961716u, 1593590867u,
            1622220018u, 1650849169u, 1679478320u, 1422739482u, 1451368633u, 1479997784u, 1508626935u, 1537256086u,
            1508181u, 1537972u, 1567763u, 1597554u, 1565885237u, 1594514388u, 1623143539u, 1651772690u,
            1680401841u, 1423663003u, 1452292154u, 1480921305u, 1509550456u, 1538179607u, 1566808758u, 1595437909u,
            1624067060u, 1652696211u, 1681325362u, 1424586524u, 1453215675u, 1481844826u, 1510473977u, 1539103128u,
            1567732279u, 1596361430u, 1624990581u, 1653619732u, 1682248883u, 1425510045u, 1454139196u, 1482768347u,
            1511397498u, 1540026649u, 1568655800u, 1597284951u, 1625914102u, 1654543253u, 1683172404u, 1426433566u,
            1455062717u, 1483691868u, 1512321019u, 1540950170u, 1569579321u, 1598208472u, 1626837623u, 1655466774u,
            1684095925u, 1427357087u, 1455986238u, 1484615389u, 1513244540u, 1541873691u, 1570502842u, 1599131993u,
            1627761144u, 1656390295u, 1685019446u, 1428280608u, 1456909759u, 1485538910u, 1514168061u, 1542797212u,
            1571426363u, 1600055514u, 1628684665u, 1657313816u, 1685942967u, 1429204129u, 1457833280u, 1486462431u,
            1515091582u, 1543720733u, 1572349884u, 1600979035u, 1629608186u, 1658237337u, 1686866488u, 1072319689u,
            1959823370u, 2847327051u, 3734830732u, 327367117u, 1214870798u, 2102374479u, 2989878160u, 3877381841u,
            469918226u, 1100948840u, 1988452521u, 2875956202u, 3763459883u
        ];
        return new(defaults, cueIds);
    }

    /// <summary>TC404B50/SC paired caller: false uses slot 6; invalid ruler suppresses the
    /// submission; valid raw indices 1..6 choose slots 9..14, otherwise slot 6.</summary>
    public static OriginalAudioMusicSourceSlot SelectSourceSlot(OriginalAudioRulerSlotInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.UseDefinitionBranch) return new(true, 6, input);
        if (!input.RulerObjectIsValid) return new(false, null, input);
        if (input.RawDefinitionIndex is not int raw)
            throw new ArgumentException("A valid ruler definition branch needs its explicit raw definition index.", nameof(input));
        return new(true, raw is >= 1 and <= 6 ? raw + 8 : 6, input);
    }

    /// <summary>TC4058B0 uses the original world-month season getter: raw seasons 0..3
    /// select source slots 2..5; an out-of-range raw result retains source slot 2.</summary>
    public static int SelectSeasonSourceSlot(int rawSeasonIndex) => rawSeasonIndex switch
        { 1 => 3, 2 => 4, 3 => 5, _ => 2 };

    /// <summary>The caller supplies the month consumed by TC32EC20/SC32EE80. This does
    /// not choose a scenario's calendar field or infer an active original process date.</summary>
    public static int SelectMonthSourceSlot(int sourceMonth) =>
        SelectSeasonSourceSlot(OriginalMapSeasonMath.SelectMonth(sourceMonth).SeasonIndex);

    /// <summary>
    /// Native TC21E860 takes a signed byte at settings+1C+slot; only -1 is the default
    /// sentinel. TC404890 requires first query return/out both nonzero; indices 28..115
    /// additionally require the indexed query return/out both nonzero. Failure chooses
    /// the source default. TC400F20 rejects indices outside 0..116 without clamping.
    /// </summary>
    public OriginalAudioPreparedMusicSelection PrepareFromSlot(int sourceSlot, sbyte signedSetting,
        OriginalAudioSelectorQuery firstQuery, OriginalAudioSelectorQuery? indexedQuery = null)
    {
        if (sourceSlot is < 0 or >= SourceSlotCount) throw new ArgumentOutOfRangeException(nameof(sourceSlot));
        ArgumentNullException.ThrowIfNull(firstQuery);
        bool sentinel = signedSetting == -1;
        int configured = sentinel ? DefaultMusicIndices[sourceSlot] : signedSetting;
        bool fallback = !firstQuery.SucceededWithNonzeroValue;
        if (!fallback && configured is >= 28 and <= 115)
        {
            if (indexedQuery is null)
                throw new ArgumentException("This source music index requires explicit indexed query results.", nameof(indexedQuery));
            fallback = !indexedQuery.SucceededWithNonzeroValue;
        }
        int effective = fallback ? DefaultMusicIndices[sourceSlot] : configured;
        bool canQueue = effective is >= 0 and < MusicIndexCount;
        return new(sourceSlot, signedSetting, configured, effective, sentinel, fallback, canQueue,
            canQueue ? CueHashes[effective] : null, firstQuery, indexedQuery);
    }

    /// <summary>TC403860 builds the stock map as music-index keys with {kind=0,row=index}.
    /// This returns that verified initialization entry, not a mutable original mapping read.</summary>
    public static OriginalAudioMusicCueMapping StockMappingForIndex(int musicIndex)
    {
        if (musicIndex is < 0 or >= MusicIndexCount) throw new ArgumentOutOfRangeException(nameof(musicIndex));
        return new(0, musicIndex);
    }

    /// <summary>TC404960 commits only a valid pending index. Its explicit mapping entry
    /// selects descriptor+4 (cueHash); absent/nonzero-kind/invalid-row entries retain zero.
    /// Request opcode 62h is prepared without invoking the original proxy or a mixer.</summary>
    public OriginalAudioPreparedMusicCommit PrepareCommit(OriginalAudioPreparedMusicSelection selection,
        OriginalAudioMusicCueMapping? mapping)
    {
        ArgumentNullException.ThrowIfNull(selection);
        int index = selection.EffectiveMusicIndex;
        if (!selection.CanQueue || index is < 0 or >= MusicIndexCount)
            return new(false, index, null, null, mapping);
        uint cueHash = mapping is { RawKind: 0, RawTableRowIndex: >= 0 and < MusicIndexCount }
            ? CueHashes[mapping.RawTableRowIndex] : 0;
        return new(true, index, 0x62, cueHash, mapping);
    }

    private static void ValidateLocale(string locale)
    {
        if (locale is not ("SC" or "TC"))
            throw new ArgumentOutOfRangeException(nameof(locale), locale, "Music selection rules support SC and TC.");
    }
}
