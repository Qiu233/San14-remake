using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Current Person second-location, surface and home-grain icon
    /// for ordinary new target rows, in caller order (including duplicates).
    /// Captures once; does not create an Army, change selection or find a route.</summary>
    public IReadOnlyList<DeploymentTargetCard> QueryDeploymentTargetCards(GameState state,
        IReadOnlyList<int> officerIds) => QueryDeploymentTargetCardsCore(state, officerIds, null);

    /// <summary>Also selects each draft row's actual water/land formation icon
    /// using the same current capture. Rows remain independent even when their
    /// commanders are equal.</summary>
    public IReadOnlyList<DeploymentTargetCard> QueryDeploymentTargetCards(GameState state,
        IReadOnlyList<DeploymentDraftRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return QueryDeploymentTargetCardsCore(state, rows.Select(row => row.Request.OfficerId).ToArray(), rows);
    }

    private IReadOnlyList<DeploymentTargetCard> QueryDeploymentTargetCardsCore(GameState state,
        IReadOnlyList<int> officerIds, IReadOnlyList<DeploymentDraftRow>? rows)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(officerIds);
        if (officerIds.Count == 0) return Array.Empty<DeploymentTargetCard>();
        SourceWorldContext current;
        try
        {
            current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, _definitions);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            BaseCaptureCurrentInputs.BindCurrent(state, current);
            if (state.ReadArmyLifecycle() is { } arena)
            {
                _ = WorldIndexContext.ImportOwnedState(current, arena.Indexes, state);
                current.RestoreOwnedArmyReturnSlots(state, arena);
            }
        }
        catch (InvalidDataException error)
        {
            return officerIds.Select(id => new DeploymentTargetCard(id, null, null, null, null,
                error.Message, error.Message, error.Message) { PersonUnresolvedInput = error.Message,
                    FormationUnresolvedInput = rows is null ? null : error.Message }).ToArray();
        }
        return officerIds.Select(ReadCard).ToArray();

        DeploymentTargetCard ReadCard(int id, int ordinal)
        {
            bool? hasHex = null, naval = null;
            int? hexSlot = null, palette = null;
            string? positionMissing = null, surfaceMissing = null, grainMissing = null;
            bool? personValid = null;
            string? personMissing = null;
            int? personPhysical = null;
            try { personPhysical = ReadPerson(); personValid = personPhysical.HasValue; }
            catch (InvalidDataException error) { personMissing = error.Message; }
            HexReference? hex = null;
            try
            {
                hex = ReadPosition(); hasHex = hex?.PhysicalSlot.HasValue == true;
                hexSlot = hex?.PhysicalSlot;
            }
            catch (InvalidDataException error) { positionMissing = error.Message; }
            if (positionMissing is not null) surfaceMissing = positionMissing;
            else if (hasHex == true)
            {
                try
                {
                    byte surfaceCode = TargetCardNeed(current.ReadHexSurfaceCode(hex!), "hex_surface");
                    var surface = current.ReadSurfaceTypeTableReference(surfaceCode <= 30 ? surfaceCode : 0)
                        ?? throw TargetCardMissing("surface_pointer");
                    int physical = surface.PhysicalSlot ?? throw TargetCardMissing("null_surface_receiver");
                    byte flags = TargetCardNeed(PersonArmyCaptureInputs.ReadTerrainFlags2C(state, physical), "current_surface_flags");
                    naval = (flags & 2) != 0;
                }
                catch (InvalidDataException error) { surfaceMissing = error.Message; }
            }
            try { palette = ReadGrainPalette(); }
            catch (InvalidDataException error) { grainMissing = error.Message; }
            ArmyMapIconSelection? icon = null;
            string? formationMissing = rows is null ? TargetCardMissing("draft_formations").Message : surfaceMissing;
            if (rows is not null && naval.HasValue)
            {
                try
                {
                    var profile = state.NativeArmySimulation?.Parameters;
                    if (profile is null || profile.Language != Seed.Language ||
                        profile.RuleVersion != SimulationRuleIdentity.ForLocale(Seed.Language))
                        throw TargetCardMissing("formation_profile");
                    var row = rows[ordinal];
                    int requested = naval.Value ? row.NavalFormationId : row.Request.FormationId;
                    if (requested < 0) throw TargetCardMissing("negative_formation_index");
                    int physical = requested <= 20 ? requested : 0;
                    // The fixed normal table's physical0 has native validity
                    // false. The original icon writer leaves its node alone.
                    if (physical != 0)
                    {
                        byte kind = TargetCardNeed(TacticRemakeCurrentFormations.ReadRawByte(state, physical, 0x7E), "current_formation_kind");
                        icon = ArmyMapUiSources.FormationIcon((byte)physical, kind);
                    }
                }
                catch (InvalidDataException error) { formationMissing = error.Message; }
            }
            return new(id, hasHex, hexSlot, naval, palette, positionMissing, surfaceMissing, grainMissing)
                { IsPersonValid = personValid, PersonUnresolvedInput = personMissing,
                    FormationIcon = icon, FormationUnresolvedInput = formationMissing };

            int? ReadPerson()
            {
                if (id < 0) throw TargetCardMissing("negative_person_index");
                var person = current.ReadPersonLookupReference(id <= 6000 ? id : 0)
                    ?? throw TargetCardMissing("person_pointer");
                if (person.PhysicalSlot is not int physical) return null;
                return TargetCardNeed(current.ReadPersonNativeValidity(physical), "person_validity") ? physical : null;
            }
            NativeLocation? ReadLocation()
            {
                if (personMissing is not null) throw new InvalidDataException(personMissing);
                return personPhysical is int physical ? PersonPositionRepair.DecodeSecondLocation(
                    TargetCardNeed(current.ReadPersonSecondLocation(physical), "person_second_location")) : null;
            }
            BaseReference? ReadBase(NativeLocation? location)
            {
                BaseReference? receiver = location?.Kind switch
                {
                    5 => current.ReadCityTableReference(location.Value.Index),
                    6 => current.ReadGateTableReference(location.Value.Index),
                    _ => new(BaseKind.City, null)
                };
                if (receiver is null) throw TargetCardMissing("base_pointer");
                return TargetCardNeed(current.ReadBaseNativeValidity(receiver), "base_validity") ? receiver : null;
            }
            HexReference? ReadPosition()
            {
                var location = ReadLocation();
                if (location?.Kind is 5 or 6)
                    return ReadBase(location) is { } receiver ? CurrentGateMapQueries.FindHex(receiver, current) : null;
                if (location?.Kind != 27) return null;
                if (state.ReadArmyLifecycle() is null) throw TargetCardMissing("army_saved_owner");
                var army = current.ReadArmyUnitTableReference(location.Value.Index) ?? throw TargetCardMissing("army_pointer");
                if (!TargetCardNeed(current.ReadArmyUnitNativeValidity(army), "army_validity")) return null;
                ushort raw = TargetCardNeed(current.ReadArmyUnitRawHex2A(army), "army_hex");
                return current.ReadHexTableReference(raw <= 48399 ? raw : 0) ?? throw TargetCardMissing("army_hex_pointer");
            }
            int ReadGrainPalette()
            {
                // Only City/Gate can supply the ordinary new row's home grain.
                // Known invalid receivers keep11 without reaching Area or C9.
                if (ReadBase(ReadLocation()) is not { } receiver) return 11;
                var city = CurrentGateMapQueries.FindCity(receiver, current);
                if (!TargetCardNeed(current.ReadBaseNativeValidity(city), "grain_city_validity")) return 11;
                int physical = city.PhysicalSlot ?? throw TargetCardMissing("grain_city_receiver");
                return (TargetCardNeed(current.ReadCaptureCityByteC9(physical), "current_city_grain_warning") & 2) != 0 ? 61 : 11;
            }
        }
    }

    private static T TargetCardNeed<T>(T? value, string field) where T : struct => value ?? throw TargetCardMissing(field);
    private static InvalidDataException TargetCardMissing(string field) => new("deployment.target_card." + field);
}
