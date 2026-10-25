using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //227070 clears the five point words and five acquired-level bytes.
    //237CC0's scenario(static8C) branch skips this runtime section entirely.
    internal GameState InstallFreshForeignTradeProgress(GameState state)
    {
        var native = state.NativeArmySimulation ?? throw ForeignTradeMissing("fresh_native_owner");
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0)
            throw ForeignTradeMissing("fresh_boundary");
        var forces = native.Forces.ToDictionary(pair => pair.Key, pair => pair.Value with
        {
            BestAdministrationLevelSum = 0,
            BestExceptionalOfficerCount = 0,
            DevelopmentWarningFlags = 0,
            ForeignTradeProgress = new(ImmutableArray.Create<ushort>(0, 0, 0, 0, 0),
                ImmutableArray.Create<byte>(0, 0, 0, 0, 0))
        });
        return state with { ForceProcessingOrder = new(Array.Empty<int>()), NativeArmySimulation = native with
        {
            Forces = forces,
            World = native.World with { PlayerCoalitionCooldownDate = new(0, 1, 1, 0), LeadingDoctrineYearCount = 0 }
        } };
    }

    //429460 iterates five CForeignData objects, then the complete live C0 for
    //each one. There is no extra Force validity filter or duplicate removal.
    internal GameState ApplyNormalTurnSeasonalForeignTradeCurrent(GameState state, SourceWorldContext current)
    {
        if (Seed.Language != "TC") throw ForeignTradeMissing("locale_rules");
        for (int slot = 0; slot < 5; slot++)
        {
            var definition = current.ReadStaticTableReference(0x85A20, slot) ?? throw ForeignTradeMissing("country_pointer");
            int country = definition.PhysicalSlot ?? throw ForeignTradeMissing("country_physical");
            //234060 tests the native contiguous receiver ordinal, not its
            //name or diplomatic relation bytes. Receiver0 is the placeholder.
            if (country == 0) continue;
            if (country is < 0 or > 4) throw ForeignTradeMissing("country_ordinal");
            foreach (var pointer in current.ReadRegistryReferences(RegistryKind.Force) ?? throw ForeignTradeMissing("Force_C0"))
            {
                if (pointer is not { Kind: RegistryKind.Force, PhysicalSlot: int physical }) throw ForeignTradeMissing("Force_payload");
                var native = state.NativeArmySimulation ?? throw ForeignTradeMissing("native_owner");
                var force = native.Forces.GetValueOrDefault(physical) ?? throw ForeignTradeMissing("Force_owner");
                var trade = force.ForeignTradeProgress ?? throw ForeignTradeMissing("Force_trade_progress");
                if (trade.FriendshipPoints.Length != 5 || trade.AcquiredTechnologyLevels.Length != 5)
                    throw ForeignTradeMissing("five_trade_slots");
                int level = trade.AcquiredTechnologyLevels[country];
                int floor = level switch
                {
                    0 => Parameter("unacquired-friendship-floor", null),
                    1 => Parameter("first-friendship-floor", 1),
                    2 => Parameter("second-friendship-floor", 150),
                    3 => Parameter("third-friendship-floor", 300),
                    4 => 0,
                    _ => Parameter("other-friendship-floor", 500)
                };
                int reduced = unchecked(trade.FriendshipPoints[country] - Parameter("seasonal-friendship-decay", 5));
                //2F5DD0 compares the signed intermediate with the acquired
                //technology floor before its final native0..1000 setter.
                int requested = reduced < floor ? floor : Math.Min(reduced, 1000);
                ushort after = unchecked((ushort)Math.Clamp(requested, 0, 1000));
                state = state with { NativeArmySimulation = native with { Forces = new(native.Forces)
                { [physical] = force with { ForeignTradeProgress = trade with
                    { FriendshipPoints = trade.FriendshipPoints.SetItem(country, after) } } } } };
            }
        }
        return state;

        int Parameter(string name, int? initial)
        {
            string key = "simulation.foreign-trade." + name;
            var bank = state.NativeArmySimulation?.Parameters ?? throw ForeignTradeMissing("parameter_owner");
            if (bank.Float32Values.ContainsKey(key)) throw ForeignTradeMissing("integer_parameter_" + name);
            return bank.Int32Values.TryGetValue(key, out int value) ? value : initial ?? throw ForeignTradeMissing("current_" + name);
        }
    }

    private static InvalidDataException ForeignTradeMissing(string field) => new("normal.turn.foreign-trade." + field);
}
