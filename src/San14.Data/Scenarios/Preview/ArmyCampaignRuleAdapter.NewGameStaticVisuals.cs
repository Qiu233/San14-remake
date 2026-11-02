using San14.Core;

namespace San14.Data;

/// <summary>The reached normal-load Base postload dependency projection.
/// Missing morale outputs remain unknown; they are not primary-model inputs.</summary>
public sealed record NewGameBaseVisualPostLoad(int Durability, int Capacity, byte Flag66, int Force,
    byte? Morale44 = null, int? MoraleCapacity = null);

/// <summary>Actual World80 order and Object4E selection. Position belongs to
/// the subsequently created native primary Unit, not a Hex-center substitute.</summary>
public sealed record NewGameFootholdVisualMarker(BaseReference Base, ushort ObjectLink, int Force);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The normal static load's scoped visual world. Reuses its
    /// current source identities, then publishes only reached visual fields
    /// into this operation-local context. The GameState is never changed.</summary>
    public SourceWorldContext CaptureNewGameStaticVisualDefinitions(GameState state)
    {
        var current = CaptureNewGameStaticObjectVisualDefinitions(state);
        var options = state.NewGameOptions ?? throw NewGameStaticVisualMissing("options");
        var variants = CeCityVariantTable.LoadCurrent(_scenario.Snapshot.Locale);
        var opening = CityOpeningPostLoadSource.LoadSourceCreatedOrdinary(_repository, _scenario, _map, options, variants);
        var parameters = RenderSamplerSourceAdapters.LoadCityOpeningPostLoadNative(
            _repository.SourceDirectory, _scenario.Snapshot.Locale).BaseParameters;

        foreach (var row in opening.Cities.Values)
        {
            var target = new BaseReference(BaseKind.City, row.Slot);
            ushort link = NewGameStaticVisualNeed(current.ReadBaseObjectLink(target), "City4E");
            int force = NewGameStaticVisualNeed(current.ReadBaseForceSlot(target), "CityForce");
            ushort raw = NewGameStaticVisualNeed(current.ReadObjectRawField14(new(link)), "CityObject14");
            if (link != row.ObjectSlot || raw != row.SourceDurability || force != row.ForcePhysicalSlot ||
                current.ReadCitySizeB8(target) != row.SizeCode)
                throw NewGameStaticVisualMissing("changed-source-City-binding:" + row.Slot);
            current.SetObjectRawField14(link, checked((ushort)row.Durability));
            current.SetBasePostLoadFlag66(target, checked((byte)row.DamageTier));
            current.PublishNewGameBaseVisualPostLoad(target, new(row.Durability, row.Capacity,
                checked((byte)row.DamageTier), force));
        }
        foreach (var gate in _scenario.Snapshot.Gates.Where(row => row.Slot != 0))
        {
            var target = new BaseReference(BaseKind.Gate, gate.Slot);
            if (!NewGameStaticVisualNeed(current.ReadBaseNativeValidity(target), "Gate-validity"))
                throw NewGameStaticVisualMissing("invalid-source-Gate:" + gate.Slot);
            ushort link = NewGameStaticVisualNeed(current.ReadBaseObjectLink(target), "Gate4E");
            if (link != gate.LocationSlot) throw NewGameStaticVisualMissing("Gate-Object-identity");
            if (!NewGameStaticVisualNeed(current.ReadObjectNativeValidity(new(link)), "Gate-Object-validity"))
                throw NewGameStaticVisualMissing("invalid-source-Gate-Object:" + gate.Slot);
            int force = NewGameStaticVisualNeed(current.ReadBaseForceSlot(target), "GateForce");
            ushort raw = NewGameStaticVisualNeed(current.ReadObjectRawField14(new(link)), "GateObject14");
            int capacity = Math.Min((int)NewGameStaticVisualNeed(current.ReadBaseField46(target), "Gate46"), 9999);
            // The same normal source E58=1 was admitted by the City opening
            // producer above: the advanced percentage branch is not reached.
            int durability = Math.Min((int)raw, capacity);
            byte tier = CityOpeningPostLoadSource.ReadFlag66(durability, capacity, parameters);
            current.SetObjectRawField14(link, checked((ushort)durability));
            current.SetBasePostLoadFlag66(target, tier);
            current.PublishNewGameBaseVisualPostLoad(target, new(durability, capacity, tier, force));
        }
        return current;
    }

    /// <summary>Actual Base registry order for the source map-label builder.
    /// Callers pair these bindings with native Unit18 after model creation.</summary>
    public IReadOnlyList<NewGameFootholdVisualMarker> CaptureNewGameFootholdVisualMarkers(GameState state) =>
        ReadNewGameFootholdVisualMarkers(CaptureNewGameStaticObjectVisualDefinitions(state));

    /// <summary>Reads map-label bindings from the same operation's prepared
    /// static current. Visual postload changes durability and damage tier,
    /// leaving this registry, validity, Object link and Force ownership intact.</summary>
    public IReadOnlyList<NewGameFootholdVisualMarker> ReadNewGameFootholdVisualMarkers(SourceWorldContext current)
    {
        var markers = new List<NewGameFootholdVisualMarker>();
        foreach (var target in current.ReadRegisteredBases() ?? throw NewGameStaticVisualMissing("Base-registry80"))
        {
            if (target is null) throw NewGameStaticVisualMissing("Base-registry-pointer");
            if (!NewGameStaticVisualNeed(current.ReadBaseNativeValidity(target), "marker-Base-validity")) continue;
            ushort link = NewGameStaticVisualNeed(current.ReadBaseObjectLink(target), "marker-Object4E");
            if (link >= 52) continue;
            int force = NewGameStaticVisualNeed(current.ReadBaseForceSlot(target), "marker-Force");
            if (force == 0) continue;
            markers.Add(new(target, link, force));
        }
        return markers.AsReadOnly();
    }

    private SourceWorldContext CaptureNewGameStaticObjectVisualDefinitions(GameState state)
    {
        var current = CaptureNewGameVisualDefinitions(state);
        // Invert only the normal loader's documented zero-to-null domain IDs,
        // with the same source District0 and actual Object13 ownership proof.
        foreach (var row in _scenario.Snapshot.Cities.Where(row => row.Slot != 0 && row.DistrictSlot == 0))
            if (state.Cities.TryGetValue(row.Slot, out var city) && city.DistrictId is null && city.ForceId is null &&
                current.ReadObjectForceSlot13(new(row.Slot)) == 0)
                current.SetCityDistrictSlot(row.Slot, 0);
        current.BindNewGameStaticVisualWorldHeader();
        return current;
    }
    private static T NewGameStaticVisualNeed<T>(T? value, string field) where T : struct =>
        value ?? throw NewGameStaticVisualMissing(field);
    private static InvalidDataException NewGameStaticVisualMissing(string field) => new("new-game.static-visual." + field);
}

public sealed partial class SourceWorldContext
{
    private readonly Dictionary<BaseReference, NewGameBaseVisualPostLoad> _newGameBaseVisualPostLoad = [];
    internal void PublishNewGameBaseVisualPostLoad(BaseReference target, NewGameBaseVisualPostLoad result) =>
        _newGameBaseVisualPostLoad.Add(target, result);
    public NewGameBaseVisualPostLoad ReadNewGameBaseVisualPostLoad(BaseReference target) =>
        _newGameBaseVisualPostLoad.GetValueOrDefault(target)
        ?? throw new InvalidDataException("new-game.static-visual.Base-postload-not-published");

    internal void BindNewGameStaticVisualWorldHeader()
    {
        // Logical singleton identity within this capture, not a player ID or
        // epoch. Normal static loading resets this exact World header's3A;
        // its static serializer omits3A before unit-model creation.
        const int headerIdentity = 0;
        StaticCompletionWorld = new(headerIdentity);
        ResetWorldForceSlot3A(headerIdentity);
    }
}
