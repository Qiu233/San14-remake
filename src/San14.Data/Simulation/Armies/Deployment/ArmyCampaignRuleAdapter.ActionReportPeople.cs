using System.Collections.ObjectModel;
using San14.Core;

namespace San14.Data;

public sealed record ActionReportPersonDisplay(ushort LookupId, int? OfficerId, string Name,
    OfficerPortraitRoute Portrait);
public sealed record ActionReportPeopleDisplay(IReadOnlyDictionary<ushort, ActionReportPersonDisplay> Rows,
    DeploymentNarrationPortraitRoute? StrategistPortrait);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One read-only capture for the report's current names and portrait
    /// routes. Recycled visible rows consume these values without recapturing.</summary>
    public ActionReportPeopleDisplay ReadActionReportPeople(GameState state,
        IReadOnlyList<ushort> lookupIds, int? strategistPersonId, bool strategistPortraitVisible)
    {
        var current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, _definitions);
        var naming = ArmyMapUiSources.Create();
        var rows = new Dictionary<ushort, ActionReportPersonDisplay>();
        foreach (ushort lookup in lookupIds.Distinct())
        {
            var reference = lookup <= 6000 ? current.ReadPersonLookupReference(lookup)
                : current.ReadTransferPersonTableReference(0);
            int physical = reference?.PhysicalSlot ?? throw Missing("person_lookup");
            bool valid = current.ReadPersonNativeValidity(physical) ?? throw Missing("person_validity");
            if (!valid)
            {
                var placeholder = naming.PortraitNames(2198);
                rows[lookup] = new(lookup, null, "", new(lookup, OfficerPortraitRouteKind.StockNamed,
                    2198, placeholder.ResolvedAppearanceIndex, placeholder.ResourceName, placeholder.SpriteName, null, null));
                continue;
            }
            int identity = current.ReadPersonIdentity(physical) ?? throw Missing("person_identity");
            if (!state.Officers.TryGetValue(identity, out var officer)) throw Missing("person_owner");
            ushort appearance = current.ReadPersonPortraitAppearance(physical) ?? throw Missing("person_appearance");
            var route = ReadOfficerPortraitAppearance(state, current, _definitions, identity, appearance, naming);
            if (route.Kind != OfficerPortraitRouteKind.StockNamed)
                throw new InvalidDataException(route.UnresolvedInput ?? "action-report.person_portrait");
            rows[lookup] = new(lookup, identity, officer.Name, route);
        }
        DeploymentNarrationPortraitRoute? standing = null;
        if (strategistPortraitVisible)
        {
            int speaker = strategistPersonId ?? throw Missing("strategist_person");
            int physical = current.ReadPersonLookupReference(speaker)?.PhysicalSlot ?? throw Missing("strategist_lookup");
            ushort appearance = current.ReadPersonPortraitAppearance(physical) ?? throw Missing("strategist_appearance");
            standing = ReadNarrationPortrait(state, current, _definitions, speaker, appearance);
        }
        return new(new ReadOnlyDictionary<ushort, ActionReportPersonDisplay>(rows), standing);

        static InvalidDataException Missing(string field) => new("action-report.display." + field);
    }
}
