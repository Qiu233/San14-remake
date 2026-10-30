using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    // The normal entry and explicit events share this business source. Only
    // the automatic caller has acknowledgement boundaries between its stages.
    private sealed partial class ForcedEmperorCaptureSource
    {
        internal AutomaticEmperorCaptureAdvance AdvanceAutomatic(
            AutomaticEmperorCaptureProgress progress, AutomaticEmperorCaptureInputs? automaticInputs)
        {
            var context = progress.Context;
            try
            {
                for (;;)
                {
                    int emperor = context.EmperorPhysicalSlot, ruler = context.RulerPhysicalSlot;
                    int owner = context.NewForcePhysicalSlot;
                    switch (progress.Phase)
                    {
                        case AutomaticEmperorCapturePhase.Opening:
                            if (mode == 1)
                            {
                                progress = progress with { Phase = AutomaticEmperorCapturePhase.ReceptionSceneEnter };
                                if (context.NewForceIsPlayer)
                                {
                                    SoundAutomatic(23);
                                    SpeakerAutomatic(emperor, 0x2B11, new(Person0: context.EmperorId, Person1: context.RulerId));
                                }
                            }
                            else
                            {
                                progress = progress with { Phase = AutomaticEmperorCapturePhase.AbolitionNarration };
                                // The player branch is lazy: it never needs37E0.
                                bool showEmperor = context.NewForceIsPlayer;
                                if (!showEmperor)
                                {
                                    var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                                        ?? throw Missing("automatic.current_player_Force");
                                    int playerForce = Need(player.PhysicalSlot, "automatic.current_player_identity");
                                    int playerRuler = Lookup(Need(current.ReadForceRecord(playerForce)?.RawRuler10,
                                        "automatic.current_player_ruler10"));
                                    showEmperor = Tendency(playerRuler) >= Need(automaticInputs?.AbolitionWitnessTendencyMinimum,
                                        "configuration.abolition_witness_tendency_minimum");
                                }
                                if (showEmperor)
                                {
                                    SoundAutomatic(context.NewForceIsPlayer && context.KingdomName == 6 ? 4 : 7);
                                    SpeakerAutomatic(emperor, 0x2AEB, new(Person0: context.EmperorId));
                                }
                            }
                            break;
                        case AutomaticEmperorCapturePhase.ReceptionSceneEnter:
                            SceneAutomatic(45);
                            progress = progress with { Phase = AutomaticEmperorCapturePhase.ReceptionFirstNarration };
                            break;
                        case AutomaticEmperorCapturePhase.ReceptionFirstNarration:
                            NarrationAutomatic(0x2B16, DateOperands(), 8);
                            progress = progress with { Phase = AutomaticEmperorCapturePhase.ReceptionSecondNarration };
                            break;
                        case AutomaticEmperorCapturePhase.ReceptionSecondNarration:
                            NarrationAutomatic(0x2B17, DateOperands());
                            progress = progress with { Phase = AutomaticEmperorCapturePhase.ReceptionSceneExit };
                            break;
                        case AutomaticEmperorCapturePhase.ReceptionSceneExit:
                            SceneAutomatic(null);
                            progress = progress with { Phase = AutomaticEmperorCapturePhase.ReportsAndLoyalty };
                            break;
                        case AutomaticEmperorCapturePhase.AbolitionNarration:
                            NarrationAutomatic(0x2AEC, DateOperands());
                            progress = progress with { Phase = AutomaticEmperorCapturePhase.ReportsAndLoyalty };
                            break;
                        case AutomaticEmperorCapturePhase.ReportsAndLoyalty:
                            // Reports consume their actual source selector/RNG
                            // once, after opening acknowledgements, not at prepare.
                            Reports(emperor, ruler, owner, automatic: true);
                            var people = BuildAutomaticLoyalty(owner, ruler);
                            if (people.Length != 0 && context.NewForceIsPlayer)
                            {
                                people = SortAutomaticLoyalty(people);
                                SoundAutomatic(mode == 1 ? 4 : 6);
                                SpeakerAutomatic(people[0], mode == 1 ? 0x2B19 : 0x2AEE,
                                    new(Person0: Identity(people[0]), Person1: mode == 1 ? context.RulerId :
                                        Need(current.ReadMoraleWaveRuler22EAC0(newForce), "automatic.loyalty_ruler22EAC0")));
                            }
                            progress = progress with { LoyaltyPeople = people,
                                Phase = AutomaticEmperorCapturePhase.ApplyLoyaltyAndPrepareDiplomacy };
                            break;
                        case AutomaticEmperorCapturePhase.ApplyLoyaltyAndPrepareDiplomacy:
                            ApplyAutomaticLoyalty(progress.LoyaltyPeople);
                            var diplomacy = BuildAutomaticDiplomacy(owner, ruler);
                            progress = progress with { LoyaltyPeople = [], UnfavorableForces = diplomacy.Negative,
                                FavorableForces = diplomacy.Positive, Phase = AutomaticEmperorCapturePhase.ApplyDiplomacy };
                            if (diplomacy.Negative.Length != 0)
                            {
                                int otherRuler = Lookup(Need(current.ReadForceRecord(diplomacy.Negative[0])?.RawRuler10,
                                    "automatic.diplomacy_speaker_ruler10"));
                                SpeakerAutomatic(otherRuler, mode == 1 ? 0x2B2A : 0x2B00,
                                    new(Person0: Identity(otherRuler), Person1: context.RulerId));
                            }
                            break;
                        case AutomaticEmperorCapturePhase.ApplyDiplomacy:
                            foreach (int other in progress.UnfavorableForces)
                                ChangeRelation(owner, other, mode == 1 ? -10 : -20);
                            foreach (int other in progress.FavorableForces) ChangeRelation(owner, other, 5);
                            if (mode == 2) WriteEmperor(0);
                            progress = progress with { UnfavorableForces = [], FavorableForces = [],
                                Phase = AutomaticEmperorCapturePhase.Completed };
                            break;
                        case AutomaticEmperorCapturePhase.Completed:
                            return Result();
                        default: throw Missing("automatic.phase");
                    }
                    if (effects.Count != 0) return Result();
                }
            }
            finally
            {
                // Suspension persists the semantic arrays, never borrowed
                // source pointers/handles. The same current's temporary owner
                // is balanced before the enclosing transaction is published.
                for (int i = temporaries.Count - 1; i >= 0; i--)
                {
                    Lock(true);
                    try { current.ClearTemporaryHexList(temporaries[i]); current.ReleaseTemporaryHexList(temporaries[i]); }
                    finally { Lock(false); }
                }
            }
            AutomaticEmperorCaptureAdvance Result() => new(state,
                progress with { NeedsPresentationReceipt = effects.Count != 0, AwaitingEffectBatchSequence = null },
                effects.ToArray(), effects.Count != 0);
            HistoryMessageOperands DateOperands()
            {
                var date = state.NativeMapSeason?.ThirdDate ?? throw Missing("automatic.World34_date");
                return new(Person0: context.RulerId, Person1: context.EmperorId, Integers: [date.Year, date.Month]);
            }
        }

        private ImmutableArray<int> BuildAutomaticLoyalty(int owner, int ruler)
        {
            var people = Temporary();
            foreach (int person in ForcePeople(owner))
            {
                // Status1 rejects lazily before the independent37E4 owner.
                bool admitted = mode == 1
                    ? Need(current.ReadPersonStatus(person), "automatic.loyalty_status") != 1 &&
                        Tendency(person) >= Need(inputs?.LoyalOfficerTendencyMinimum, "configuration.loyal_officer_tendency_minimum")
                    : !Related(person, ruler) && Tendency(person) >= 10 && Identity(person) != Identity(ruler);
                if (admitted) Append(people, RegistryKind.Person, person);
            }
            return Read(people, RegistryKind.Person).Select(entry => entry.Physical).ToImmutableArray();
        }

        private ImmutableArray<int> SortAutomaticLoyalty(ImmutableArray<int> people)
        {
            var ordered = people.ToList();
            // 256A50 is a strict comparator. Use insertion ordering to retain
            // equal payload order, without a second selector or RNG call.
            for (int index = 1; index < ordered.Count; index++)
            {
                int selected = ordered[index], at = index;
                while (at > 0 && Before(selected, ordered[at - 1]))
                { ordered[at] = ordered[at - 1]; at--; }
                ordered[at] = selected;
            }
            return ordered.ToImmutableArray();
            bool Before(int first, int second)
            {
                byte a = Need(OwnedArmyMoraleWaveInputs.ReadPersonPostE7(state, current, first), "automatic.first_PostE7");
                byte b = Need(OwnedArmyMoraleWaveInputs.ReadPersonPostE7(state, current, second), "automatic.second_PostE7");
                if ((a == 0) != (b == 0)) return a != 0;
                if (a != 0)
                {
                    byte firstPriority = Need(OwnedArmyMoraleWaveInputs.ReadPost37(state, a), "automatic.first_Post37");
                    byte secondPriority = Need(OwnedArmyMoraleWaveInputs.ReadPost37(state, b), "automatic.second_Post37");
                    if (firstPriority != secondPriority) return firstPriority < secondPriority;
                }
                int firstAge = PersonCurrentAgeSource.Read(state, current, first);
                int secondAge = PersonCurrentAgeSource.Read(state, current, second);
                return firstAge != secondAge ? firstAge > secondAge : Identity(first) < Identity(second);
            }
        }

        private void ApplyAutomaticLoyalty(ImmutableArray<int> people)
        {
            if (people.IsDefault) throw Missing("automatic.loyalty_list");
            foreach (int person in people)
            {
                byte before = Need(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, person,
                    package._governorInputs), "Person_E8");
                if (mode == 2 ? before > 100 : before >= 100) continue;
                int value = mode == 2 ? Math.Clamp(before - 10, 0, 255)
                    : Math.Clamp(before + Math.Min(100 - before, 10), 0, 255);
                if (Need(OfficerBehaviorRules.PreservesMaximumLoyalty(current.ReadPersonBehavior(person)), "Person_flags15E_bit13")) value = 255;
                current.SetPersonDestructionLoyaltyE8(person, (byte)value);
                state = ArmyDestructionLoyalty.PublishCurrentRawE8(state, current, person, (byte)value);
            }
        }

        private (ImmutableArray<int> Negative, ImmutableArray<int> Positive) BuildAutomaticDiplomacy(int owner, int ruler)
        {
            var candidates = Temporary();
            foreach (int other in ReadAttackRequestForceOrder(current))
            {
                if (other == owner) continue;
                if (oldForce is not null && Need(current.ReadForceNativeValidity(oldForce), "excluded_Force_validity") &&
                    other == Need(oldForce.PhysicalSlot, "excluded_Force_identity")) continue;
                int otherRuler = Lookup(Need(current.ReadForceRecord(other)?.RawRuler10, "candidate_Force_ruler10"));
                if (Tendency(otherRuler) < 10 || mode == 2 && Relation(other, owner) == 0) continue;
                Append(candidates, RegistryKind.Force, other);
            }
            if (mode == 2) return (ShuffleForces(candidates).ToImmutableArray(), []);
            if (Read(candidates, RegistryKind.Force).Count == 0) return ([], []);
            var favorable = Temporary();
            var unfavorable = Temporary();
            foreach (var entry in Read(candidates, RegistryKind.Force))
            {
                int otherRuler = Lookup(Need(current.ReadForceRecord(entry.Physical)?.RawRuler10, "classification_ruler10"));
                bool negative;
                if (Identity(ruler) == 736) negative = true;
                else if (inputs?.UnfavorableRulerTendencyMaximum is int maximum)
                    negative = Tendency(ruler) <= maximum || Dislikes(otherRuler, ruler);
                else if (Dislikes(otherRuler, ruler)) negative = true;
                else throw Missing("configuration.unfavorable_ruler_tendency_maximum");
                Append(negative ? unfavorable : favorable, RegistryKind.Force, entry.Physical);
            }
            return (ShuffleForces(unfavorable).ToImmutableArray(), Read(favorable, RegistryKind.Force)
                .Select(entry => entry.Physical).ToImmutableArray());
        }

        private bool AutomaticPresentationAllowed()
        {
            if (Need(package.ReadApplicationReturnToTitlePending(), "automatic.return_to_title")) return false;
            // The same saved1B0 owner used by CampaignHistoryWriter backs
            // ordinary captures; an explicit current override remains first.
            var blocker = current.ReadReportBlocker() ?? (state.CampaignEventEligibility?.HistoryRecordingSuppressed is bool suppressed
                ? new ReportBlockerState(true, suppressed ? 1 : 0) : null)
                ?? throw Missing("automatic.event_manager1B0_owner");
            return !blocker.Present || Need(blocker.RawField1B0, "automatic.event_manager1B0") == 0;
        }
        private void SoundAutomatic(int cue) => effects.Add(new(null, null) { TalentCommandPresentation =
            new(TalentCommandPresentationOperation.SoundCue, cue, null, null, null, null) });
        private void SceneAutomatic(int? kind)
        {
            if (AutomaticPresentationAllowed()) effects.Add(new(null, null) { MonthlyMaintenance =
                new(kind.HasValue ? MonthlyMaintenanceOperation.EmperorSceneEnter : MonthlyMaintenanceOperation.EmperorSceneExit,
                    kind, null, null) });
        }
        private void SpeakerAutomatic(int person, int root, HistoryMessageOperands operands)
        {
            string text = Format(root, operands);
            if (!AutomaticPresentationAllowed()) return;
            int id = Identity(person);
            var officer = state.Officers.GetValueOrDefault(id) ?? throw Missing("automatic.speaker_owner");
            var speaker = new TalentOfficerPresentationContext(id, Head(officer.CurrentSurname ?? throw Missing("automatic.speaker_surname")) +
                Head(officer.CurrentGivenName ?? throw Missing("automatic.speaker_given_name")), current.ReadPersonPortraitAppearance(person));
            effects.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.OfficerText,
                null, id, null, null, text) { Speaker = speaker } });
        }
        private void NarrationAutomatic(int root, HistoryMessageOperands operands, int? sound = null)
        {
            string text = Format(root, operands);
            if (sound is int cue) SoundAutomatic(cue);
            if (AutomaticPresentationAllowed()) effects.Add(new(null, null) { MonthlyMaintenance =
                new(MonthlyMaintenanceOperation.NarrationText, null, null, text) });
        }
    }
}
