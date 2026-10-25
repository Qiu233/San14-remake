using System.Collections.Immutable;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Source inputs for the normal event transaction's StagingAudio.
/// This object never plays, stops or acknowledges audio. Runtime application
/// and player-slot knowledge is supplied by its actual lifetime owner.</summary>
public sealed class CampaignNormalEventAudioInputs : IS14AudioEventExecutionState
{
    //TC SActPlayBGM3AF2E0 passes447A0000 twice (1000f). The complete
    //SC3AF790 callback and its actual scalar are cached in
    //.local/event-execution-20261002/current-callbacks-90-92.json.
    //The scalar's original unit is deliberately not interpreted here.
    private const float BgmScalar = 1000f;

    //TC SActPlaySE3AFEA0 reads this immutable96-i32 table. Offline data-only
    //read: TC.rdata RVA13BD070/file13BC270,384B; every value equals the
    //previously read SC table in the callback artifact above. No executable
    //or offline artifact is read by the shipped application.
    private static readonly ImmutableArray<int> SeMap =
    [
        57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68,
        69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80,
        81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92,
        93, 94, 95, 96, 97, 98, 99, 100, 101, 102, 103, 104,
        105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116,
        117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 128,
        129, 130, 131, 132, 133, 134, 135, 136, 137, 138, 139, 140,
        141, 142, 143, 144, 21, 22, 23, 24, 211, 212, 213, 219
    ];
    private readonly Func<int?>? applicationField1AC;
    private readonly Func<int, bool?>? seSlotActive;
    private readonly Func<bool?>? applicationPointerPresent;

    public CampaignNormalEventAudioInputs(string locale, Func<int?>? readApplicationField1AC = null,
        Func<int, bool?>? readSeSlotActive = null, Func<bool?>? readApplicationPointerPresent = null)
    {
        if (locale is not ("TC" or "SC")) throw new InvalidDataException("normal-event.audio.locale");
        applicationField1AC = readApplicationField1AC;
        seSlotActive = readSeSlotActive;
        applicationPointerPresent = readApplicationPointerPresent;
    }

    public float? ReadBgmScalar() => BgmScalar;
    public int? ReadSeMapEntry(int index) => (uint)index < 96 ? SeMap[index]
        : throw new ArgumentOutOfRangeException(nameof(index));

    //The interface's historical World labels refer to TC DAT142785440,
    //the application/environment pointer used by this callback. They must
    //not be inferred from the presence of a campaign GameState instead.
    public bool? ReadWorldPointerPresent() => applicationPointerPresent?.Invoke();
    public int? ReadWorldField1AC() => applicationField1AC?.Invoke();
    public bool? ReadSeSlotActive(int slot) => (uint)slot < 62 ? seSlotActive?.Invoke(slot)
        : throw new ArgumentOutOfRangeException(nameof(slot));

    //CampaignEventExecution.Stage intercepts these methods and persists their
    //ordered semantic commands. Calling the inputs as a player is a misuse.
    public void InvokeBgmManager(int rawId, int flags, float scalar3, float scalar5) => throw NeedsStaging();
    public void InvokeSeManager(int mappedId, float scalar2, float scalar3) => throw NeedsStaging();
    public void StopSeSlot(int slot) => throw NeedsStaging();
    public void StopAllSe() => throw NeedsStaging();
    private static InvalidOperationException NeedsStaging() => new("Normal event audio inputs require the transaction's StagingAudio consumer.");
}
