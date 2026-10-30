using San14.Core;
using San14.Data;
using Godot;

public partial class Main
{
    /// <summary>The same original InfoMenu pages serve both the map menu and
    /// the proposal's retained information child. The caller keeps admission,
    /// its world-adoption transaction and parent navigation.</summary>
    private bool TryOpenTypedInformationPages(MapInformationBrowserView browser,ArmyCampaignRuleAdapter rules,
        GameState world,int player,InformationTab tab,InformationListKind kind,Action<GameState> adoptWorld)
    {
        bool own=tab==InformationTab.OwnForce;
            if(kind==InformationListKind.Chronology && own)
            {
                var prepared=rules.PrepareChronologyInformationPage(world);
                if(prepared.Available && prepared.Value is {Selected:not null} chronology)
                    OpenInformationChronology(browser,world,chronology);
                else {GD.PushWarning(prepared.UnresolvedInput??"chronology.empty_records");browser.ApplyNotice(_ui.Get("information_partial"));}
                return true;
            }
            if (kind==InformationListKind.Officers)
            {
                var prepared = rules.PrepareAllOfficerInformationPages(world, player, own);
                if (prepared.Available && prepared.Value is { } officers)
                    OpenInformationOfficerPages(browser, rules, world, officers, adoptWorld);
                else { GD.PushWarning(prepared.UnresolvedInput!); browser.ApplyNotice(_ui.Get("information_partial")); }
                return true;
            }
            if (kind==InformationListKind.Armies)
            {
                var prepared = rules.PrepareAllArmyInformationPages(world, player, own);
                if (prepared.Available && prepared.Value is { } armies)
                    OpenInformationArmyPages(browser, rules, world, armies, adoptWorld);
                else { GD.PushWarning(prepared.UnresolvedInput!); browser.ApplyNotice(_ui.Get("information_partial")); }
                return true;
            }
            if (kind==InformationListKind.Cities)
            {
                var prepared = rules.PrepareAllCityInformationPages(world, player, own);
                if (prepared.Available && prepared.Value is { } cities) OpenInformationCityPages(browser, rules, world, cities);
                else { GD.PushWarning(prepared.UnresolvedInput!); browser.ApplyNotice(_ui.Get("information_partial")); }
                return true;
            }
            if (kind==InformationListKind.Forces && !own)
            {
                var prepared = rules.PrepareAllForceInformationPages(world, player, own);
                if (prepared.Available && prepared.Value is { } forces) OpenInformationForcePages(browser, rules, world, forces);
                else { GD.PushWarning(prepared.UnresolvedInput!); browser.ApplyNotice(_ui.Get("information_partial")); }
                return true;
            }
            if (kind==InformationListKind.Gates)
            {
                var prepared=rules.PrepareAllGateInformationPages(world,player,own);
                if (prepared.Available && prepared.Value is { } values) OpenInformationGatePages(browser,rules,world,values,adoptWorld);
                else { GD.PushWarning(prepared.UnresolvedInput!); browser.ApplyNotice(_ui.Get("information_partial")); }
                return true;
            }
            if (kind==InformationListKind.Areas)
            {
                var prepared=rules.PrepareAllAreaInformationPages(world,player,own);
                if (prepared.Available && prepared.Value is { } values) OpenInformationAreaPages(browser,rules,world,values);
                else { GD.PushWarning(prepared.UnresolvedInput!); browser.ApplyNotice(_ui.Get("information_partial")); }
                return true;
            }
            if (kind==InformationListKind.Districts)
            {
                var prepared=rules.PrepareAllDistrictInformationPages(world,player,own);
                if (prepared.Available && prepared.Value is { } values) OpenInformationDistrictPages(browser,rules,world,values,adoptWorld);
                else { GD.PushWarning(prepared.UnresolvedInput!); browser.ApplyNotice(_ui.Get("information_partial")); }
                return true;
            }
            if (kind==InformationListKind.Formations)
            {
                var prepared = rules.PrepareAllFormationInformationPages(world);
                if (prepared.Available && prepared.Value is { } formations)
                    OpenInformationFormationPages(browser, rules, world, formations, adoptWorld);
                else { GD.PushWarning(prepared.UnresolvedInput!); browser.ApplyNotice(_ui.Get("information_partial")); }
                return true;
            }
            if (kind is InformationListKind.Items or InformationListKind.Scenery or InformationListKind.Advantages or InformationListKind.Traits or InformationListKind.Tactics or InformationListKind.Ranks or InformationListKind.Offices or InformationListKind.Doctrines or InformationListKind.Policies or InformationListKind.Facilities or InformationListKind.ForeignTechnologies or InformationListKind.CreditRanks or InformationListKind.DoctrinePanels)
            {
                var prepared = rules.PrepareAllDefinitionInformationPages(world, player, kind, own);
                if (prepared.Available && prepared.Value is { } definitions)
                    OpenInformationDefinitionPages(browser, rules, world, definitions, adoptWorld);
                else { GD.PushWarning(prepared.UnresolvedInput!); browser.ApplyNotice(_ui.Get("information_partial")); }
                return true;
            }
        return false;
    }
}
