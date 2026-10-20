using System.Collections.ObjectModel;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>85A230's current invalid/hide gate and the proved bank10
    /// stock route. The88-person appearance scan is lazy and ordered. One
    /// capture is shared by the batch; no world or resource owner is written.</summary>
    public IReadOnlyDictionary<int, OfficerPortraitRoute> QueryOfficerPortraitRoutes(GameState state,
        IReadOnlyList<int> officerIds)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(officerIds);
        var results = new Dictionary<int, OfficerPortraitRoute>();
        if (officerIds.Count == 0) return new ReadOnlyDictionary<int, OfficerPortraitRoute>(results);
        SourceWorldContext? current = null; string? captureFailure = null;
        try { current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, _definitions); }
        catch (InvalidDataException error) { captureFailure = error.Message; }
        var naming = ArmyMapUiSources.Create();
        foreach (int officerId in officerIds.Distinct())
        {
            ushort? appearance = null; int? matched = null;
            try
            {
                if (current is null) throw new InvalidDataException(captureFailure!);
                var person = state.Officers.GetValueOrDefault(officerId) ?? throw PortraitRouteMissing("current_person_owner");
                if (officerId is < 0 or > 6000 || current.ReadPersonLookupReference(officerId)?.PhysicalSlot is not int physical ||
                    _definitions.ResolveOfficerSource(officerId) != physical || current.ReadPersonIdentity(physical) != officerId)
                    throw PortraitRouteMissing("current_person_identity");
                bool valid = current.ReadPersonNativeValidity(physical) ?? throw PortraitRouteMissing("current_person_validity");
                if (!valid)
                {
                    results[officerId] = new(officerId, OfficerPortraitRouteKind.Hidden, null, null, null, null, null, null);
                    continue;
                }
                appearance = person.PortraitAppearanceIndex ?? throw PortraitRouteMissing("current_appearance");
                if (current.ReadPersonPortraitAppearance(physical) != appearance)
                    throw PortraitRouteMissing("current_appearance_binding");
                results[officerId] = ReadOfficerPortraitAppearance(state, current, _definitions, officerId, appearance.Value, naming);
            }
            catch (InvalidDataException error)
            {
                results[officerId] = new(officerId, OfficerPortraitRouteKind.Unresolved, appearance,
                    null, null, null, matched, error.Message);
            }
        }
        return new ReadOnlyDictionary<int, OfficerPortraitRoute>(results);
    }
    internal static OfficerPortraitRoute ReadOfficerPortraitAppearance(GameState state,
        SourceWorldContext current, ArmyOperationContext definitions, int officerId,
        ushort appearance, ArmyMapUiSources naming)
    {
        int? matched = null;
        try
        {
            int index = appearance;
            //Preserve the outer gate order. These app/DLC/edit owners are
            //not supplied by file existence or by a resource load exception.
            if (index is >= 5000 and <= 6999 && index is not (>= 5106 and <= 5205 or >= 6000 and <= 6214))
                throw PortraitRouteMissing("special_portrait_availability");
            if (index < 1300)
            {
                for (int lookup = 2001; lookup <= 2088; lookup++)
                {
                    var reference = current.ReadPersonLookupReference(lookup) ?? throw PortraitRouteMissing("scan_person_lookup");
                    if (reference.PhysicalSlot is not int at) continue; //Explicit known null pointer.
                    int identity = current.ReadPersonIdentity(at) ?? throw PortraitRouteMissing("scan_person_identity");
                    var owner = state.Officers.GetValueOrDefault(identity) ?? throw PortraitRouteMissing("scan_person_owner");
                    if (definitions.ResolveOfficerSource(identity) != at) throw PortraitRouteMissing("scan_person_owner_binding");
                    ushort observed = owner.PortraitAppearanceIndex ?? throw PortraitRouteMissing("scan_current_appearance");
                    if (current.ReadPersonPortraitAppearance(at) != observed) throw PortraitRouteMissing("scan_appearance_binding");
                    if (observed != index) continue;
                    matched = identity; //Do not filter validity, status or deployment here.
                    if (!OrdinaryPortraitEligibility.IsKnownAllowed(identity, state.CampaignEventWorld?.ScenarioId))
                        throw PortraitRouteMissing(OrdinaryPortraitEligibility.NeedsApplicationSetting(identity)
                            ? "current_portrait_application_setting" : "current_portrait_unlock_pair");
                    break;
                }
            }
            if (index is >= 6000 and <= 6244) throw PortraitRouteMissing("current_dlc_portrait_availability");
            if (index is >= 7000 and <= 7999) throw PortraitRouteMissing("edited_portrait_resource");
            if (index is not (>= 0 and <= 1310 or >= 2000 and <= 2199 or >= 2500 and <= 2509 or
                >= 3000 and <= 3005 or >= 5000 and <= 5399))
                throw PortraitRouteMissing("unregistered_portrait_range");
            var names = naming.PortraitNames(appearance);
            return new(officerId, OfficerPortraitRouteKind.StockNamed, appearance,
                names.ResolvedAppearanceIndex, names.ResourceName, names.SpriteName, matched, null);
        }
        catch (InvalidDataException error)
        {
            return new(officerId, OfficerPortraitRouteKind.Unresolved, appearance,
                null, null, null, matched, error.Message);
        }
    }
    private static InvalidDataException PortraitRouteMissing(string field) => new("officer.portrait." + field);
}
