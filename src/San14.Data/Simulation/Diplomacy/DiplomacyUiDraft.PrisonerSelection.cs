using San14.Core;

namespace San14.Data;

public sealed record DiplomacyPrisonerSelection(OfficerInformationPageSet Pages,
    IReadOnlyList<int> InitialSelection, int MaximumSelected, string GuideText,
    IReadOnlyDictionary<int, AreaAdministrationQuery<bool>> AlteredHealth);

public sealed partial class DiplomacyUiDraft
{
    public AreaAdministrationQuery<DiplomacyPrisonerSelection> ReadPrisonerSelection()
    {
        if (Kind != DiplomaticMissionKind.ReturnCaptives)
            throw new InvalidOperationException("This diplomacy page has no captive selector.");
        if (CaptiveChoicesUnresolvedInput is {} missing)
            return AreaAdministrationQuery<DiplomacyPrisonerSelection>.Unknown(missing);
        try
        {
            return AreaAdministrationQuery<DiplomacyPrisonerSelection>.Known(
                source.PrisonerSelection(CaptiveChoices, SelectedCaptiveOfficerIds));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<DiplomacyPrisonerSelection>.Unknown(error.Message); }
    }
}

internal sealed partial class DiplomacyUiSource
{
    internal DiplomacyPrisonerSelection PrisonerSelection(IReadOnlyList<DiplomacyCaptiveChoice> candidates,
        IReadOnlyList<int> initial) => adapter.ReadDiplomacyPrisonerSelectionCurrent(State,current,ForceId,candidates,initial);
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal DiplomacyPrisonerSelection ReadDiplomacyPrisonerSelectionCurrent(GameState state, SourceWorldContext current,
        int forceId, IReadOnlyList<DiplomacyCaptiveChoice> candidates, IReadOnlyList<int> initial)
    {
        //The existing candidate publication owns the actual five-ability
        //sum/identity initial order and the native ransom quote.
        var reader = new OfficerInformationCurrent(this,state,forceId,rowPaletteEnabled:false,retainedCurrent:current);
        return reader.DiplomacyPrisonerPages(candidates,initial);
    }

    private sealed partial class OfficerInformationCurrent
    {
        private IReadOnlyDictionary<int,int>? diplomacyPrisonerRansoms;
        private readonly Dictionary<int,int?> diplomacyHoldingRulers = [];

        internal DiplomacyPrisonerSelection DiplomacyPrisonerPages(IReadOnlyList<DiplomacyCaptiveChoice> candidates,
            IReadOnlyList<int> initial)
        {
            diplomacyPrisonerRansoms = candidates.ToDictionary(row => row.OfficerId,row => row.RansomGold);
            var pages = Pages(candidates.Select(row => row.OfficerId).ToArray(),Data(1,0x58E),
                DiplomacyPrisonerColumnsSource.Create(Data));
            pages.SortSelection(initial,selectedFirst:true);
            return new(pages,initial.ToArray(),3,rules.PrepareCommanderSelectionGuide(),
                candidates.ToDictionary(candidate=>candidate.OfficerId,candidate=>MovementAlteredHealth(candidate.OfficerId)));
        }

        private PreparedCell DiplomacyPrisonerCell(Person person,int column)
        {
            int Ransom() => diplomacyPrisonerRansoms![person.Id];
            bool Trait()
            {
                for(int index=0;index<9;index++)
                    if(Need(TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current,person.PhysicalSlot,index),
                        "prisoner_personality_slot") == 109) return true;
                return false;
            }
            return column switch
            {
                78 => MakeCell(() => HoldingRuler(person) is int physical ? HoldingPersonName(physical,false) : "",
                    () => HoldingRuler(person) is int physical ? new NameKey(HoldingPersonName(physical,true)) : new NameKey("",true)),
                79 => MakeCell(() => Number(Ransom()),() => new NumberKey(Ransom())),
                113 => MakeCell(() => Trait() ? Data(1,0x338) : "",() => new NumberKey(Trait()?1:0)),
                _ => throw Missing("prisoner_column")
            };
        }

        private int? HoldingRuler(Person person)
        {
            if(diplomacyHoldingRulers.TryGetValue(person.Id,out var saved))return saved;
            var home = BaseLookup.Find(Need(current.ReadPersonFirstLocation(person.PhysicalSlot),"prisoner_HomeE2"),current);
            int raw = Need(current.ReadBaseForceSlot(home),"prisoner_holding_Force");
            var force = current.ReadForceTableReference(raw<=51?raw:0) ?? throw Missing("prisoner_holding_Force_pointer");
            if(!Need(current.ReadForceNativeValidity(force),"prisoner_holding_Force_validity"))
                return diplomacyHoldingRulers[person.Id]=null;
            int ruler = Need(current.ReadForceRecord(Need(force.PhysicalSlot,"prisoner_holding_Force_receiver"))?.RawRuler10,
                "prisoner_holding_ruler10");
            //The original fallback is World's physical transfer Person0,
            //not the logical person6000. Neither name getter tests validity.
            int physical = Need((ruler<=6000 ? current.ReadPersonLookupReference(ruler) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot,
                "prisoner_holding_ruler_pointer");
            return diplomacyHoldingRulers[person.Id]=physical;
        }

        private string HoldingPersonName(int physical,bool sort)
        {
            int id = Need(current.ReadPersonIdentity(physical),"prisoner_holding_ruler_identity");
            if(state.Officers.TryGetValue(id,out var owner))
                return sort ? owner.CurrentNameSortKey ?? throw Missing("prisoner_ruler_sort34_5A")
                    : (owner.CurrentSurname ?? throw Missing("prisoner_ruler_surname12"))
                        +(owner.CurrentGivenName ?? throw Missing("prisoner_ruler_given20"));
            if(physical!=0)throw Missing("prisoner_ruler_name_owner");
            var rows = rules._scenario.Snapshot.Characters;
            if(rows.Count==0 || rows[0].Slot!=0)throw Missing("prisoner_Person0_definition");
            return sort ? rows[0].NameSortKey ?? throw Missing("prisoner_Person0_sort34_5A")
                : (rows[0].FamilyName ?? throw Missing("prisoner_Person0_surname12"))
                    +(rows[0].GivenName ?? throw Missing("prisoner_Person0_given20"));
        }
    }
}
