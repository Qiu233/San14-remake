using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class PlayerReturnCaptiveCommandSource
    {
        //The source marks every rejected row, but only the first newly marked
        //row speaks. Rejected rows stay in the pending list for another action.
        private void PrepareRejectedNotices(ArmyReturnCaptiveDisposition choice,
            ImmutableArray<int> rejected, int requestedCount)
        {
            if (rejected.IsEmpty) return;
            if (choice is not (ArmyReturnCaptiveDisposition.Recruit or ArmyReturnCaptiveDisposition.Execute))
                throw PlayerReturnCaptiveMissing("rejected_disposition");
            bool recruit = choice == ArmyReturnCaptiveDisposition.Recruit;
            ushort bit = recruit ? (ushort)0x10 : (ushort)0x1000;
            int? first = null;
            foreach (int id in rejected)
            {
                int person = Person(id);
                ushort flags = current.ReadArmyReturnPersonFlags196(person) ?? throw PlayerReturnCaptiveMissing("rejected_flags");
                if ((flags & bit) == 0)
                {
                    current.WriteArmyReturnPersonFlags196(person, (ushort)(flags | bit));
                    first ??= id;
                }
                var trials = Pending.RecruitmentTrials;
                int index = trials.IndexOf(trials.First(row => row.OfficerId == id));
                if (index < 0) throw PlayerReturnCaptiveMissing("rejected_trial");
                var trial = trials[index];
                State = State with { ArmyReturnCaptiveContinuation = Pending with
                {
                    RecruitmentTrials = trials.SetItem(index, recruit
                        ? trial with { FailedRecruitNoticeShown = true }
                        : trial with { ExecutionProtectionNoticeShown = true })
                } };
            }
            if (first is not int firstId) return;
            Append([new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.SoundCue,
                recruit ? 6 : 8, null, null, null, null) }]);
            if (recruit)
            {
                var refusal = Refusal(firstId);
                if (!string.IsNullOrEmpty(refusal.TextAtSource))
                    Append([new(null, null) { TalentCommandPresentation = refusal }]);
                int index = Pending.RecruitmentTrials.IndexOf(Pending.RecruitmentTrials.First(row => row.OfficerId == firstId));
                State = State with { ArmyReturnCaptiveContinuation = Pending with
                { RecruitmentTrials = Pending.RecruitmentTrials.SetItem(index,
                    Pending.RecruitmentTrials[index] with { AdviceTextAtSource = refusal.TextAtSource }) } };
                if (requestedCount > 1)
                    Append([new(null, null) { TalentCommandPresentation = Message(0x2B94,
                        new(Person0: firstId, Integers: [rejected.Length]), null) }]);
            }
            else
            {
                int ruler = current.ReadForceRecord(Pending.CapturingForceId)?.RawRuler10
                    ?? throw PlayerReturnCaptiveMissing("protection_player_ruler");
                var primary = ForcePrimaryDistrictSource.Resolve(current, new(Pending.CapturingForceId));
                int adviser = package.ReadCaptureAdviserCurrent(State, current, primary).PhysicalSlot
                    ?? throw PlayerReturnCaptiveMissing("protection_adviser");
                int adviserId = current.ReadPersonIdentity(adviser) ?? throw PlayerReturnCaptiveMissing("protection_adviser_identity");
                Append([new(null, null) { TalentCommandPresentation = Message(0x3244,
                    new(Person0: adviserId, Person1: ruler), adviserId) }]);
                if (requestedCount > 1)
                    Append([new(null, null) { TalentCommandPresentation = Message(0x3245,
                        new(Person0: firstId, Integers: [rejected.Length]), null) }]);
            }
        }

        private TalentCommandPresentationRequest Refusal(int id)
        {
            int person = Person(id);
            int force = Pending.CapturingForceId;
            int oldForce = current.ReadPersonOldForceSlot(person) ?? throw PlayerReturnCaptiveMissing("refusal_original_force");
            int ruler = current.ReadForceRecord(force)?.RawRuler10 ?? throw PlayerReturnCaptiveMissing("refusal_player_ruler");
            int oldRuler = current.ReadForceRecord(oldForce)?.RawRuler10 ?? throw PlayerReturnCaptiveMissing("refusal_original_ruler");
            //66B820's invalid source Person/Force/ruler branch is an empty
            //message, rather than a replacement speaker or guessed ruler.
            if (current.ReadPersonNativeValidity(person) != true || current.ReadForceNativeValidity(new(force)) != true ||
                oldRuler is < 1 or > 6000)
                return new(TalentCommandPresentationOperation.OfficerText, null, id, null, null, string.Empty);
            int choice = TacticEffectSourceSamplerMath.Sample(3, 1, id, ruler, oldRuler, 0, Sampler());
            var aftermath = current.ReadOfficerAftermath(person) ?? throw PlayerReturnCaptiveMissing("refusal_self_identity");
            int self = aftermath.Resentment?.TargetOfficerId ?? aftermath.SourceKnowledge?.UnmappedResentmentTarget
                ?? throw PlayerReturnCaptiveMissing("refusal_self162");
            int root = self == id ? choice switch { 0 => 0x2C00, 1 => 0x2C10, _ => 0x2C1F }
                : choice switch { 0 => 0x2BC8, 1 => 0x2BDA, _ => 0x2BED };
            return Message(root, new(Person0: id, Person1: ruler, Person2: oldRuler), id);
        }
    }
}
