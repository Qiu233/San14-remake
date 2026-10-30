using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class ArmyInformationCurrent
    {
        private SortKey TargetSortKey(Unit unit, bool temporary)
        {
            bool visible = rules.UnitDetailCommandVisible(state, Current, unit.Owner);
            //353B20 never consumes the target text/identity after the hidden
            //rank has won. Missing irrelevant legacy target residue is not
            //needed to compare two hidden ordinary targets.
            if (!temporary && !visible) return new TargetKey(int.MaxValue, "", null, true);

            byte action = TargetSortNeed(ArmyConfigurationSourceAdapter.ReadAction(state, unit.Owner), "action37");
            byte kind; ushort identity;
            if (temporary)
            {
                var fields = ArmyOverrideTargetSourceAdapter.Read(state, unit.Owner);
                kind = TargetSortNeed(fields.Kind3C, "temporary_kind3C");
                //Equal raw FF ranks are the source's no-comparison case.
                if (visible && kind == 255) return new TargetKey(255, "", null, true);
                identity = TargetSortNeed(fields.Identity3E, "temporary_identity3E");
            }
            else
            {
                var fields = Current.ReadArmyLifecycleTargetFields(unit.PhysicalSlot)
                    ?? throw TargetSortMissing("ordinary_target_owner");
                kind = TargetSortNeed(fields.Kind38, "ordinary_kind38");
                identity = TargetSortNeed(fields.Identity3A, "ordinary_identity3A");
            }

            int rank = kind is 9 or 11 ? 0x7FFFFFFD : kind;
            if (!temporary && action == 13) rank = 0x7FFFFFFE;
            if (temporary && !visible) rank = 254;
            //FE is deliberately not the FF no-comparison case. Two hidden
            //temporary targets still run367840 with their original kind/ID.
            return new TargetKey(rank, TargetSortText(action, kind, identity), identity, false);
        }

        private string TargetSortText(byte action, byte kind, ushort identity)
        {
            //367840 reads independent sorting fields. Visible captions use
            //other getters and cannot substitute for any of these keys.
            if (kind == 5)
                return rules.ReadCityDetailNameSortKeyCurrent(Current, identity < 52 ? identity : 0);
            if (kind == 6)
            {
                var gate = Current.ReadGateTableReference(identity < 11 ? identity : 0)
                    ?? throw TargetSortMissing("Gate_lookup");
                int physical = TargetSortNeed(gate.PhysicalSlot, "Gate_receiver");
                var rows = rules._scenario.Snapshot.Gates;
                if ((uint)physical >= (uint)rows.Count || rows[physical].Slot != physical)
                    throw TargetSortMissing("Gate_definition");
                return rows[physical].SecondaryText ?? throw TargetSortMissing("Gate_sort_text1A");
            }
            if (kind == 27)
            {
                var army = Current.ReadArmyUnitTableReference(identity < 501 ? identity : 0)
                    ?? throw TargetSortMissing("Army_lookup");
                int commander = TargetSortNeed(Current.ReadArmyUnitCommanderId(army), "Army_commander12");
                var person = commander <= 6000 ? Current.ReadPersonLookupReference(commander)
                    : Current.ReadTransferPersonTableReference(0);
                int physical = TargetSortNeed(person?.PhysicalSlot, "Army_commander_receiver");
                int id = TargetSortNeed(Current.ReadPersonIdentity(physical), "Army_commander_identity");
                //231080 has no Person-validity gate. A missing actual current
                //placeholder name remains unknown instead of using an empty
                //string or the Army's visible commander caption.
                return state.Officers.GetValueOrDefault(id)?.CurrentNameSortKey
                    ?? throw TargetSortMissing("Army_commander_current_name_key34_5A");
            }
            if (kind == 9 && action != 13)
            {
                var hex = Current.ReadHexTableReference(identity < 48400 ? identity : 0)
                    ?? throw TargetSortMissing("Hex_lookup");
                int rawArea = TargetSortNeed(Current.ReadHexAreaSlot(hex), "Hex_Area12");
                var area = Current.ReadAreaTableReference(rawArea < 501 ? rawArea : 0)
                    ?? throw TargetSortMissing("Area_lookup");
                if (!TargetSortNeed(Current.ReadAreaNativeValidity(area), "Area_validity")) return "";
                int physical = TargetSortNeed(area.PhysicalSlot, "Area_receiver");
                var rows = rules._scenario.Snapshot.Areas;
                if ((uint)physical >= (uint)rows.Count || rows[physical].Slot != physical)
                    throw TargetSortMissing("Area_definition");
                return rows[physical].SecondaryText ?? throw TargetSortMissing("Area_sort_text1A");
            }
            //The actual helper returns its normal empty string for every
            //other kind, including Object11 and an action13 Hex target.
            return "";
        }

        private sealed record TargetKey(int Rank, string Text, int? RawId, bool NoCompare) : SortKey
        {
            internal override bool Before(SortKey other, bool ascending)
            {
                if (other is not TargetKey right) throw TargetSortMissing("key_type");
                //Mode3 has no reverse callback for these two columns. Its
                //reverse adapter swaps the source operands; it never negates
                //a predicate or adds an unrelated Army identity tie.
                return ascending ? OrderedBefore(this, right) : OrderedBefore(right, this);
            }

            private static bool OrderedBefore(TargetKey left, TargetKey right)
            {
                if (left.Rank != right.Rank) return left.Rank < right.Rank;
                if (left.NoCompare || right.NoCompare) return false;
                //282A60 is a bounded, NUL-terminated UTF16 equality check.
                //Only exactly equal original text falls through to raw ID;
                //30A810's normalized-equal strings do not get that ID tie.
                if (PersonNameOrder.Compare(left.Text, right.Text) == 0)
                    return TargetSortNeed(left.RawId, "left_raw_target_identity")
                        < TargetSortNeed(right.RawId, "right_raw_target_identity");
                return DeploymentResponsibilityNameOrder.Before(left.Text, right.Text);
            }
        }

        private static T TargetSortNeed<T>(T? value, string field) where T : struct => value ?? throw TargetSortMissing(field);
        private static InvalidDataException TargetSortMissing(string field) => new("army-information.target-sort." + field);
    }
}
