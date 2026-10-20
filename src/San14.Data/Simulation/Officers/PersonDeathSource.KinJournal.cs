using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal sealed partial class PersonDeathSource
    {
        private void ApplyDeathKinJournal(int person, ForceReference capturedForce,
            HexReference capturedPosition, int eventCode)
        {
            int control = latest.CampaignChronicle?.RecordingSuppression ?? throw Missing("kin_journal_recording_control");
            if (control != 0) return;
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(latest, current) ?? throw Missing("kin_journal_current_player");
            int playerPhysical = Known(player.PhysicalSlot, "kin_journal_player_pointer");
            ushort raw = Known(current.ReadForceRecord(playerPhysical)?.RawRuler10, "kin_journal_raw_ruler10");
            var ruler = (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
                ?? throw Missing("kin_journal_ruler_lookup");
            var deceased = new TransferPersonReference(person);
            if (!(KinSworn(ruler, deceased) || KinSpouse(ruler, deceased) ||
                KinRelated(ruler, deceased) || KinListHit(ruler, deceased)))
            {
                Observe("death.kin-journal-unrelated");
                return;
            }

            int day = DeathHistoryDay();
            string name = KinFullName(Owner(person), true);
            string forceLabel = package.HistoryWriter.FallbackName(latest, current, capturedForce, KinFullName);
            int hex = Known(capturedPosition.PhysicalSlot, "kin_journal_captured_Hex");
            string forceName = package.HistoryWriter.CustomFirstName(latest, current, capturedForce, KinFullName);
            int color = Known(current.ReadForceReportColor(capturedForce), "kin_journal_current_Force49");
            RequireKinLabel(forceLabel);
            RequireKinLabel(name);
            uint kind = eventCode switch { 12 => 6, 13 => 7, 14 => 8, _ => 0 };
            var command = new CampaignChronicleCommand(eventCode, 1, day, forceName, color, kind,
                ImmutableArray.Create(forceLabel, name, string.Empty), ImmutableArray.Create(hex, 48400, 48400, 48400));
            latest = new CampaignChronicleWriter(package.HistoryWriter).Write(latest, current, command);
            Observe(kind == 0 ? "death.kin-journal-no-main-entry" : "death.kin-journal-written");
        }

        private static string KinFullName(OfficerState person, bool full)
        {
            string first = Head(person.CurrentSurname ?? throw Missing("kin_journal_current_surname"));
            return full ? first + Head(person.CurrentGivenName ?? throw Missing("kin_journal_current_given_name")) : first;
            static string Head(string text) => text.IndexOf('\0') is int n && n >= 0 ? text[..n] : text;
        }
        private static void RequireKinLabel(string text)
        {
            if (text.Length > 8 || text.Any(c => c == '\0' || char.IsSurrogate(c)))
                throw Missing("kin_journal_complete_label_copy_profile");
        }

        private bool KinPair(TransferPersonReference a, TransferPersonReference b)
        {
            if (!Valid(a) || !Valid(b)) return false;
            if (Valid(a) && Valid(b))
            {
                ushort bId = KinId(b);
                if (KinId(a) == bId) return false;
            }
            return true;
        }
        private bool KinSworn(TransferPersonReference a, TransferPersonReference b)
        {
            if (!KinPair(a, b)) return false;
            ushort word = KinWord(a, 0xBC);
            return word != 0 && word == KinWord(b, 0xBC);
        }
        private bool KinSpouse(TransferPersonReference a, TransferPersonReference b)
        {
            if (!KinPair(a, b)) return false;
            ushort word = KinWord(a, 0xBA);
            return word != 0 && word == KinId(b);
        }
        private bool KinParent(TransferPersonReference a, TransferPersonReference b, int field)
        {
            if (!KinPair(a, b)) return false;
            ushort word = KinWord(a, field);
            return word != 0 && word == KinId(b);
        }
        private bool KinSiblings(TransferPersonReference a, TransferPersonReference b)
        {
            if (!KinPair(a, b)) return false;
            ushort father = KinWord(a, 0xB4);
            if (father != 0 && father == KinWord(b, 0xB4)) return true;
            ushort mother = KinWord(a, 0xB6);
            return mother != 0 && mother == KinWord(b, 0xB6);
        }
        private bool KinRelated(TransferPersonReference a, TransferPersonReference b)
        {
            if (!Valid(a) || !Valid(b)) return false;
            if (Status(Physical(a, "kin_journal_status_a")) is >= 1 and <= 4 &&
                Status(Physical(b, "kin_journal_status_b")) is >= 1 and <= 4)
            {
                int first = Known(current.ReadPersonOldForceSlot(Physical(a, "kin_journal_force_a")), "kin_journal_raw_Force_a");
                int second = Known(current.ReadPersonOldForceSlot(Physical(b, "kin_journal_force_b")), "kin_journal_raw_Force_b");
                if (first != second) return false;
            }
            if (KinParent(a, b, 0xB4) || KinParent(a, b, 0xB6) ||
                KinParent(b, a, 0xB4) || KinParent(b, a, 0xB6) ||
                KinSpouse(a, b) || KinSiblings(a, b) || KinSworn(a, b)) return true;
            ushort clan = KinWord(a, 0xB2);
            return clan != 0 && clan == KinWord(b, 0xB2);
        }
        private bool KinListHit(TransferPersonReference a, TransferPersonReference b)
        {
            if (!Valid(b)) return false;
            ushort target = KinId(b);
            for (int i = 0; i < 8; i++)
                if (RelationListWord(Physical(a, "kin_journal_C0_receiver"), i, true) == target) return true;
            return false;
        }
        private ushort KinId(TransferPersonReference person) => Identity(Physical(person, "kin_journal_Person_ID_pointer"));
        private ushort KinWord(TransferPersonReference person, int field) => RelationWord(Physical(person, "kin_journal_relation_pointer"), field);
    }
}
