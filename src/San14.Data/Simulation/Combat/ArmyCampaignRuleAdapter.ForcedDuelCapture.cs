using San14.Core;

namespace San14.Data;

/// <summary>Source-time, unconsumed2E0FC0 announcement. Text formatting,
/// sound/controller admission and playback remain application work. The name
/// operand is retained before custody can change Person/Force ownership.</summary>
public sealed record ForcedDuelCaptureAnnouncement(int OfficerId, string OfficerNameAtSource,
    int MessageRoot, int SoundId)
{
    public string? TextAtSource { get; init; }
    public TalentOfficerPresentationContext? Speaker { get; init; }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal ForcedDuelCaptureAnnouncement? CaptureForcedDuelCaptureAnnouncement(GameState state,
        SourceWorldContext current, int person, ForceReference winningForce, Action<GameState> publish)
    {
        //The forced case passes2E0FC0(...,0,1): its optional2A89 history
        //prefix is absent, but the captured player's announcement is real.
        var oldForce = current.ReadPersonForce(person) ?? throw DuelMissing("forced_capture_old_Force");
        if (!DuelForceIsPlayer(state, current, oldForce)) return null;
        int id = DuelAftermathNeed(current.ReadPersonIdentity(person), "forced_capture_identity");
        string name = state.Officers.GetValueOrDefault(id)?.Name
            ?? throw DuelMissing("forced_capture_source_name");
        ushort flags = DuelAftermathNeed(current.ReadArmyReturnPersonFlags196(person), "forced_capture_flags");
        int root = (flags & 0x4000) == 0 ? 0x2A8C : 0x34AA;
        int sound = DuelForceIsPlayer(state, current, winningForce) ? 8 : 6;
        //2E0FC0 formats Person0 before custody/freedom can change its status,
        //relationships or appearance. Preserve the exact App random draws.
        var formatted = FormatReportMessage(state, current, root, new(Person0: id),
            ReportMessageOrigin.DuelCapture, null);
        var speaker = root == 0x2A8C
            ? new TalentOfficerPresentationContext(id, name,
                DuelAftermathNeed(current.ReadPersonPortraitAppearance(person), "forced_capture_source_appearance"))
            : null;
        publish(formatted.State);
        return new(id, name, root, sound) { TextAtSource = formatted.Text, Speaker = speaker };
    }
}
