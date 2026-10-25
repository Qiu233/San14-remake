using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //293BD0 calls277590, then274DD0, before clearing the retiring Force's
    //leader bit. Both callbacks read the latest interaction, not two cached
    //decisions made before the first callback changed it.
    internal GameState ApplyForceUnregisterCoalitionCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, int victim, int captor, List<CampaignEffectRequest> effects)
        => new ForceRetirementCoalitionOperation(this, state, current, pool, effects).Run(victim, captor);

    private sealed class ForceRetirementCoalitionOperation(ArmyCampaignRuleAdapter package,
        GameState state, SourceWorldContext current, WorldIndexContext pool, List<CampaignEffectRequest> effects)
    {
        internal GameState Run(int victim, int captor)
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
                throw Missing("shared_pool");
            if (Other() == victim)
            {
                var target = Force(Selected());
                if (Valid(target))
                {
                    int targetId = Ordinal(target);
                    History(0x3202, new(Force0: targetId),
                        0x3203, new(Force0: victim, Force1: targetId));
                    Sound8();
                    Announcement(0x3204, new(Force0: victim, Force1: targetId));
                }
                Dissolve();
            }
            //277590 may already have cleared both bytes. This fresh read
            //also preserves its source behavior when leader==target.
            if (Selected() == victim)
            {
                var winner = Force(captor);
                if (Valid(winner) && (Flags(winner) & 2) != 0)
                    CompleteCoalitionVictory(winner);
                else
                {
                    var target = Force(Selected());
                    if (Valid(target))
                    {
                        int targetId = Ordinal(target);
                        History(0x3205, new(Force0: targetId), 0x3206, new(Force0: targetId));
                        Sound8();
                        Announcement(0x3207, new(Force0: targetId));
                    }
                    Dissolve();
                }
            }
            return state;
        }

        //257270 is entered only for an actual winning coalition member.
        //Its initial target/leader validity gates return without dissolving
        //when either live pointer is invalid.
        private void CompleteCoalitionVictory(ForceReference winner)
        {
            if (!Valid(winner)) return;
            var target = Force(Selected());
            if (!Valid(target)) return;
            var leader = Force(Other());
            if (!Valid(leader)) return;
            int targetId = Ordinal(target);
            var operands = new HistoryMessageOperands(Force0: targetId);
            History(0x3213, operands, 0x3214, operands);
            bool player = package.TalentForceIsPlayerCurrent(state, current, leader);
            if (player && (Flags(leader) & 4) != 0) Speech(leader, 0x3215, operands);
            if (!player && (Flags(leader) & 4) != 0) Speech(leader, 0x321A, operands);
            ApplyHistoricalVictoryFlag();

            //The C0 source order is retained, including repeated pointers.
            //Only pairs where both currently have bit1 receive2704F0.
            var members = Forces().ToArray();
            for (int first = 0; first < members.Length; first++)
            {
                var a = members[first];
                if ((Flags(a) & 2) == 0) continue;
                for (int second = first + 1; second < members.Length; second++)
                {
                    var b = members[second];
                    if ((Flags(b) & 2) == 0) continue;
                    int other = Ordinal(b);
                    int before = other < 52 ? DRelations(state, Physical(a))[other] : 0;
                    var left = PersonReturnPathSource.ReadForceAdvantage(state, current, a, 8, defaultValue: 100);
                    int leftValue = left.Value ?? throw Missing(left.UnresolvedOwner ?? "first_effect8");
                    var right = PersonReturnPathSource.ReadForceAdvantage(state, current, b, 8, defaultValue: 100);
                    int rightValue = right.Value ?? throw Missing(right.UnresolvedOwner ?? "second_effect8");
                    int magnitude = left.QualifiedStates.Length != 0 ? leftValue : rightValue;
                    //Cached file-backed TC141A49984 is signed20. A saved
                    //current value takes precedence over that static source.
                    int adjustment = StrategicParameter(state, "coalition-victory-relation-adjustment", 20);
                    int after = Math.Clamp(unchecked(unchecked(magnitude * adjustment) / 100 + before), 20, 200);
                    state = package.ApplyNormalTurnForceRelationCurrent(state, current, pool, a, b, unchecked(after - before));
                }
            }
            Dissolve();
        }

        //275330 writes a campaign event accomplishment, not the external
        //achievement service. Read the existing history/calendar owners only
        //as their original short-circuit conditions are reached.
        private void ApplyHistoricalVictoryFlag()
        {
            var world = state.CampaignEventWorld ?? throw Missing("event_world");
            uint flags = Need(world.EventAchievementFlags, "event_accomplishments");
            if ((flags & 0x10) != 0) return;
            var target = Force(Selected());
            if (!Valid(target) || Ruler(target) != 736) return;
            var leader = Force(Other());
            if (!Valid(leader) || Ruler(leader) != 35) return;
            int witness = Person(521); //The actual World11A0 Person pointer.
            if (!Need(current.ReadPersonNativeValidity(witness), "witness_validity") ||
                Need(current.ReadPersonStatus(witness), "witness_status") != 1) return;
            var witnessForce = current.ReadPersonForce(witness) ?? throw Missing("witness_Force");
            if (!Valid(witnessForce) || (Flags(witnessForce) & 2) == 0) return;
            var history = state.CampaignEvents ?? throw Missing("event_history");
            if (!history.Occurred[3]) return;
            var occurred = history.OccurrenceDates[3];
            int then = occurred is null ? -31 : unchecked(occurred.Day - 31 +
                (occurred.Month + occurred.Year * 12) * 30);
            var now = state.NativeMapSeason?.ThirdDate ?? throw Missing("event_current_date");
            int today = unchecked(now.Day - 31 + (now.Month + now.Year * 12) * 30);
            if (unchecked(today - then) >= 720 || history.Occurred[5]) return;
            int emperor = Person(Need(CityCaptureTerritoryInputs.ReadEmperor1654(state), "Emperor1654"));
            if (!Need(current.ReadPersonNativeValidity(emperor), "Emperor_validity")) return;
            flags |= 0x10;
            state = state with { CampaignEventWorld = world with { EventAchievementFlags = flags } };
            current.ConditionEventAchievementFlags = flags;
        }

        private void History(int mainRoot, HistoryMessageOperands mainOperands,
            int detailRoot, HistoryMessageOperands detailOperands)
        {
            var main = Format(mainRoot, mainOperands);
            var detail = Format(detailRoot, detailOperands);
            RequireNoVoice(main); RequireNoVoice(detail);
            int day = (package.ReadArmyDestructionSampler(state, current) ?? throw Missing("history_Day450")).Day450;
            state = package.WriteHistory(state, current,
                new(1, 0, 0, day, main.Text, detail.Text, 5012), effects);
        }
        private void Announcement(int root, HistoryMessageOperands operands)
        {
            var formatted = Format(root, operands); RequireNoVoice(formatted);
            effects.Add(new(null, null) { TalentCommandPresentation = new(
                TalentCommandPresentationOperation.Announcement, null, null, null, null, formatted.Text) });
        }
        private void Speech(ForceReference force, int root, HistoryMessageOperands operands)
        {
            int ruler = Ruler(force);
            var source = state;
            var formatted = Format(root, operands);
            effects.Add(CreateSingleOfficerPresentation(current, source, ruler, formatted,
                TalentCommandPresentationOperation.ConditionalOfficerText, "force-unregister.coalition"));
        }
        private HistoryTextResult Format(int root, HistoryMessageOperands operands)
        {
            var text = package.FormatNormalTurnReport(state, current, root, operands);
            state = text.State; return text;
        }
        private void Dissolve() => state = package.DissolveCampaignCoalitionCurrent(state, current, pool, effects);
        //Neither277590 nor274DD0 wraps this SE8 in3343E0.
        private void Sound8() => effects.Add(new(null, null) { TalentCommandPresentation =
            new(TalentCommandPresentationOperation.SoundCue, 8, null, null, null, null) });
        private int Selected() => Need(state.NativeArmySimulation?.World.SelectedForce1656, "selected_target");
        private int Other() => Need(state.NativeForceUnregisterCurrent?.OtherForce1657, "selected_leader");
        private ForceReference Force(int raw) => current.ReadForceTableReference(raw < 52 ? raw : 0)
            ?? throw Missing("Force_pointer");
        private bool Valid(ForceReference force) => Need(current.ReadForceNativeValidity(force), "Force_validity");
        private int Ordinal(ForceReference force) => Need(current.ReadForcePhysicalSlot(force), "Force60");
        private int Physical(ForceReference force) => Need(force.PhysicalSlot, "Force_physical");
        private byte Flags(ForceReference force) => Need(state.NativeArmySimulation?.Forces.GetValueOrDefault(Physical(force))?.Flags12, "Force12");
        private int Ruler(ForceReference force) => Need(current.ReadMoraleWaveRuler22EAC0(force), "Force_ruler10");
        private int Person(int id) => Need((id <= 6000 ? current.ReadPersonLookupReference(id)
            : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_pointer");
        private IEnumerable<ForceReference> Forces()
        {
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Force) ?? throw Missing("ForceC0"))
                yield return entry is { Kind: RegistryKind.Force, PhysicalSlot: int at } ? new(at) : throw Missing("Force_payload");
        }
        private static void RequireNoVoice(HistoryTextResult text)
        { if (text.Voices.Count != 0) throw Missing("detached_resource_voice"); }
        private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("force-unregister.coalition." + field);
    }
}
