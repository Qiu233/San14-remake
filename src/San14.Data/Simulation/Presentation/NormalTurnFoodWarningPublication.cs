using San14.Core;
using System.Collections.Immutable;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The saved warning identity and its actual current map receiver.
/// A publication changes neither the source C9 bit nor the food calculation.</summary>
public sealed record NormalTurnFoodWarningMarker(int BaseLocationId, int MarkerLocationId,
    int HexId, string Name);
public sealed record NormalTurnFoodWarningPublication(string Text,
    IReadOnlyList<NormalTurnFoodWarningMarker> Markers)
{
    public bool ClearsPreviousWarnings => true;
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>2FE2B0's persistent map tooltip refresh, not a narration box.
    /// The renderer clears its prior warnings even for an empty source list.</summary>
    public AreaAdministrationQuery<NormalTurnFoodWarningPublication> QueryNormalTurnFoodWarnings(
        GameState state, NormalTurnFoodWarningPresentationRequest request)
    {
        try
        {
            if (request.TextAtSource is null || request.WarningBaseLocationIds.IsDefault)
                throw FoodWarningPublicationMissing("request");
            if (request.WarningBaseLocationIds.IsEmpty)
                return AreaAdministrationQuery<NormalTurnFoodWarningPublication>.Known(new(request.TextAtSource, []));
            var current = CaptureFoodWarningCurrent(state);
            return AreaAdministrationQuery<NormalTurnFoodWarningPublication>.Known(
                ReadFoodWarningMarkers(state, current, request.TextAtSource, request.WarningBaseLocationIds));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<NormalTurnFoodWarningPublication>.Unknown(error.Message); }
    }

    /// <summary>1A1470 restores an existing C9 warning when a map layout is
    /// created after load. This reads its saved bit, never recalculates food.</summary>
    public AreaAdministrationQuery<NormalTurnFoodWarningPublication> QueryCurrentNormalTurnFoodWarnings(GameState state)
    {
        try
        {
            var current = CaptureFoodWarningCurrent(state);
            var locations = ImmutableArray.CreateBuilder<int>();
            foreach (var entry in current.ReadRegisteredBases() ?? throw FoodWarningPublicationMissing("registered_Bases"))
            {
                var receiver = entry ?? throw FoodWarningPublicationMissing("Base_pointer");
                if (!(current.ReadBaseNativeValidity(receiver) ?? throw FoodWarningPublicationMissing("Base_validity"))) continue;
                int location = current.ReadBaseObjectLink(receiver) ?? throw FoodWarningPublicationMissing("Base4E");
                if (location is < 1 or > 51) continue;
                var city = current.ReadCityTableReference(location) ?? throw FoodWarningPublicationMissing("City_lookup");
                int physical = city.PhysicalSlot ?? throw FoodWarningPublicationMissing("City_pointer");
                byte flags = state.NativeBaseCaptureCurrent?.Cities.GetValueOrDefault(physical)?.C9
                    ?? throw FoodWarningPublicationMissing("saved_CityC9");
                if ((flags & 2) != 0) locations.Add(location);
            }
            if (locations.Count == 0)
                return AreaAdministrationQuery<NormalTurnFoodWarningPublication>.Known(new(string.Empty, []));
            var vm = new NarrationResourceMessages(_repository.LoadMessages(Seed.Language, MessageKind.Game).Messages);
            return AreaAdministrationQuery<NormalTurnFoodWarningPublication>.Known(
                ReadFoodWarningMarkers(state, current, vm.ResolveNormalTurnFoodWarningLabel(), locations));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<NormalTurnFoodWarningPublication>.Unknown(error.Message); }
    }

    private SourceWorldContext CaptureFoodWarningCurrent(GameState state)
    {
        var current = CaptureDeploymentPersonSelectionCurrent(state);
        current.RestoreOwnedArmyReturnSlots(state, state.ReadArmyLifecycle()
            ?? throw FoodWarningPublicationMissing("base_lifecycle"));
        return current;
    }

    private static NormalTurnFoodWarningPublication ReadFoodWarningMarkers(GameState state,
        SourceWorldContext current, string text, IEnumerable<int> locations)
    {
        var markers = new List<NormalTurnFoodWarningMarker>();
        foreach (int location in locations)
        {
            if (location is < 1 or > 61) throw FoodWarningPublicationMissing("location");
            var receiver = BaseLookup.Find(location, current);
            if (!(current.ReadBaseNativeValidity(receiver) ?? throw FoodWarningPublicationMissing("Base_validity")))
                throw FoodWarningPublicationMissing("Base_receiver");
            int physical = receiver.PhysicalSlot ?? throw FoodWarningPublicationMissing("Base_identity");
            int markerId = receiver.Kind == BaseKind.City ? physical : physical + 51;
            string name = receiver.Kind == BaseKind.City
                ? state.Cities.GetValueOrDefault(physical)?.Name ?? throw FoodWarningPublicationMissing("City_name")
                : state.Gates.GetValueOrDefault(markerId)?.Name ?? throw FoodWarningPublicationMissing("Gate_name");
            int hex = current.ReadBaseCurrentHexIndex(receiver);
            if ((uint)hex >= 48400) throw FoodWarningPublicationMissing("Base_hex");
            markers.Add(new(location, markerId, hex, name));
        }
        return new(text, markers.AsReadOnly());
    }

    private static InvalidDataException FoodWarningPublicationMissing(string field) => new("normal-turn.food-warning.publication." + field);
}
