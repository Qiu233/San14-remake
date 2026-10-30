using San14.Core;

namespace San14.Data;

public sealed class AreaAdministrationPersonSelection
{
    internal AreaAdministrationPersonSelection(int area, long revision, IReadOnlyList<CityDomesticCandidate> candidates,
        AreaAdministrationQuery<IReadOnlyList<int>> initialOrder)
    { AreaId=area; Revision=revision; Candidates=Array.AsReadOnly(candidates.ToArray()); InitialOrder=initialOrder; }
    public int AreaId { get; }
    public long Revision { get; }
    public IReadOnlyList<CityDomesticCandidate> Candidates { get; }
    // Membership and its source enumeration are independently useful when a
    // presentation-only initial ranking input is unavailable.
    public AreaAdministrationQuery<IReadOnlyList<int>> InitialOrder { get; }
}
public sealed record AreaAdministrationPersonPage<TRow>(AreaAdministrationPersonSelection Selection, long Revision,
    IReadOnlyList<TRow> Rows, AreaAdministrationQuery<int?> PreselectedOccurrence);
public sealed record AreaAdministrationPersonBasicRow(int Occurrence, CityDomesticCandidate Candidate,
    AreaAdministrationQuery<string> Name, IReadOnlyList<AreaAdministrationQuery<int?>> Abilities,
    IReadOnlyList<AreaAdministrationQuery<DeploymentPersonResponsibility?>> Responsibilities,
    AreaAdministrationQuery<bool?> AreaLocation, AreaAdministrationQuery<AreaAdministrationDevelopmentPreview> Development,
    AreaAdministrationQuery<int?> Occupation, AreaAdministrationQuery<int?> RowState);
public sealed record AreaAdministrationPersonTraitRow(int Occurrence, CityDomesticCandidate Candidate,
    AreaAdministrationQuery<string> Name, IReadOnlyList<AreaAdministrationQuery<bool?>> Traits,
    AreaAdministrationQuery<int?> RowState);
public sealed record AreaAdministrationReplacementArea(int AreaId, string Name);
public sealed record AreaAdministrationPersonReplacement(bool Required, IReadOnlyList<AreaAdministrationReplacementArea> Areas);

public sealed partial class AreaAdministrationDraft
{
    private AreaAdministrationPersonSelection? _personSelection;
    private int? _replacementArea;
    public GameState PersonSelectionWorld => _state;

    public AreaAdministrationQuery<int?> QueryPersonPreselection(AreaAdministrationPersonSelection selection) => PersonCell<int?>(() =>
    { CheckPersonSelection(selection); return Preselected(selection); });

