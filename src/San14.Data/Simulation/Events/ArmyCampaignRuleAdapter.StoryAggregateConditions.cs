using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal void BindStoryAggregateConditionsCurrent(GameState state, SourceWorldContext current) =>
        current.StoryAggregateConditions = new(this, state, current);

    internal StoryAggregateConditionResult EvaluateStoryAggregateConditionCurrent(GameState state,
        SourceWorldContext current, int code, IReadOnlyList<int> arguments, ForceReference? force)
    {
        if (arguments.Count != 8) throw AggregateConditionMissing("operands");
        if (code == 31)
            return new(ReadStoryForceDoctrineCurrent(state, current,
                force ?? throw AggregateConditionMissing("doctrine_Force_pointer")) == arguments[1]);
        if (code == 43)
        {
            if (force is null || !AggregateConditionNeed(current.ReadForceNativeValidity(force), "government_Force_validity") ||
                arguments[1] is < 1 or > 5) return new(false);
            int physical = AggregateConditionNeed(force.PhysicalSlot, "government_Force_receiver");
            int count = 0;
            for (int ordinal = 0; ordinal < 7; ordinal++)
            {
                string key = BattleRemakeCurrentForcePolicies.DepartmentPersonKey(physical, arguments[1] - 1, ordinal);
                int id = state.NativeArmySimulation?.Parameters.Int32Values.TryGetValue(key, out int value) == true &&
                    value is >= 0 and <= ushort.MaxValue ? value : throw AggregateConditionMissing("government_person_word");
                //22D480 counts raw IDs1..6000. Capacity, pending assignment,
                //Person lookup and native Person validity are not consulted.
                if (id is >= 1 and <= 6000) count++;
            }
            return new((count > 0 ? 1 : 0) == arguments[2]);
        }
        if (code is not (432 or 433)) throw AggregateConditionMissing("leaf");
        int target = arguments[0], firstKind = arguments[1], mode = arguments[2], secondKind = arguments[3];
        if (target < 1 || target > (code == 432 ? 17 : 6) || firstKind is < 1 or > 3 ||
            secondKind is < 1 or > 3 || firstKind == secondKind) return new(false);

        int first = 0, second = 0;
        //3AD170/3AFCB0 walk the complete101-pointer Scenery table, including
        //aliases. This is neither the registry list nor an owned dictionary.
        for (int slot = 0; slot < 101; slot++)
        {
            var scenery = current.ReadSceneryTableReference(slot) ?? throw AggregateConditionMissing("Scenery_table_pointer");
            if (!AggregateConditionNeed(current.ReadSceneryNativeValidity(scenery), "Scenery_validity")) continue;
            if ((AggregateConditionNeed(current.ReadSceneryFlags47(scenery), "Scenery_flags47") & 1) == 0) continue;
            int sceneryId = AggregateConditionNeed(scenery.PhysicalSlot, "Scenery_receiver");
            var development = state.LandmarkInvestments?.GetValueOrDefault(sceneryId)?.Development
                ?? throw AggregateConditionMissing("Scenery_current_development");
            if (development.EffectKind == 0 || development.Effect == 0) continue;
            //22A800(receiver,0) is precisely the current raw effect56. The
            //officer bonus branch of that getter is not requested here.
            var area = TalentSceneryArea(current, scenery);
            if (!AggregateConditionNeed(current.ReadAreaNativeValidity(area), "Scenery_Area_validity")) continue;
            byte originalCity = AggregateConditionNeed(current.ReadAreaValidityCode(area), "Area_original_City34");
            var city = current.ReadCityTableReference(originalCity < 52 ? originalCity : 0)
                ?? throw AggregateConditionMissing("original_City_pointer");
            if (!AggregateConditionNeed(current.ReadBaseNativeValidity(city), "original_City_validity")) continue;
            var province = current.ReadConditionCityState(AggregateConditionNeed(city.PhysicalSlot, "original_City_receiver"))
                ?? throw AggregateConditionMissing("State_pointer");
            if (!AggregateConditionNeed(current.ReadConditionStateValidity(province), "State_validity")) continue;
            int provincePhysical = AggregateConditionNeed(province.PhysicalSlot, "State_receiver");
            int actual;
            if (code == 432)
            {
                int anchor = AggregateConditionNeed(current.ReadConditionStateTableSlot(0)?.PhysicalSlot, "State_anchor");
                actual = provincePhysical - anchor;
            }
            else
            {
                byte rawRegion = AggregateConditionNeed(BaseCaptureRewardInputs.ReadStateRegion(state, provincePhysical), "State_region2E");
                var region = current.ReadStaticTableReference(0xDC20, rawRegion < 12 ? rawRegion : 0)
                    ?? throw AggregateConditionMissing("Region_pointer");
                if (region.PhysicalSlot is not int regionPhysical) continue;
                int anchor = AggregateConditionNeed(current.ReadStaticTableReference(0xDC20, 0)?.PhysicalSlot, "Region_anchor");
                if (regionPhysical == anchor) continue;
                actual = regionPhysical - anchor;
            }
            if (actual != target) continue;
            if (development.EffectKind == firstKind) first = unchecked(first + development.Effect);
            else if (development.EffectKind == secondKind) second = unchecked(second + development.Effect);
        }
        bool result = mode switch
        {
            1 => first != second, 2 => first == second, 3 => first > second,
            4 => first >= second, 5 => first < second, 6 => first <= second, _ => false
        };
        bool manager = AggregateConditionNeed(current.ReadEventManagerPresent(), "event_manager_presence");
        return new(result, manager ? (result ? firstKind : secondKind) : null);
    }

    //22BFA0 reads the Force receiver even when it is Force0, resolves its
    //actual current ruler and tests the selected Doctrine's name validity.
    //The same getter supplies the government representative capacity leaves.
    internal int ReadStoryForceDoctrineCurrent(GameState state, SourceWorldContext current, ForceReference force)
    {
        int forcePhysical = AggregateConditionNeed(force.PhysicalSlot, "doctrine_Force_receiver");
        ushort rawRuler = AggregateConditionNeed(current.ReadForceRecord(forcePhysical)?.RawRuler10, "Force_ruler10");
        var ruler = (rawRuler < 6001 ? current.ReadPersonLookupReference(rawRuler) : current.ReadTransferPersonTableReference(0))
            ?? throw AggregateConditionMissing("ruler_pointer");
        int person = AggregateConditionNeed(ruler.PhysicalSlot, "ruler_receiver");
        ushort identity = AggregateConditionNeed(current.ReadPersonIdentity(person), "ruler_identity");
        var officer = state.Officers.GetValueOrDefault(identity) ?? throw AggregateConditionMissing("ruler_current_owner");
        int rawDoctrine = AggregateConditionNeed(OfficerDoctrineRules.ReadDoctrineId(officer.DoctrineId, officer.AbilityState), "ruler_doctrine14C");
        var doctrine = current.ReadStaticTableReference(0x78CF8, (uint)rawDoctrine < 7 ? rawDoctrine : 0)
            ?? throw AggregateConditionMissing("Doctrine_pointer");
        if (doctrine.PhysicalSlot is not int physical) return 0;
        int anchor = AggregateConditionNeed(current.ReadStaticTableReference(0x78CF8, 0)?.PhysicalSlot, "Doctrine_anchor");
        if (physical == anchor) return 0;
        string name;
        if (state.Doctrines.TryGetValue(physical, out var definition)) name = definition.Name;
        else
        {
            var catalog = _scenario.Snapshot.DoctrineParameters?.Doctrines ?? throw AggregateConditionMissing("Doctrine_catalog");
            if ((uint)physical >= catalog.Count || physical is >= 1 and <= 6)
                throw AggregateConditionMissing("Doctrine_current_owner");
            name = catalog[physical].Name;
        }
        if (name.Length == 0) return 0;
        return physical - anchor;
    }

    private static T AggregateConditionNeed<T>(T? value, string field) where T : struct =>
        value ?? throw AggregateConditionMissing(field);
    private static InvalidDataException AggregateConditionMissing(string field) => new("story.aggregate_condition." + field);
}
