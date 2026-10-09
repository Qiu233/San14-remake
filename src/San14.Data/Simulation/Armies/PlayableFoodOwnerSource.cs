using System.Collections.ObjectModel;
using San14.Core;

namespace San14.Data;

/// <summary>Current 2DCA60/2DCDB0 food receivers. This resolves ownership only;
/// expense math, manager60 exclusion, logistics and source timing are separate.</summary>
public static class PlayableFoodOwnerSource
{
    public const string RuleId = "CE-current-food-owner-2DCA60-2DCDB0-v1";

    public static IReadOnlyDictionary<int, PlayableFoodPayerResolution> CaptureFoodOwners(
        GameState state, SourceWorldContext? current)
    {
        ArgumentNullException.ThrowIfNull(state);
        var result = new Dictionary<int, PlayableFoodPayerResolution>();
        for (int location = 1; location <= 61; location++)
            result.Add(location, Resolve(state, current, location));
        return new ReadOnlyDictionary<int, PlayableFoodPayerResolution>(result);
    }

    private static PlayableFoodPayerResolution Resolve(GameState state,
        SourceWorldContext? current, int location)
    {
        if (current is null)
            return Unknown("Current Base/map food-owner capture is unresolved.");
        BaseReference home;
        BaseReference? first;
        int? garrison;
        try
        {
            home = BaseLookup.Find(location, current);
            first = CurrentGateMapQueries.FindCity(home, current);
            garrison = ReadValidCity(state, current, first);
        }
        catch (InvalidDataException error) { return Unknown(error.Message); }

        //A known invalid first City is not a real collector and cannot charge
        //its HomeGate Armies. Do not turn this branch into a guessed City0.
        if (!garrison.HasValue)
            return new(null, null, null, null, RuleId);
        if (home.Kind == BaseKind.City)
            return new(garrison, garrison, null, null, RuleId);

        try
        {
            //22D110 assigns HomeGate to its first mapped City. 2DCDB0 then
            //charges that collector City's own22A940 receiver, a second query.
            BaseReference payer = CurrentGateMapQueries.FindCity(first, current);
            return new(garrison, ReadValidCity(state, current, payer), null, null, RuleId);
        }
        catch (InvalidDataException error)
        { return new(garrison, null, null, error.Message, RuleId); }
    }

    private static int? ReadValidCity(GameState state, SourceWorldContext current,
        BaseReference city)
    {
        bool valid = current.ReadBaseNativeValidity(city)
            ?? throw new InvalidDataException("Current food receiver City native validity is unknown.");
        if (!valid) return null;
        int physical = city.PhysicalSlot
            ?? throw new InvalidDataException("Current food receiver City pointer is unknown.");
        if (city.Kind != BaseKind.City || physical is < 1 or > 51 ||
            !state.Cities.ContainsKey(physical))
            throw new InvalidDataException("Current food receiver is absent from the campaign City owner.");
        return physical;
    }

    private static PlayableFoodPayerResolution Unknown(string owner) =>
        new(null, null, owner, owner, RuleId);
}

public sealed partial class ArmyCampaignRuleAdapter : IPlayableFoodOwnerRules
{
    public IReadOnlyDictionary<int, PlayableFoodPayerResolution> CaptureFoodOwners(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return PlayableFoodOwnerSource.CaptureFoodOwners(state, CaptureCurrentDefinitions(state));
    }
}
