using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //The hint is a source snapshot for this pending controller, rather than
    //a sampler or text VM execution in a row's pointer/hover callback.
    private GameState PreparePlayerReturnCaptiveAdviceCurrent(GameState state, SourceWorldContext current)
    {
        var saved = state.ArmyReturnCaptiveContinuation ?? throw PlayerReturnCaptiveMissing("advice_owner");
        var trials = saved.RecruitmentTrials;
        int ruler = current.ReadForceRecord(saved.CapturingForceId)?.RawRuler10
            ?? throw PlayerReturnCaptiveMissing("advice_player_ruler");
        foreach (int id in saved.RemainingOfficerIds)
        {
            int person = current.ReadPersonLookupReference(id)?.PhysicalSlot ?? throw PlayerReturnCaptiveMissing("advice_person");
            int oldForce = current.ReadPersonOldForceSlot(person) ?? throw PlayerReturnCaptiveMissing("advice_original_force");
            int oldRuler = current.ReadForceRecord(oldForce)?.RawRuler10 ?? throw PlayerReturnCaptiveMissing("advice_original_ruler");
            string text = string.Empty;
            if (current.ReadPersonNativeValidity(person) == true && current.ReadForceNativeValidity(new(saved.CapturingForceId)) == true &&
                oldRuler is >= 1 and <= 6000)
            {
                ushort flags = current.ReadArmyReturnPersonFlags196(person) ?? throw PlayerReturnCaptiveMissing("advice_flags");
                int sampled = TacticEffectSourceSamplerMath.Sample(3, 1, id, ruler, oldRuler, 0,
                    new(saved.SampleDay, saved.SampleSalt));
                int root;
                if ((flags & 0x10) != 0)
                {
                    var aftermath = current.ReadOfficerAftermath(person) ?? throw PlayerReturnCaptiveMissing("advice_self");
                    int self = aftermath.Resentment?.TargetOfficerId ?? aftermath.SourceKnowledge?.UnmappedResentmentTarget
                        ?? throw PlayerReturnCaptiveMissing("advice_self162");
                    root = self == id ? sampled switch { 0 => 0x2C00, 1 => 0x2C10, _ => 0x2C1F }
                        : sampled switch { 0 => 0x2BC8, 1 => 0x2BDA, _ => 0x2BED };
                }
                else root = sampled switch { 0 => 0x2B3C, 1 => 0x2B4D, _ => 0x2B5F };
                var message = FormatNormalTurnReport(state, current, root, new(Person0: id, Person1: ruler, Person2: oldRuler));
                state = message.State;
                text = message.Text;
            }
            int index = trials.IndexOf(trials.First(row => row.OfficerId == id));
            trials = trials.SetItem(index, trials[index] with { AdviceTextAtSource = text });
        }
        return state with { ArmyReturnCaptiveContinuation = saved with { RecruitmentTrials = trials } };
    }
}
