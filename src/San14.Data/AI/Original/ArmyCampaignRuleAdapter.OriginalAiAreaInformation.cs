using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal BaseInformationValue ReadOriginalAiAreaInformationCurrent(GameState state,
        SourceWorldContext current, AreaReference area, int mode)
    {
        int physical = BaseInfoNeed(area.PhysicalSlot, "Area_identity");
        BaseInformationValue Number(int value) => new BaseInformationInteger(value);
        BaseInformationValue City(int requested)
        {
            var at = current.ReadCityTableReference(requested < 52 ? requested : 0)
                ?? throw BaseInfoMissing("Area_City_pointer");
            return new BaseInformationEntity(AiForceInformationBaseEntity(current, at));
        }
        int Progress(int dimension)
        {
            var values = state.Areas.GetValueOrDefault(physical)?.Development ?? throw BaseInfoMissing("Area_development");
            int? value = dimension switch { 0 => values.Commerce, 1 => values.Agriculture, _ => values.Barracks };
            return BaseInfoNeed(value, "Area_development_" + dimension);
        }
        switch (mode)
        {
            case 0: return Number(physical);
            case 1:
            {
                int raw = BaseInfoNeed(current.ReadAreaHexIndex(area), "Area_Hex3A");
                return new BaseInformationHex(TroopsInfoHex(current, raw < 48400 ? raw : 0));
            }
            case 2: return City(BaseInfoNeed(current.ReadAreaScenarioForceSlot(area), "Area_original_City34"));
            case 3: return City(BaseInfoNeed(current.ReadAreaCitySlot(area), "Area_current_City35"));
            case 4: return new BaseInformationEntity(BaseInfoPersonEntity(current,
                BaseInfoNeed(current.ReadAssignmentArea36(physical), "Area_officer36")));
            case 5: return Number(CeAreaIncomeMath.Level(checked((ushort)Progress(0))));
            case 6: return Number(Progress(0));
            case 7: return Number(Progress(1));
            case 8: return Number(Progress(2));
            case 9: return new BaseInformationUnchanged();
            //22EAF0 is the constant maximum, shared by all three progress words.
            case 10 or 11 or 12: return Number(1000);
            case 13: return Number(BaseInfoNeed(state.Areas.GetValueOrDefault(physical)?.NativeNormalAftermath?.Quantity52, "Area_population52"));
            case 14: return Number(BaseInfoNeed(state.Areas.GetValueOrDefault(physical)?.Security, "Area_security54"));
            case 18:
                _ = BaseInfoNeed(current.ReadAreaCurrentForceSlot(area), "Area_current_Force");
                return Number(BaseInfoNeed(state.Areas.GetValueOrDefault(physical)?.Security, "Area_security54"));
            case 19:
            {
                var result = new List<OriginalAiEntity>();
                //22F1C0 walks the eight authored neighbor slots, preserving repeats.
                for (int index = 0; index < 8; index++)
                {
                    ushort raw = BaseInfoNeed(current.ReadAreaNeighborSlot(area, index), "Area_neighbor56");
                    if (raw == 0) continue;
                    var next = current.ReadAreaTableReference(raw < 501 ? raw : 0) ?? throw BaseInfoMissing("Area_neighbor_pointer");
                    if (BaseInfoNeed(current.ReadAreaNativeValidity(next), "Area_neighbor_validity"))
                        result.Add(new(WorldEntityKind.Area, BaseInfoNeed(next.PhysicalSlot, "Area_neighbor_identity")));
                }
                return new BaseInformationEntities(WorldEntityKind.Area, result);
            }
            case 20:
            {
                int raw = BaseInfoNeed(current.ReadAreaCurrentForceSlot(area), "Area_representative_Force");
                var force = current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw BaseInfoMissing("Area_Force_pointer");
                return new BaseInformationEntity(new(WorldEntityKind.Force, BaseInfoNeed(force.PhysicalSlot, "Area_Force_identity")));
            }
            case 22: return new BaseInformationHexes(BaseInfoAreaHexes(current, physical).ToArray());
            case 23:
            {
                int raw = BaseInfoNeed(current.ReadAreaHexIndex(area), "Area_Hex3A");
                var hex = current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw BaseInfoMissing("Area_Hex_pointer");
                int location = BaseInfoNeed(current.ReadHexObjectLink(hex), "Area_Hex_Object1A");
                var home = BaseLookup.Find(location < 62 ? location : 0, current);
                return BaseInfoNeed(current.ReadBaseNativeValidity(home), "Area_Hex_Base_validity")
                    ? new BaseInformationEntity(AiForceInformationBaseEntity(current, home))
                    : City(BaseInfoNeed(current.ReadAreaCitySlot(area), "Area_fallback_City35"));
            }
            case 24:
            {
                var result = new List<OriginalAiEntity>();
                foreach (var hex in BaseInfoAreaHexes(current, physical))
                {
                    int raw = BaseInfoNeed(current.ReadHexObjectLink(new(hex.Id)), "Area_Hex_Object1A");
                    var target = current.ReadObjectTableReference(raw < 3001 ? raw : 0) ?? throw BaseInfoMissing("Area_Object_pointer");
                    if (!BaseInfoNeed(current.ReadObjectNativeValidity(target), "Area_Object_validity")) continue;
                    int slot = BaseInfoNeed(target.PhysicalSlot, "Area_Object_identity");
                    var record = current.ReadPhysicalObject(slot) ?? throw BaseInfoMissing("Area_Object_record");
                    var definition = current.ReadObjectDefinitionReference(record.RawField12 < 31 ? record.RawField12 : 0)
                        ?? throw BaseInfoMissing("Area_Object_definition");
                    if (BaseInfoNeed(current.ReadObjectDefinitionRaw2C(definition), "Area_Object_kind") != 1)
                        result.Add(new(WorldEntityKind.MapObject, slot));
                }
                return new BaseInformationEntities(WorldEntityKind.MapObject, result);
            }
            default: throw BaseInfoMissing("Area_selector_" + mode);
        }
    }

    private static IEnumerable<OriginalAiHex> BaseInfoAreaHexes(SourceWorldContext current, int area)
    {
        for (int hex = AreaHexIterator.FindNextIndex(0, area, current); hex < 48400;
            hex = AreaHexIterator.FindNextIndex(hex + 1, area, current))
            yield return TroopsInfoHex(current, hex);
    }
}
