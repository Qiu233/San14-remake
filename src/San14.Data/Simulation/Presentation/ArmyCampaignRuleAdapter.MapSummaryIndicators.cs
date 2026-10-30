using System.Collections.ObjectModel;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private MapSummaryIndicatorFrame? mapSummaryIndicatorFrame;
    private SourceWorldContext? mapSummaryIndicatorCurrent;

    /// <summary>The actual map current supplies all Army candidates once per
    /// publication. Source-frame rotation is a separate UI owner and never
    /// calls this reader per tick or captures another World.</summary>
    public MapSummaryIndicatorFrame QueryMapSummaryIndicatorStates(GameState state, SourceWorldContext current)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(current);
        if (ReferenceEquals(mapSummaryIndicatorFrame?.World, state) && ReferenceEquals(mapSummaryIndicatorCurrent, current))
            return mapSummaryIndicatorFrame!;
        var rows = new Dictionary<int, IReadOnlyList<MapSummaryIndicatorState>>();
        var unresolved = new Dictionary<int, string>();
        //The original refresh walks the501 concrete Army pointers, not a
        //nearby-Hex list. Invalid/excluded receivers keep their prior index.
        for (int slot = 0; slot <= 500; slot++)
        {
            try
            {
                var army = current.ReadArmyUnitTableReference(slot) ?? throw SummaryArmyMissing("indicator.army_pointer");
                if (!SummaryArmyNeed(current.ReadArmyUnitNativeValidity(army), "indicator.army_validity")) continue;
                int physical = SummaryArmyNeed(army.PhysicalSlot, "indicator.army_physical");
                if (SummaryArmyNeed(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical),
                    "indicator.army_excluded")) continue;
                rows[physical] = ReadMapSummaryArmyIndicatorStatesCurrent(state, current, physical);
            }
            catch (InvalidDataException error) { unresolved[slot] = error.Message; }
        }
        var frame = new MapSummaryIndicatorFrame(state,
            new ReadOnlyDictionary<int, IReadOnlyList<MapSummaryIndicatorState>>(rows),
            new ReadOnlyDictionary<int, string>(unresolved));
        mapSummaryIndicatorFrame = frame; mapSummaryIndicatorCurrent = current;
        return frame;
    }

    private IReadOnlyList<MapSummaryIndicatorState> ReadMapSummaryArmyIndicatorStatesCurrent(
        GameState state, SourceWorldContext current, int physical)
    {
        var unit = SummaryArmyUnit(state, physical);
        var runtime = unit.NativeTacticRuntime ?? throw SummaryArmyMissing("indicator.runtime");
        int mask = (runtime.Abnormal23 != 0 ? 1 : 0) | (runtime.Abnormal24 != 0 ? 2 : 0) |
            (runtime.Abnormal25 != 0 ? 4 : 0);
        int person = SummaryArmyCommander(current, physical);
        if (!SummaryArmySupplied(current, unit, person, physical)) mask |= 1 << 4;
        else
        {
            bool alternate = false;
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                ?? throw SummaryArmyMissing("indicator.player_pointer");
            int playerId = SummaryArmyNeed(player.PhysicalSlot, "indicator.player_physical");
            var owner = current.ReadArmyUnitForceReference(new(physical)) ?? throw SummaryArmyMissing("indicator.force_pointer");
            if (playerId != 0 && playerId == SummaryArmyNeed(owner.PhysicalSlot, "indicator.force_physical"))
            {
                //The own-player predicate precedes commander-home lookup.
                //22A730 resolves the person's actual E2 location, then22A940
                //resolves a Gate through its City before testing nativeC9 bit2.
                int home = SummaryArmyNeed(current.ReadPersonNativeValidity(person), "indicator.commander_validity")
                    ? SummaryArmyNeed(current.ReadPersonFirstLocation(person), "indicator.home") : 0;
                var basis = BaseLookup.Find(home, current);
                if (SummaryArmyNeed(current.ReadBaseNativeValidity(basis), "indicator.home_validity"))
                {
                    var city = BaseCurrencyAdditionSource.CurrencyCity(basis, current);
                    if (SummaryArmyNeed(current.ReadBaseNativeValidity(city), "indicator.city_validity"))
                        alternate = (SummaryArmyNeed(current.ReadCaptureCityByteC9(
                            SummaryArmyNeed(city.PhysicalSlot, "indicator.city_physical")), "indicator.city_C9") & 2) != 0;
                }
            }
            mask |= 1 << (alternate ? 5 : 3);
        }
        return MapSummaryIndicatorRules.FromMask(mask);
    }
}
