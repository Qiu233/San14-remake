using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal MapSummaryPage QueryPersonalityMapSummaryCurrent(GameState state, SourceWorldContext current,
        MapSummaryReceiver receiver)
    {
        var text = new Dictionary<int, string> { [2] = ReadHistoryDataText(1, 0x360) };
        var rows = new List<MapSummaryTraitRow>();
        var result = new MapSummaryPage(receiver.Slot, receiver.ReceiverId, "00_82_03_02_sum_butai_kosei", null, text)
            { Traits = rows };
        try
        {
            if (!SummaryArmyNeed(current.ReadArmyUnitNativeValidity(new(receiver.ReceiverId)), "traits.army_validity"))
                return result with { ReceiverValid = false };
            var army = SummaryArmyUnit(state, receiver.ReceiverId);
            int person = SummaryArmyCommander(current, receiver.ReceiverId);
            int formation = SummaryArmyNeed(ArmyConfigurationSourceAdapter.ReadCurrentLand(state, army), "traits.land1B");
            byte formationMask = formation is 19 or 20 ? (byte)8 : (byte)2;
            //92A070 uses source slot0 through8, no five-entry map-label cap.
            for (int index = 0; index < 9; index++)
            {
                var trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(current, person, index)
                    ?? throw SummaryArmyMissing("traits.pointer");
                int id = SummaryArmyNeed(trait.PhysicalSlot, "traits.identity");
                if (id is < 1 or > 355) continue;
                byte first = SummaryArmyNeed(current.ReadPersonalityByte(trait, 0xB6), "traits.effect1");
                byte second = SummaryArmyNeed(current.ReadPersonalityByte(trait, 0xBA), "traits.effect2");
                //89CB70 category2's finite offline table column. Other
                //categories and their special15 gate are not this caller.
                if (!SummaryArmyTraitAdmission(first) && !SummaryArmyTraitAdmission(second)) continue;
                if ((SummaryArmyNeed(current.ReadSkillPersonalityFlags(trait), "traits.flags") & formationMask) == 0) continue;
                bool active = SummaryArmyTraitActive(current, person, trait, index);
                byte style = SummaryArmyNeed(current.ReadPersonalityByte(trait, 0xBE), "traits.style");
                rows.Add(new(index, current.ReadPersonalityName(trait), style, active));
            }
            return result with { ReceiverValid = rows.Count != 0 };
        }
        catch (InvalidDataException error) { return result with { UnresolvedOwner = error.Message }; }
    }

    private static bool SummaryArmyTraitAdmission(byte effect) => effect is >= 1 and <= 105 or >= 107 and <= 112
        or >= 115 and <= 118 or 124;

    private static bool SummaryArmyTraitActive(SourceWorldContext current, int person, PersonalityReference trait, int sourceIndex)
    {
        var associated = PersonArmyLocationSource.ReadCommanderArmy(current, person)
            ?? throw SummaryArmyMissing("traits.associated_army");
        if (SummaryArmyNeed(current.ReadArmyUnitNativeValidity(associated), "traits.associated_validity"))
        {
            int id = SummaryArmyNeed(trait.PhysicalSlot, "traits.identity");
            //8A6F80/2308D0 use the first matching source slot, including
            //duplicates. They do not test the displayed compact row index.
            for (int first = 0; first < 9; first++)
            {
                if (SummaryArmyNeed(TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current, person, first), "traits.related_lookup") != id) continue;
                var fields = current.ReadArmyReturnTraitFields(associated) ?? throw SummaryArmyMissing("traits.related_descriptors");
                int count = SummaryArmyNeed(current.ReadTemporaryHexListCount(new(fields.Descriptors160[first].Identity)), "traits.related_count");
                if (count > 0) return true;
                break;
            }
        }
        return SummaryArmyNeed(current.ReadPersonalityWord(trait, 0xB4), "traits.condition") == ushort.MaxValue ||
            (SummaryArmyNeed(current.ReadPersonTraitMask1BC(person), "traits.active_mask") & (1 << sourceIndex)) != 0;
    }
}
