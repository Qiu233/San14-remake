using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal sealed partial class PersonDeathSource
    {
        private BaseReference? deathOriginalCity;
        //295EE0 captures this result after detaching the victim and before
        //the killer's2704F0 penalty. It is not a generic Force-empty query.
        private bool ReadRulerWillUnregister(int person, int cause)
        {
            var force = deathOriginalForce ?? throw Missing("ruler_captured_Force");
            int cities = Known(PersonArmyCaptureSource.ReadOwnedCityCount(current, force), "ruler_City_count22AB60");
            bool noTerritory = cities == 0 || cities == 1 &&
                PersonPositionRepair.DecodeSecondLocation(Known(current.ReadPersonSecondLocation(person), "ruler_E4")).Kind == 5 && cause == 2;
            int count = CountRulerServingPeople(force);
            return count == 1 || noTerritory;
        }

        //The common death caller retains its outer relationship temporaries.
        //A normal293BD0 completion returns0; only its actual terminal service
        //can return1 and bypass the remaining Person reset.
        private int ApplyRulerSuccession(int person, TransferPersonReference killer, bool willUnregister)
        {
            _ = deathOriginalForce ?? throw Missing("ruler_captured_Force");
            _ = deathOriginalCity ?? throw Missing("ruler_captured_City");
            ApplyRulerDeathCoalitionLeader(person); //2777F0 precedes275020.
            ApplyRulerDeathCoalitionTarget(person, killer);
            if (willUnregister)
            {
                //This caller reaches293BD0 with the ruler and its residual
                //Base/Army/Person owners still live. The empty-retirement
                //leaf is not that operation and cannot stand in for it.
                throw Missing("death_force_unregister293BD0_current_operation");
            }
            else
            {
                int successor = ApplyRulerSuccessorSelection(person);
                if (!Valid(killer))
                    foreach (var item in PersonItems(person))
                    {
                        ClearItemFlag3(item);
                        WriteDirectHeldItem(item, (ushort)Math.Min((int)Identity(successor), 6000));
                    }
            }
            //The original caller still invokes2BE120, including its prefix
            //when the special ruler transfer has emptied the Item iterator.
            return 0;
        }

        private int CountRulerServingPeople(ForceReference force)
        {
            int id = Known(current.ReadForcePhysicalSlot(force), "ruler_count_Force60"), count = 0;
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("ruler_count_Person100"))
            {
                int person = Known(entry, "ruler_count_Person_payload");
                if (id is < 1 or > 51 || Known(current.ReadPersonOldForceSlot(person), "ruler_count_Person_Force") != id ||
                    Status(person) is < 1 or > 4) continue;
                ushort first = Known(current.ReadPersonFirstLocation(person), "ruler_count_E2");
                if (BattleRemakeCurrentRegionFactor.IsNativeBandPerson(Identity(person)) && first is >= 1 and <= 61 &&
                    Known(current.ReadForcePhysicalSlot(Force(person)), "ruler_count_synthetic_Force") is not (>= 46 and <= 50)) continue;
                if (first is < 1 or > 61 &&
                    Known(current.ReadForcePhysicalSlot(Force(person)), "ruler_count_noHome_Force") is >= 46 and <= 50) continue;
                if ((Known(current.ReadOwnedReturnPersonFlags(person), "ruler_count_flags15E") & 0x4000) != 0) continue;
                count = unchecked(count + 1);
            }
            return count;
        }

        private void ApplyRulerDeathCoalitionLeader(int person)
        {
            int other = Known(latest.NativeForceUnregisterCurrent?.OtherForce1657, "ruler_coalition_World1657");
            if (other is < 1 or > 51) return;
            var leader = RulerCoalitionForce(other);
            if (!Known(current.ReadForceNativeValidity(leader), "ruler_coalition_leader_validity") ||
                RulerCoalitionPerson(leader) != person) return;
            int selected = Known(latest.NativeArmySimulation?.World.SelectedForce1656, "ruler_coalition_World1656");
            if (selected is < 1 or > 51) return;
            var target = RulerCoalitionForce(selected);
            if (!Known(current.ReadForceNativeValidity(target), "ruler_coalition_target_validity")) return;
            var effects = new List<CampaignEffectRequest>();
            RulerCoalitionHistory(0x2A35, new(Force0: selected), 0x2A36,
                () => new(Person0: 5012, Person1: package.TalentRulerCurrent(current,
                    OwnedCurrentPlayerForce.ReadCurrentReference(latest, current) ?? throw Missing("ruler_coalition_player")),
                    Person2: package.TalentRulerCurrent(current, leader), Force0: selected), effects);
            RulerCoalitionAnnouncement(0x2A37, new(Person0: Identity(person), Force0: selected), effects);
            latest = package.DissolveCampaignCoalitionCurrent(latest, current, pool, effects);
            AppendRulerSuccessionEffects(effects);
        }

        private void ApplyRulerDeathCoalitionTarget(int person, TransferPersonReference killer)
        {
            int selected = Known(latest.NativeArmySimulation?.World.SelectedForce1656, "ruler_coalition_fresh_World1656");
            if (selected is < 1 or > 51) return;
            var target = RulerCoalitionForce(selected);
            if (!Known(current.ReadForceNativeValidity(target), "ruler_coalition_fresh_target_validity") ||
                RulerCoalitionPerson(target) != person) return;
            ForceReference? winner = Valid(killer) ? Force(Physical(killer, "ruler_coalition_killer")) : null;
            var effects = new List<CampaignEffectRequest>();
            if (winner is not null && Known(current.ReadForceNativeValidity(winner), "ruler_coalition_killer_Force_validity") &&
                (Known(latest.NativeArmySimulation?.Forces.GetValueOrDefault(Known(winner.PhysicalSlot, "ruler_coalition_winner"))?.Flags12,
                    "ruler_coalition_winner12") & 2) != 0)
                latest = package.ApplyRulerDeathCoalitionVictoryCurrent(latest, current, pool, winner, effects);
            else
            {
                RulerCoalitionHistory(0x2A38, new(Force0: selected), 0x2A39,
                    () => new(Person0: 5012, Person1: package.TalentRulerCurrent(current,
                        OwnedCurrentPlayerForce.ReadCurrentReference(latest, current) ?? throw Missing("ruler_coalition_player")),
                        Person2: Identity(person), Force0: selected), effects);
                RulerCoalitionAnnouncement(0x2A3A, new(Person0: Identity(person), Force0: selected), effects);
                latest = package.DissolveCampaignCoalitionCurrent(latest, current, pool, effects);
            }
            AppendRulerSuccessionEffects(effects);
        }

        private void RulerCoalitionHistory(int mainRoot, HistoryMessageOperands mainOperands,
            int detailRoot, Func<HistoryMessageOperands> detailOperands, List<CampaignEffectRequest> effects)
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(latest, current) ?? throw Missing("ruler_coalition_history_player");
            if (!Known(current.ReadForceNativeValidity(player), "ruler_coalition_history_player_validity")) return;
            var main = package.FormatNormalTurnReport(latest, current, mainRoot, mainOperands); latest = main.State;
            var detail = package.FormatNormalTurnReport(latest, current, detailRoot, detailOperands()); latest = detail.State;
            latest = package.WriteHistory(latest, current,
                new(1, 0, 0, DeathHistoryDay(), main.Text, detail.Text, 5012), effects);
        }
        private void RulerCoalitionAnnouncement(int root, HistoryMessageOperands operands, List<CampaignEffectRequest> effects)
        {
            effects.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.SoundCue, 8, null, null, null, null) });
            var text = package.FormatNormalTurnReport(latest, current, root, operands); latest = text.State;
            effects.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.Announcement, null, null, null, null, text.Text) });
        }
        private ForceReference RulerCoalitionForce(int raw) => current.ReadForceTableReference(raw < 52 ? raw : 0)
            ?? throw Missing("ruler_coalition_Force_pointer");
        private int RulerCoalitionPerson(ForceReference force) => Physical(ItemLookup(ItemForceWord10(force)), "ruler_coalition_ruler_pointer");

        //The dedicated PersonDeath union owns only its narration payloads.
        //These existing shared gameplay intents keep their admitted batch
        //contract and their actual order in the enclosing death operation.
        private void AppendRulerSuccessionEffects(IReadOnlyList<CampaignEffectRequest> effects)
        {
            if (effects.Count == 0) return;
            var queue = latest.CampaignEffectRequests ?? throw Missing("ruler_effect_owner");
            long revision = latest.NativeClock?.UpdateOrdinal ?? throw Missing("ruler_effect_revision");
            latest = latest with { CampaignEffectRequests = queue.Append(CampaignEffectSource.MonthlyMaintenance, revision, effects) };
        }
    }
}
