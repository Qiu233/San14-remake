using System.Collections.ObjectModel;
using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed record DeploymentCancellationRow(ArmyReceiverId SourceReceiver, int PhysicalSlot, int? UnitId,
    int? CommanderId, string? Name, string? TroopsText, int? FormationId, int? Morale,
    int? MaximumMorale, int? SupplyPalette, OfficerPortraitRoute? Portrait,
    IReadOnlyDictionary<string, string> UnresolvedFields)
{
    public ArmyMapIconSelection? FormationIcon { get; init; }
}

public sealed record DeploymentCancellationPage(int CityId, IReadOnlyList<DeploymentCancellationRow> Rows,
    string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The ordinary city cancellation popup. Its fixed receiver-table
    /// order is independent of the active Army registry and of old deployment drafts.</summary>
    public DeploymentCancellationPage QueryDeploymentCancellation(GameState state, int cityId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var rows = new List<DeploymentCancellationRow>();
        try
        {
            if (cityId is < 1 or > 51) throw DeploymentCancellationMissing("city_identity");
            var captured = CaptureUnitDetailsCurrent(state);
            var current = captured.Current ?? throw new InvalidDataException(captured.Missing
                ?? DeploymentCancellationMissing("current").Message);
            var city = current.ReadCityTableReference(cityId) ?? throw DeploymentCancellationMissing("city_pointer");
            var slots = (state.ReadArmyLifecycle() ?? throw DeploymentCancellationMissing("receiver_owner")).ReceiverSlots();
            if (slots.Count != 501) throw DeploymentCancellationMissing("receiver_table");
            for (int physical = 0; physical < 501; physical++)
            {
                if (!IsCityDeploymentCancellationCandidateCurrent(state, current, city, physical)) continue;
                var slot = slots[physical];
                var image = slot.ActiveUnitId is int active ? state.Units.GetValueOrDefault(active) : slot.PendingImage;
                if (image is null) throw DeploymentCancellationMissing("candidate_image");
                var army = new ArmyUnitReference(physical);
                var issues = new Dictionary<string, string>();
                int? commander = Value("commander", () => (int)DeploymentCancellationNeed(
                    current.ReadArmyUnitCommanderId(army), "commander12"));
                int Person()
                {
                    int id = commander ?? throw DeploymentCancellationMissing("commander12");
                    return DeploymentCancellationNeed((id < 6001 ? current.ReadPersonLookupReference(id)
                        : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "commander_pointer");
                }
                string? name = Reference("name", () =>
                {
                    int person = Person();
                    if (!DeploymentCancellationNeed(current.ReadPersonNativeValidity(person), "name_Person_validity"))
                        throw DeploymentCancellationMissing("name_placeholder_text");
                    int id = DeploymentCancellationNeed(current.ReadPersonIdentity(person), "name_Person_identity");
                    string full = state.Officers.GetValueOrDefault(id)?.Name ?? throw DeploymentCancellationMissing("current_Person_name");
                    string format = ReadHistoryDataText(1, 0x331);
                    if (!format.Contains("%s", StringComparison.Ordinal)) throw DeploymentCancellationMissing("name_format331");
                    return format.Replace("%s", full, StringComparison.Ordinal);
                });
                string? troops = Reference("troops", () => image.NativeArmyOrigin is not null && image.Troops is >= 0 and <= ushort.MaxValue
                    ? image.Troops.ToString(CultureInfo.InvariantCulture) : throw DeploymentCancellationMissing("Army16"));
                int? formation = Value("formation", () => DeploymentCancellationNeed(slot.Status10, "Army10") == 1
                    ? DeploymentCancellationNeed(ArmyConfigurationSourceAdapter.ReadCurrentLand(state, image), "Army1B") : 19);
                ArmyMapIconSelection? formationIcon = Value("formation_icon", () =>
                {
                    int raw = formation ?? throw DeploymentCancellationMissing("formation");
                    byte type = DeploymentCancellationNeed(TacticRemakeCurrentFormations.ReadRawByte(state,
                        raw <= 20 ? raw : 0, 0x7E), "Formation7E");
                    return ArmyMapUiSources.FormationIcon((byte)raw, type);
                });
                int? maximum = Value("morale_maximum", () =>
                {
                    int force = DeploymentCancellationNeed(current.ReadArmyUnitForceReference(army)?.PhysicalSlot, "Force_receiver");
                    return DeploymentCancellationNeed(captured.Rules?.ForceMoraleMaximum(state, force), "morale_maximum");
                });
                int? morale = Value("morale", () => Math.Min(DeploymentCancellationNeed(ArmyMoraleSourceAdapter.Read(image), "Army1A"),
                    maximum ?? throw DeploymentCancellationMissing("morale_maximum")));
                int? palette = Value("supply_palette", () => ReadUnitDetailSupplyPalette(state, current, image));
                OfficerPortraitRoute? portrait = Reference("portrait", () =>
                {
                    int person = Person();
                    bool valid = DeploymentCancellationNeed(current.ReadPersonNativeValidity(person), "portrait_Person_validity");
                    int id = valid ? DeploymentCancellationNeed(current.ReadPersonIdentity(person), "portrait_Person_identity") : 0;
                    ushort appearance = valid ? DeploymentCancellationNeed(current.ReadPersonPortraitAppearance(person), "portrait_A6") : (ushort)2198;
                    var route = ReadOfficerPortraitAppearance(state, current, _definitions, id, appearance, ArmyMapUiSources.Create());
                    if (route.Kind == OfficerPortraitRouteKind.Unresolved)
                        issues["portrait"] = route.UnresolvedInput ?? DeploymentCancellationMissing("portrait_route").Message;
                    return route;
                });
                rows.Add(new(slot.ReceiverId, physical, slot.ActiveUnitId ?? slot.PendingImage?.Id, commander,
                    name, troops, formation, morale, maximum, palette, portrait, new ReadOnlyDictionary<string, string>(issues))
                    { FormationIcon = formationIcon });

                T? Value<T>(string field, Func<T> read) where T : struct
                { try { return read(); } catch (InvalidDataException error) { issues[field] = error.Message; return null; } }
                T? Reference<T>(string field, Func<T> read) where T : class
                { try { return read(); } catch (InvalidDataException error) { issues[field] = error.Message; return null; } }
            }
            return new(cityId, rows.AsReadOnly(), null);
        }
        catch (InvalidDataException error) { return new(cityId, rows.AsReadOnly(), error.Message); }
    }

    //6DE160: only these reached fields admit a receiver. The formal single-row
    //consumer reuses this helper with its existing transaction-local capture.
    internal static bool IsCityDeploymentCancellationCandidateCurrent(GameState state,
        SourceWorldContext current, BaseReference city, int physical)
    {
        if (physical is < 0 or > 500) throw DeploymentCancellationMissing("receiver_physical");
        var army = new ArmyUnitReference(physical);
        if (!DeploymentCancellationNeed(current.ReadArmyUnitNativeValidity(army), "Army_validity")) return false;
        ushort hex = DeploymentCancellationNeed(current.ReadArmyUnitRawHex2A(army), "Army2A");
        if (hex != current.ReadBaseCurrentHexIndex(city)) return false;
        var slots = (state.ReadArmyLifecycle() ?? throw DeploymentCancellationMissing("receiver_owner")).ReceiverSlots();
        if ((uint)physical >= (uint)slots.Count || slots[physical].PhysicalSlot != physical)
            throw DeploymentCancellationMissing("receiver_identity");
        var slot = slots[physical];
        var image = slot.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) : slot.PendingImage;
        return DeploymentCancellationNeed(image?.NativeMapUiByte13C ?? slot.MapUiByte13C, "Army13C") == 0;
    }

    private static T DeploymentCancellationNeed<T>(T? value, string field) where T : struct
        => value ?? throw DeploymentCancellationMissing(field);
    private static InvalidDataException DeploymentCancellationMissing(string field)
        => new("deployment-cancellation." + field);
}
