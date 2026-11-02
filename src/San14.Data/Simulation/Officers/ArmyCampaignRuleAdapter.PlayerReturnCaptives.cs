using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

internal sealed record ArmyReturnPlayerCaptivePreparation(ImmutableArray<int> OriginalOfficerIds,
    ImmutableArray<ArmyReturnCaptiveTrial> RecruitmentTrials);

public sealed partial class ArmyCampaignRuleAdapter
{
    //66A980: the pending owner freezes exactly one278900 result per source
    //Person, before any selection or redraw. It never rerolls on resume.
    internal ArmyReturnPlayerCaptivePreparation PreparePlayerReturnCaptiveTrialsCurrent(GameState state,
        SourceWorldContext current, IReadOnlyList<int> physicalOrder, BaseReference target,
        Func<TacticSourceSamplerWorld> readSampler)
    {
        var ids = ImmutableArray.CreateBuilder<int>(physicalOrder.Count);
        var trials = ImmutableArray.CreateBuilder<ArmyReturnCaptiveTrial>(physicalOrder.Count);
        foreach (int person in physicalOrder)
        {
            int force = current.ReadBaseForceSlot(target) ?? throw PlayerReturnCaptiveMissing("trial_Base_Force");
            ushort rulerId = current.ReadForceRecord(force)?.RawRuler10 ?? throw PlayerReturnCaptiveMissing("trial_ruler10");
            int ruler = (rulerId <= 6000 ? current.ReadPersonLookupReference(rulerId) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot
                ?? throw PlayerReturnCaptiveMissing("trial_ruler_lookup");
            int id = current.ReadPersonIdentity(person) ?? throw PlayerReturnCaptiveMissing("trial_Person_identity");
            var trial = TryRecruitCurrent(state, current, person, ruler, -1, readSampler);
            ushort flags = current.ReadArmyReturnPersonFlags196(person) ?? throw PlayerReturnCaptiveMissing("trial_Person_flags");
            current.WriteArmyReturnPersonFlags196(person, (ushort)((flags & 0xFFBF) | (trial.Accepted ? 0x40 : 0)));
            ids.Add(id);
            trials.Add(new(id, trial.Accepted, trial.SampleAfter, trial.Probability, (flags & 0x200) != 0));
        }
        return new(ids.ToImmutable(), trials.ToImmutable());
    }

    //66E910's actual return introduction. The ordinary source queue owns
    //playback. Only its consumed batch can later admit selection.
    internal GameState PreparePlayerReturnCaptiveIntroductionCurrent(GameState state,
        SourceWorldContext current, ArmyReturnCaptiveContinuation pending)
    {
        ushort ruler = current.ReadForceRecord(pending.CapturingForceId)?.RawRuler10
            ?? throw PlayerReturnCaptiveMissing("intro_ruler10");
        var requests = new List<CampaignEffectRequest>
        {
            new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.SoundCue, 8, null, null, null, null) }
        };
        var message = FormatNormalTurnReport(state, current, 0x2B70,
            new(Person0: pending.ReturningCommanderId, Person1: ruler, Person2: pending.OriginalOfficerIds[0]));
        state = message.State;
        if (message.Voices.Count > 1) throw PlayerReturnCaptiveMissing("intro_multiple_voices");
        var speaker = state.Officers[pending.ReturningCommanderId];
        int speakerPhysical = current.ReadPersonLookupReference(pending.ReturningCommanderId)?.PhysicalSlot
            ?? throw PlayerReturnCaptiveMissing("intro_speaker");
        requests.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.OfficerText,
            null, pending.ReturningCommanderId, null, null, message.Text)
        {
            Voice = message.Voices.FirstOrDefault() is { } voice ? SaveVoice(voice) : null,
            Speaker = new(pending.ReturningCommanderId,
                (speaker.CurrentSurname ?? throw PlayerReturnCaptiveMissing("intro_surname")) +
                (speaker.CurrentGivenName ?? throw PlayerReturnCaptiveMissing("intro_given_name")),
                current.ReadPersonPortraitAppearance(speakerPhysical))
        } });
        var outbox = (state.CampaignEffectRequests ?? throw PlayerReturnCaptiveMissing("intro_effect_owner"))
            .Append(CampaignEffectSource.ArmyBattleDefeat, pending.SourceRevision, requests);
        return state with
        {
            CampaignEffectRequests = outbox,
            ArmyReturnCaptiveContinuation = pending with
            {
                Phase = ArmyReturnCaptivePhase.AwaitingIntroduction,
                PendingPresentation = new(pending.BatchId, pending.Round, pending.CapturingForceId, outbox.LastBatchSequence)
            }
        };
    }
}