    public AreaAdministrationQuery<AreaAdministrationPersonSelection> QueryPersonSelection()
    {
        try
        {
            int area=Active();
            if(_personSelection is { } existing) return new(existing,null);
            int rawCity=K(_current.ReadAreaCitySlot(new(area)),"selection_area_city");
            var candidates=AreaAdministrationExecution.OrderedCandidates(_current,rawCity<=51?rawCity:0)
                .Select(c=>new CityDomesticCandidate(c.Identity,c.Physical)).ToArray();
            AreaAdministrationQuery<IReadOnlyList<int>> initial=candidates.Length<2
                ? new(Enumerable.Range(0,candidates.Length).ToArray(),null)
                : new(null,"area-draft.person.initial_ability4_weight");
            return new(_personSelection=new(area,Revision,candidates,initial),null);
        }
        catch(InvalidDataException error){return new(null,error.Message);}
    }
    public AreaAdministrationQuery<AreaAdministrationPersonReplacement> QueryPersonReplacementAreas(
        AreaAdministrationPersonSelection selection,int occurrence)
    {
        try
        {
            var candidate=CheckCandidate(selection,occurrence);int p=candidate.PhysicalSlot;
            int capacity=unchecked(Additional(p)+1);
            if(capacity<=1||_vectors.Count(p)<capacity||Person(Active())==candidate.RawIdentity) return new(new(false,[]),null);
            var result=new List<AreaAdministrationReplacementArea>();
            if(K(_current.ReadPersonNativeValidity(p),"replacement_person_validity"))
                for(int i=0;i<_vectors.Count(p);i++)
                {
                    int raw=_vectors.Entry(p,i);
                    if(raw<0)throw Missing("replacement_area_index");
                    var reference=_current.ReadAreaTableReference(raw<=500?raw:0)??throw Missing("replacement_area_pointer");
                    if(!K(_current.ReadAreaNativeValidity(reference),"replacement_area_validity"))continue;
                    int area=K(reference.PhysicalSlot,"replacement_area_identity");
                    // The source publishes the actual active prefix, preserving
                    // duplicate occurrences. The chooser does not mutate it.
                    result.Add(new(area,_state.Areas.GetValueOrDefault(area)?.Name??throw Missing("replacement_area_name")));
                }
            return new(new(true,result.AsReadOnly()),null);
        }
        catch(InvalidDataException error){return new(null,error.Message);}
    }
    public AreaAdministrationDraftResult StagePerson(AreaAdministrationPersonSelection selection,int occurrence,int? replacementArea=null)
    {
        CityDomesticCandidate? candidate=null;
        return Atomic(()=>
        {
            candidate=CheckCandidate(selection,occurrence);
            var replacements=QueryPersonReplacementAreas(selection,occurrence);
            if(!replacements.Available)throw new InvalidDataException(replacements.UnresolvedInput);
            if(replacements.Value!.Required&&!replacements.Value.Areas.Any(a=>a.AreaId==replacementArea))throw Missing("replacement_choice_required");
            if(!replacements.Value.Required&&replacementArea.HasValue)throw Missing("replacement_choice_unexpected");
            int area=Active();byte focus=Focus(area);
            if(focus==0)
            {
                var row=_state.Areas.GetValueOrDefault(area)??throw Missing("person_focus_area");
                foreach(var option in new[]{AreaDevelopmentFocus.Commerce,AreaDevelopmentFocus.Agriculture,AreaDevelopmentFocus.Barracks})
                    if((Progress(row,option)??throw Missing("person_focus_progress"))<=999){focus=EncodeFocus(option);break;}
            }
            StageIdentity(area,candidate.RawIdentity,focus,true,replacementArea);
            _replacementArea=replacementArea;
        },prepareKnownPeople:()=>
        {
            candidate=CheckCandidate(selection,occurrence);
            return _held.Select(Person).Concat(_parent.People).Concat(_child?.People??[])
                .Append(candidate.RawIdentity).Append((ushort)0).Select(ClampPerson).Distinct()
                .Select(id=>PersonAreaAppointmentSource.Resolve(_current,id)).Append(candidate.PhysicalSlot);
        });
    }
    private CityDomesticCandidate CheckCandidate(AreaAdministrationPersonSelection selection,int occurrence)
    {
        CheckPersonSelection(selection);
        if((uint)occurrence>=(uint)selection.Candidates.Count)throw Missing("person_occurrence");
        var candidate=selection.Candidates[occurrence];
        if(K(_current.ReadPersonIdentity(candidate.PhysicalSlot),"person_identity")!=candidate.RawIdentity)throw Missing("person_identity_binding");
        return candidate;
    }
    private void CheckPersonSelection(AreaAdministrationPersonSelection selection)
    {if(!ReferenceEquals(_personSelection,selection)||selection.AreaId!=Active())throw Missing("person_selection_owner");}
    private int Additional(int p)=>PersonAreaAppointmentSource.AdditionalCount(_state,_current,p);
    private int Ability(int p,int dimension)=>_previewSource.AutomaticAbility(p,dimension);
    private static AreaAdministrationQuery<T> PersonCell<T>(Func<T> read)
    {try{return new(read(),null);}catch(InvalidDataException error){return new(default,error.Message);}}
    private int? Preselected(AreaAdministrationPersonSelection selection)
    {
        int physical=PersonAreaAppointmentSource.Resolve(_current,Person(Active()));
        for(int i=0;i<selection.Candidates.Count;i++)if(selection.Candidates[i].PhysicalSlot==physical)return i;
        return null;
    }
    private AreaAdministrationQuery<AreaAdministrationPersonPage<TRow>> AreaPersonPage<TRow>(
        AreaAdministrationPersonSelection selection,IReadOnlyList<int> order,
        Func<CityDomesticPersonCellSource,CityDomesticCandidate,int,TRow> read)
    {
        try
        {
            CheckPersonSelection(selection);var source=_officerCardSource.CreatePersonCells();
            var rows=PersonOrder(selection,order).Select(i=>read(source,selection.Candidates[i],i)).ToArray();
            return new(new(selection,Revision,rows,PersonCell<int?>(()=>Preselected(selection))),null);
        }
        catch(InvalidDataException error){return new(null,error.Message);}
    }
    private static int[] PersonOrder(AreaAdministrationPersonSelection selection,IReadOnlyList<int> order)
    {
        if(order.Count!=selection.Candidates.Count||order.Any(i=>(uint)i>=(uint)order.Count)||order.Distinct().Count()!=order.Count)
            throw Missing("person_order_permutation");
        return order.ToArray();
    }
    public AreaAdministrationQuery<AreaAdministrationPersonPage<AreaAdministrationPersonBasicRow>> QueryPersonBasicPage(
        AreaAdministrationPersonSelection selection,IReadOnlyList<int> order)=>AreaPersonPage(selection,order,(source,c,i)=>
        {
            var abilities=Enumerable.Range(0,5).Select(d=>PersonCell<int?>(()=>Ability(c.PhysicalSlot,d))).ToArray();
            var responsibilities=Enumerable.Range(0,3).Select(index=>PersonCell<DeploymentPersonResponsibility?>(()=>index==0
                ?DeploymentPersonResponsibilityDisplaySource.Read(_state,_current,c.PhysicalSlot,_vectors.Count,_vectors.Entry)
                :DeploymentPersonResponsibilityDisplaySource.ReadSecondary(_state,_current,c.PhysicalSlot,index,Additional,_vectors.Count,_vectors.Entry))).ToArray();
            var development=QueryCandidateDevelopmentPreview(c.RawIdentity);
            return new AreaAdministrationPersonBasicRow(i,c,PersonCell(()=>source.Name(c)),abilities,responsibilities,
                PersonCell<bool?>(()=>source.AreaLocation(c,true)),development,
                development.Available?development.Value!.CandidateOccupation:new(null,development.UnresolvedInput),
                PersonCell<int?>(()=>source.RowState(c)));
        });
    public AreaAdministrationQuery<AreaAdministrationPersonPage<AreaAdministrationPersonTraitRow>> QueryPersonTraitPage(
        AreaAdministrationPersonSelection selection,IReadOnlyList<int> order)=>AreaPersonPage(selection,order,(source,c,i)=>
        {
            int[] requested=[109,179,110,155,111,112,113,114,115,116,181,138,219,215,218];
            return new AreaAdministrationPersonTraitRow(i,c,PersonCell(()=>source.Name(c)),requested.Select(raw=>PersonCell<bool?>(()=>
            {for(int slot=0;slot<9;slot++)if(K(TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(_current,c.PhysicalSlot,slot),"person_trait_lookup")==raw)return true;return false;})).ToArray(),
                PersonCell<int?>(()=>source.RowState(c)));
        });
    public AreaAdministrationQuery<AreaAdministrationPersonPage<CityDomesticPersonAffiliationRow>> QueryPersonAffiliationPage(
        AreaAdministrationPersonSelection selection, IReadOnlyList<int> order) => AreaPersonPage(selection, order, (source, c, occurrence) =>
        {
            var name = PersonCell(() => source.Name(c));
            var force = PersonCell(() => source.ForceName(c));
            var district = PersonCell(() => source.DistrictNumber(c));
            var home = PersonCell(() => source.Home(c));
            var location = PersonCell(() => source.CurrentLocation(c));
            var status = PersonCell(() => source.ServiceStatus(c));
            var responsibility = PersonCell<DeploymentPersonResponsibility?>(() =>
            { source.Identity(c); return DeploymentPersonResponsibilityDisplaySource.Read(_state, _current, c.PhysicalSlot, _vectors.Count, _vectors.Entry); });
            var loyalty = PersonCell(() => source.Loyalty(c));
            var officeName = PersonCell(() => source.Office(c));
            var metric = PersonCell(() => source.OtherMetric(c));
            var years = PersonCell(() => source.ServiceYears(c, true));
            var paint = PersonCell<int?>(() => source.RowState(c));
            return new CityDomesticPersonAffiliationRow(occurrence, c, name, paint, force, district, home, location,
                status, responsibility, loyalty, officeName, metric, years);
        });
    public AreaAdministrationQuery<AreaAdministrationPersonPage<CityDomesticPersonAbilityRow>> QueryPersonAbilityPage(
        AreaAdministrationPersonSelection selection, IReadOnlyList<int> order) => AreaPersonPage(selection, order, (source, c, occurrence) =>
        {
            var name = PersonCell(() => source.Name(c)); var status = PersonCell(() => source.ServiceStatus(c));
            AreaAdministrationQuery<int?> Dimension(int dimension) => PersonCell<int?>(() => { source.Identity(c); return Ability(c.PhysicalSlot, dimension); });
            var lead = Dimension(0); var strength = Dimension(1); var intelligence = Dimension(2);
            var politics = Dimension(3); var charm = Dimension(4);
            var health = PersonCell(() => source.Health(c)); var items = PersonCell(() => source.ItemCount(c));
            var paint = PersonCell<int?>(() => source.RowState(c));
            return new CityDomesticPersonAbilityRow(occurrence, c, name, paint, status, lead, strength, intelligence,
                politics, charm, health, items);
        });
    public AreaAdministrationQuery<AreaAdministrationPersonPage<CityDomesticPersonMeritRow>> QueryPersonMeritPage(
        AreaAdministrationPersonSelection selection, IReadOnlyList<int> order) => AreaPersonPage(selection, order, (source, c, occurrence) =>
        {
            var name = PersonCell(() => source.Name(c)); var stage = PersonCell(() => source.RankName(c));
            var merit = PersonCell(() => source.Merit(c, true));
            var command = PersonCell(() => source.BonusDisplay(c, () => source.CreditBonus(c, 1)));
            var areas = PersonCell(() => source.BonusDisplay(c, () => source.AdditionalBonus(c)));
            var credit3 = PersonCell(() => source.BonusDisplay(c, () => source.CreditBonus(c, 3)));
            var paint = PersonCell<int?>(() => source.RowState(c));
            return new CityDomesticPersonMeritRow(occurrence, c, name, paint, stage, merit, command, areas, credit3);
        });
    public AreaAdministrationQuery<AreaAdministrationPersonPage<CityDomesticPersonTaskRow>> QueryPersonTaskPage(
        AreaAdministrationPersonSelection selection, IReadOnlyList<int> order) => AreaPersonPage(selection, order, (source, c, occurrence) =>
        {
            var name = PersonCell(() => source.Name(c)); var task = PersonCell(() => source.Task(c));
            var target = PersonCell(() => source.TaskTarget(c)); var remaining = PersonCell(() => source.TaskRemaining(c));
            var returning = PersonCell(() => source.Returning(c)); var reset = PersonCell(() => source.Reset(c));
            var paint = PersonCell<int?>(() => source.RowState(c));
            return new CityDomesticPersonTaskRow(occurrence, c, name, paint, task, target, remaining, returning, reset);
        });
    public AreaAdministrationQuery<AreaAdministrationPersonPage<CityDomesticPersonPersonalRow>> QueryPersonPersonalPage(
        AreaAdministrationPersonSelection selection, IReadOnlyList<int> order) => AreaPersonPage(selection, order, (source, c, occurrence) =>
        {
            var name = PersonCell(() => source.Name(c)); var sex = PersonCell(() => source.Sex(c));
            var age = PersonCell(() => source.Age(c, true)); var doctrine = PersonCell(() => source.Doctrine(c));
            var father = PersonCell(() => source.Relation(c, 0xB4, false)); var mother = PersonCell(() => source.Relation(c, 0xB6, false));
            var partner = PersonCell(() => source.Relation(c, 0xBA, false)); var sworn = PersonCell(() => source.Relation(c, 0xBC, false));
            var paint = PersonCell<int?>(() => source.RowState(c));
            return new CityDomesticPersonPersonalRow(occurrence, c, name, paint, sex, age, doctrine, father, mother, partner, sworn);
        });
}
