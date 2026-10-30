using System.Text;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class ArmyInformationCurrent
    {
        private readonly Dictionary<int, string> constructionSortNames = [];

        private PreparedCell CommandCell(Unit unit, int column) => column switch
        {
            34 => MakeCell(() => rules.ReadUnitDetailAction(state, Current, unit.Owner),
                () => ActionKey(unit)),
            35 => MakeCell(() => rules.ReadUnitDetailTarget(state, Current, unit.Owner, false),
                () => TargetSortKey(unit, false)),
            36 => MakeCell(() => rules.ReadUnitDetailConstruction(state, Current, unit.Owner),
                () => ConstructionKey(unit)),
            37 => MakeCell(() => rules.ReadUnitDetailTarget(state, Current, unit.Owner, true),
                () => TargetSortKey(unit, true)),
            38 => MakeCell(() => rules.ReadUnitDetailPreference(state, unit.Owner, 142),
                () => new NumberKey((unit.Owner.Behavior ?? throw Missing("enemy_approach40")).AttackNearbyEnemies ? 2 : 1)),
            39 => MakeCell(() => rules.ReadUnitDetailPreference(state, unit.Owner, 147),
                () => new NumberKey((Flags(unit) & 4) != 0 ? 1 : 0)),
            40 => MakeCell(() => rules.ReadUnitDetailPreference(state, unit.Owner, 152),
                () => new NumberKey((Flags(unit) & 8) != 0 ? 1 : 0)),
            41 => MakeCell(() => rules.ReadUnitDetailPreference(state, unit.Owner, 157),
                () => new NumberKey(Choice(unit))),
            _ => throw Missing("command_column")
        };

        private SortKey ActionKey(Unit unit) => Visible(unit) ? new NumberKey(Action(unit)) : new NumberKey(0, true);

        private bool Visible(Unit unit) => rules.UnitDetailCommandVisible(state, Current, unit.Owner);
        private byte Action(Unit unit) => Need(ArmyConfigurationSourceAdapter.ReadAction(state, unit.Owner), "action37");
        private static byte Flags(Unit unit) => Need(unit.Owner.NativeTacticRuntime?.Flags26, "flags26");
        private byte Choice(Unit unit) => Need(unit.Owner.NativeTacticRuntime?.Status10, "status10") == 2
            ? Need(TransportRouteSourceAdapter.ReadChoice(state, unit.Owner), "transport_choice41")
            : Need(unit.Owner.NativeCommandChoice41, "choice41");

        private SortKey ConstructionKey(Unit unit)
        {
            if (!Visible(unit)) return new ConstructionSortKey(0, "", 0x7FFFFFFE);
            int definition;
            switch (Action(unit))
            {
                case 8:
                    definition = Need(FacilityConstructionDefinitions.Selected(state, Current, unit.Owner).PhysicalSlot,
                        "construction_definition46");
                    break;
                case 6: case 9: case 10:
                    var fields = Current.ReadArmyLifecycleTargetFields(unit.PhysicalSlot) ?? throw Missing("target_fields");
                    int target = Need(fields.Identity3A, "target_identity3A");
                    var reference = Current.ReadObjectTableReference(target < 3001 ? target : 0) ?? throw Missing("target_Object_pointer");
                    var obj = Current.ReadPhysicalObject(Need(reference.PhysicalSlot, "target_Object_receiver")) ?? throw Missing("target_Object_fields");
                    definition = obj.RawField12 < 31 ? obj.RawField12 : 0;
                    break;
                default: return new ConstructionSortKey(2, "", 0x7FFFFFFF);
            }
            if (definition is < 0 or >= 31) definition = 0;
            if (!constructionSortNames.TryGetValue(definition, out var text))
            {
                //The normal CE fixed53-byte definition keeps the name in the
                //first10 bytes and the nine-word secondary name immediately
                //after it. Native definition+1A is this secondary text.
                var row = rules._repository.LoadObjectDefinitions(rules._scenario.FixedDataSource.SourcePath)
                    .Catalog.Definitions[definition];
                try { text = new UnicodeEncoding(false, false, true).GetString(row.RawBytes.AsSpan(10, 18)); }
                catch (DecoderFallbackException error)
                { throw new InvalidDataException("army-information.construction_secondary_text_UTF16", error); }
                int end = text.IndexOf('\0'); if (end >= 0) text = text[..end];
                constructionSortNames[definition] = text;
            }
            return new ConstructionSortKey(1, text, 0);
        }

        private sealed record ConstructionSortKey(int Kind, string Text, long Number) : SortKey
        {
            internal override bool Before(SortKey other, bool ascending)
            {
                var right = (ConstructionSortKey)other;
                if (Kind == 2) return false;
                if (right.Kind == 2) return true;
                //The explicit reverse callback preserves text-before-hidden
                //and empty-last; only equal-kind values reverse.
                if (Kind != right.Kind) return Kind == 1;
                return Kind == 1
                    ? (ascending ? PersonNameOrder.Compare(Text, right.Text) < 0 : PersonNameOrder.Compare(right.Text, Text) < 0)
                    : (ascending ? Number < right.Number : Number > right.Number);
            }
        }
    }
}
