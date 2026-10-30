using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Ordinary City expansion, its occurrence query and explicit
    /// result registration, using the same invocation/current and manager.
    /// No editor growth gate, automatic development or inferred result facts.</summary>
    internal (GameState State, int NativeReturn) ApplyStoryCityBusinessCurrent(
        GameState state, SourceWorldContext current, int code, IReadOnlyList<int> arguments,
        BaseReference? receiver, Action<BaseReference> recordTroopIncrease)
    {
        if (arguments.Count != 8) throw StoryCityBusinessMissing("operands");
        if (code == 247)
        {
            // The source's five pair table has two unused zero pairs followed
            // by [63,108], [109,154], [155,200]. Query actual occurred bits.
            (int first, int last) = arguments[0] switch
            {
                2 => (63, 108), 3 => (109, 154), 4 => (155, 200), _ => (0, -1)
            };
            for (int eventId = first; eventId <= last; eventId++)
                if (StoryCityBusinessNeed(current.ReadWorldEventFlag(eventId), "expansion_event_occurrence"))
                    return (state, 1);
            return (state, 0);
        }
        if (code is not (152 or 365)) throw StoryCityBusinessMissing("opcode");
        var city = receiver ?? throw StoryCityBusinessMissing("City_selector");
        if (city.Kind != BaseKind.City) throw StoryCityBusinessMissing("City_receiver_kind");
        bool valid = StoryCityBusinessNeed(current.ReadBaseNativeValidity(city), "City_validity");
        if (code == 365)
        {
            if (!valid) return (state, 0);
            // This is the source's explicit manager F0 append, not a test of
            // whether the City's troops changed. Preserve order and repeats.
            recordTroopIncrease(city);
            return (state, 1);
        }
        if (!valid) return (state, 1);
        int physical = StoryCityBusinessNeed(city.PhysicalSlot, "City_pointer");
        int requested = arguments[1], size = Math.Clamp(requested, 0, 4);
        var owner = state.Cities.GetValueOrDefault(physical) ?? throw StoryCityBusinessMissing("City_owner");
        // CitySize.None=-1 represents actual raw B8=0. Other enum identities
        // stay unchanged, so the existing capacity/current binding adds one.
        current.SetCitySizeB8(physical, (byte)size);
        state = state with { Cities = new(state.Cities)
            { [physical] = owner with { Size = (CitySize)(size - 1) } } };
        int rawForce = StoryCityBusinessNeed(current.ReadBaseForceSlot(city), "City_Force");
        var force = current.ReadForceTableReference(rawForce < 52 ? rawForce : 0)
            ?? throw StoryCityBusinessMissing("City_Force_pointer");
        if (StoryCityBusinessNeed(current.ReadForceNativeValidity(force), "City_Force_validity"))
        {
            int slot = StoryCityBusinessNeed(current.ReadForcePhysicalSlot(force), "City_Force_identity");
            if (slot != 0 && slot is not (>= 46 and <= 51) && requested is 2 or 3 or 4)
                state = RulerCaptureReleaseSource.ApplyCurrentPopularityAdjustment(state, current, slot,
                    ReadEditedEventExpansionParameter(state, requested, false));
        }
        return (state, 1);
    }

    private static T StoryCityBusinessNeed<T>(T? value, string field) where T : struct
        => value ?? throw StoryCityBusinessMissing(field);
    private static InvalidDataException StoryCityBusinessMissing(string field)
        => new("normal-turn.story-city-business." + field);
}
