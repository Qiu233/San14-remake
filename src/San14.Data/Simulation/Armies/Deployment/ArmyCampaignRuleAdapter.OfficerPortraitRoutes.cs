using System.Collections.ObjectModel;
using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>85A230's current invalid/hide gate and the proved bank10
    /// stock route. The88-person appearance scan is lazy and ordered. One
    /// capture is shared by the batch; no world or resource owner is written.</summary>
    public IReadOnlyDictionary<int, OfficerPortraitRoute> QueryOfficerPortraitRoutes(GameState state,
        IReadOnlyList<int> officerIds, int bank = 10)
    {
        if (bank is not (10 or 11)) throw new ArgumentOutOfRangeException(nameof(bank));
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(officerIds);
        var results = new Dictionary<int, OfficerPortraitRoute>();
        if (officerIds.Count == 0) return new ReadOnlyDictionary<int, OfficerPortraitRoute>(results);
        SourceWorldContext? current = null; string? captureFailure = null;
        try { current = CapturePersonPresentationCurrent(state); }
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
                results[officerId] = ReadOfficerPortraitAppearance(state, current, _definitions, officerId, appearance.Value, naming, bank);
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
        ushort appearance, ArmyMapUiSources naming, int bank = 10)
    {
        int? matched = null;
        try
        {
            int index = appearance;
            //Preserve the outer gate order. These app/DLC/edit owners are
            //not supplied by file existence or by a resource load exception.
            if (PortraitContentCatalogue.IsClassified(index))
            {
                var admission = definitions.PortraitResources.LoadPortraitContent().ReadAdmission(index);
                if (!admission.Available) throw new InvalidDataException(admission.UnresolvedInput);
                if (!admission.Value) index = 2000; //931830's known locked fallback.
            }
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
            if (DlcPortraitDescriptor.ForAppearance(index) is not null)
            {
                var repository = definitions.PortraitResources;
                var available = repository.ReadDlcPortraitAvailability(index);
                if (!available.Available) throw new InvalidDataException(available.UnresolvedInput);
                if (!available.Value) index = 2000; //931830's actual archive capability fallback.
                else
                {
                    var descriptor=repository.RequireDlcPortrait(index,bank);
                    var dlc=new ArmyMapPortraitNames(index,index,descriptor.ResourceUri,descriptor.SpriteName(index,bank));
                    return new(officerId, OfficerPortraitRouteKind.StockNamed, appearance,
                        index, dlc.ResourceName, dlc.SpriteName, matched, null) {AdmittedAppearanceIndex=checked((ushort)index)};
                }
            }
            if (index is >= 7000 and <= 7999)
            {
                var repository = definitions.PortraitResources;
                if (!repository.LoadUserPortraits().IsRegistered(index)
                    || !repository.LoadUserPortraits().HasResource(index, bank))
                    index = 2000; //The registration/file gate's explicit source fallback.
                else
                {
                    var user = bank==10 ? repository.ResolveUserFacePortrait(index) : new ArmyMapPortraitNames(index,index,
                        GameResourceId.File(UserPortraitRegistration.RelativePath(index-7000,11)).ToString(),"portrait");
                    return new(officerId, OfficerPortraitRouteKind.StockNamed, appearance,
                        index, user.ResourceName, user.SpriteName, matched, null) {AdmittedAppearanceIndex=checked((ushort)index)};
                }
            }
            if (index is not (>= 0 and <= 1310 or >= 2000 and <= 2199 or >= 2500 and <= 2509 or
                >= 3000 and <= 3005 or >= 5000 and <= 5399 or >= 6000 and <= 6214))
                throw PortraitRouteMissing("unregistered_portrait_range");
            var names = naming.PortraitNames(checked((ushort)index));
            if(bank==11)
            {
                string number=names.ResolvedAppearanceIndex.ToString("D4",CultureInfo.InvariantCulture);
                string alias=names.ResolvedAppearanceIndex!=index?"b":"";
                names=names with {ResourceName="texture_chara_unique_otomo_"+number+alias,
                    SpriteName="face_"+number+alias+"_unique"};
            }
            return new(officerId, OfficerPortraitRouteKind.StockNamed, appearance,
                names.ResolvedAppearanceIndex, names.ResourceName, names.SpriteName, matched, null) {AdmittedAppearanceIndex=checked((ushort)index)};
        }
        catch (InvalidDataException error)
        {
            return new(officerId, OfficerPortraitRouteKind.Unresolved, appearance,
                null, null, null, matched, error.Message);
        }
    }
    private static InvalidDataException PortraitRouteMissing(string field) => new("officer.portrait." + field);
}
