using System.Collections.ObjectModel;
using San14.Core;

namespace San14.Data;

public sealed record ArmyMapPersonalitiesRow(int ArmyId,
    IReadOnlyList<ArmyDeparturePersonalitySnapshot> Personalities, string? UnresolvedInput);

/// <summary>Immutable values for one actual World. No mutable query context
/// escapes the capture or survives as a cache behind this publication.</summary>
public sealed record ArmyMapPersonalitiesPublication(GameState World,
    IReadOnlyDictionary<int, ArmyMapPersonalitiesRow> Rows, string? UnresolvedInput);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The label/model refresh already identifies its Army set. All
    /// requested personality rows borrow one current capture for that World.</summary>
    public ArmyMapPersonalitiesPublication QueryArmyMapPersonalitiesBatch(GameState state, IEnumerable<int> armyIds)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(armyIds);
        var rows = new Dictionary<int, ArmyMapPersonalitiesRow>();
        var armies = new List<UnitState>();
        foreach (int id in armyIds.Distinct().Order())
        {
            try { armies.Add(RequireArmyMapPersonalitySource(state, id)); }
            catch (InvalidDataException error) { rows.Add(id, new(id, [], error.Message)); }
        }
        if (armies.Count != 0)
        {
            SourceWorldContext current;
            try { current = CaptureOwnedArmyReturnWorld(state).Context; }
            catch (InvalidDataException error) { return Publish(error.Message); }
            foreach (var army in armies)
            {
                try
                {
                    var values = Array.AsReadOnly(CaptureDeparturePersonalities(state, current, army));
                    rows.Add(army.Id, new(army.Id, values, null));
                }
                catch (InvalidDataException error) { rows.Add(army.Id, new(army.Id, [], error.Message)); }
            }
        }
        return Publish(null);

        ArmyMapPersonalitiesPublication Publish(string? missing) =>
            new(state, new ReadOnlyDictionary<int, ArmyMapPersonalitiesRow>(rows), missing);
    }

    private static UnitState RequireArmyMapPersonalitySource(GameState state, int armyId)
    {
        if (!state.Units.TryGetValue(armyId, out var army) ||
            army.NativeArmyOrigin?.NativeSlotIndex is not int physical || physical is < 1 or > 500 ||
            state.ReadArmyLifecycle()?.ReceiverSlots()[physical] is not { ActiveUnitId: int active } || active != armyId)
            throw DepartureMissing("personality_refresh_active_receiver");
        return army;
    }
}
