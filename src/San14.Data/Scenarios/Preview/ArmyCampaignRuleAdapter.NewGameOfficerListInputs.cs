using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal NewGameOfficerListInputs CreateNewGameOfficerListInputs(GameState state,
        SourceWorldContext current, NewGameOfficerAbilityProducer abilities) =>
        new(state, current, _scenario, abilities, _governorInputs, _personRelationInputs,
            _personDestructionInputs, _rulerCaptureReleaseInputs, Seed,
            ReadApplicationGameplay()?.UserSelectionServicePresent, _growthQueries.Definitions, _destructionSeed);
}

/// <summary>The read-only normal-load inputs lent to the shared Person list.
/// These are initial serialized fields and documented constructor state, not
/// a replacement saved gameplay owner or a fallback used after Start.</summary>
internal sealed partial class NewGameOfficerListInputs
{
    private readonly GameState state;
    private readonly SourceWorldContext current;
    private readonly LoadedScenario scenario;
    private readonly NewGameOfficerAbilityProducer abilities;
    private readonly OwnedArmyGovernorComparatorInputs governor;
    private readonly OwnedPersonRelationInputs relations;
    private readonly OfficerAftermathInputs aftermath;
    private readonly RulerCaptureReleaseInputs preferences;
    private readonly IReadOnlyDictionary<string,int> parameters;
    private readonly bool? selectionServicePresent;

    internal NewGameOfficerListInputs(GameState state, SourceWorldContext current,
        LoadedScenario scenario, NewGameOfficerAbilityProducer abilities,
        OwnedArmyGovernorComparatorInputs governor, OwnedPersonRelationInputs relations,
        OfficerAftermathInputs aftermath, RulerCaptureReleaseInputs preferences, TacticRemakeParameterSeed seed, bool? selectionServicePresent, LoadedGrowthModifierDefinitions growth, ArmyDestructionParameterSeed destructionSeed)
    {
        if (!ArmyOperationContext.IsNewGamePreview(state) ||
            current.StaticSource?.ResourceUri != scenario.ScenarioSource.ResourceUri ||
            current.StaticSource.Sha256 != scenario.ScenarioSource.Sha256 ||
            state.ContentHashes.GetValueOrDefault(scenario.ScenarioSource.ResourceUri) != scenario.ScenarioSource.Sha256 ||
            state.ContentHashes.GetValueOrDefault(scenario.FixedDataSource.ResourceUri) != scenario.FixedDataSource.Sha256)
            throw Missing("normal-load-owner");
        this.state=state;this.current=current;this.scenario=scenario;this.abilities=abilities;
        this.governor=governor;this.relations=relations;this.aftermath=aftermath;this.preferences=preferences;
        var values=seed.Fields.ToDictionary(field=>field.Key,field=>field.Value);
        foreach(var field in destructionSeed.Fields)
        {
            if(values.TryGetValue(field.Key,out int prior)&&prior!=field.Value)throw Missing("parameter-owner-conflict."+field.Key);
            values.TryAdd(field.Key,field.Value);
        }
        parameters=values;this.growth=growth;growthMode=ReadGrowthMode(scenario);
        this.selectionServicePresent=selectionServicePresent;
        BindNormalLoadProjection();
    }

    internal int? Mode => current.ReadScenarioMode();
    internal short? ThirdYear => current.ReadConditionDate()?.Year;
    internal int? HistoricalEventControl => scenario.Snapshot.WorldParameters?.RawHistoricalEventControl;
    internal bool TimelessAges => PersonCurrentAgeSource.UsesTimelessAges(Mode ?? throw Missing("world-mode"), HistoricalEventControl);

    private OfficerState Person(int physical)
    {
        ushort id=current.ReadPersonIdentity(physical)??throw Missing("person-identity");
        if ((uint)physical >= scenario.Snapshot.Characters.Count ||
            scenario.Snapshot.Characters[physical].CharacterId != id ||
            current.ResolveOfficerSource(id)!=physical || !state.Officers.TryGetValue(id,out var person))
            throw Missing("person-source-binding");
        return person;
    }

    internal byte? Loyalty(int physical)
    {
        var person=Person(physical);
        if (person.Loyalty is int value) return value is >=0 and <=255 ? (byte)value : null;
        var row=governor.ReadNormalLoadPerson(physical);
        return row?.PersonId==person.Id ? row.RawE8 : null;
    }
    internal ushort? ServiceYear(int physical)
    {
        var person=Person(physical);var row=governor.ReadNormalLoadPerson(physical);
        return row?.PersonId==person.Id ? row.Word166 : null;
    }
    internal byte? Health(int physical)
    {
        var person=Person(physical);
        // Person ctor/reset clears161; normal static2391C0 skips health.
        // An explicit typed observation keeps its unknown/value semantics.
        return person.Health is {} health ? health.StateId is >=0 and <=255 ? (byte)health.StateId : null : (byte)0;
    }
    internal byte? Office(int physical)
    {
        int? office=abilities.ReadOfficeId(Person(physical).Id);
        return office is >=0 and <=255 ? (byte)office : null;
    }
    internal bool? OfficeValid(int office)
    {
        if (office==0) return false;
        var rows=scenario.Snapshot.RankParameters?.Offices;
        return rows is not null && (uint)office<rows.Count && rows[office].Id==office
            ? rows[office].CommandLimit!=0 : null;
    }
    internal ushort? Relation(int physical,int field)
    { Person(physical);return relations.ReadNormalLoadRelation(physical,field); }
    internal int? Disposition(int physical)
    {
        var person=Person(physical);
        if (person.AllegianceDisposition is {} explicitValue) return explicitValue.Value;
        var row=aftermath.ReadNormalLoadPerson(physical);
        return row?.PersonId==person.Id ? row.AllegianceDisposition : null;
    }
    internal short? Preference(int physical,int field)
    { Person(physical);return preferences.ReadNormalLoadWord(physical,field); }

    private bool itemsPrepared;
    internal void PrepareItemRegistry()
    {
        if(itemsPrepared)return;
        if(!current.HasNormalLoadRegistryObservation(RegistryKind.Item))
        {
            var rows=scenario.Snapshot.ScenarioItems?.Items ?? throw Missing("item-source");
            if(rows.Count!=202)throw Missing("item-source-count");
            var pool=current.ReadOwnedCurrentListPool() ?? new WorldIndexContext(current);
            current.SetRegistryDescriptor(RegistryKind.Item,new(pool.AllocateDescriptor().Identity));
            for(int slot=0;slot<rows.Count;slot++)
            {
                var row=rows[slot];
                if(row.Slot!=slot)throw Missing("item-source-identity");
                if(current.ReadRegistryTableReference(RegistryKind.Item,slot) is null)
                    current.SetRegistryTablePointer(RegistryKind.Item,slot,slot);
                var reference=current.ReadRegistryTableReference(RegistryKind.Item,slot)??throw Missing("item-pointer");
                var observed=state.Items?.GetValueOrDefault(slot)?.Lifecycle;
                var holder=state.Items?.GetValueOrDefault(slot)?.Holder;
                current.LendNormalLoadItem(slot,observed is not null ? observed.Status :
                    row.RawStatusCode is byte code ? ItemStatusKnowledge.Exact(code) : null,
                    holder is not null ? Word(holder.OfficerId) : row.OwnerId);
                _=ObjectRegistration.Register(reference,current);
            }
        }
        itemsPrepared=true;
    }

    internal ushort Reset(int physical)
    { Person(physical);return current.ReadPersonResetWord1FF(physical) ?? 0; }
    internal int AssignmentCount(int physical)
    { Person(physical);return current.ReadNormalLoadAssignmentCount(physical) ?? throw Missing("assignment-count"); }
    internal int AssignmentEntry(int physical,int index) =>
        (current.ReadAssignmentPersonAreaVector(physical) ?? throw Missing("assignment-vector")).Values[index];

    private OfficerMeritState? Credit(int physical)
    {
        var person=Person(physical);
        if(person.MeritState is {} currentCredit)return currentCredit;
        var row=scenario.Snapshot.Characters[physical].CreditReference;
        if(row is not{RawSelectionCodes.Count:3} || row.CharacterId!=person.Id)return null;
        //2391C0 derives these two counters from the three serialized bytes.
        //Reset is not needed by this reader and remains unknown here.
        return new(unchecked((short)row.RawValue0),row.RawValue1,row.RawStageCode,
            row.RawSelectionCodes.Count(value=>value is >=1 and <=3),
            row.RawSelectionCodes.Count(value=>value is >=4 and <=6),null);
    }
    internal byte? Stage(int physical)=>OfficerMeritSourceAdapter.EncodeStage(Credit(physical));
    internal short? Merit(int physical)=>OfficerMeritSourceAdapter.EncodeMerit(Credit(physical));
    internal int? Parameter(string key)=>parameters.TryGetValue(key,out int value)?value:null;
    internal bool? PolicyEnabled(int force,int effect) => state.Forces.GetValueOrDefault(force)?.PolicyEffects is {} policy
        ? policy.IsEnabled(effect) : (uint)force<scenario.Snapshot.Forces.Count&&scenario.Snapshot.Forces[force].Slot==force
            ? false : null;
    internal int? PolicyModifier(int force,int effect) => PolicyEnabled(force,effect) is bool enabled
        ? enabled ? current.BaseForceModifierReader?.Invoke(new(force),effect) : 0 : null;
    internal int CreditBonus(int physical,int kind)
    {
        var person=Person(physical);
        if(current.ReadPersonNativeValidity(physical) is not bool valid)throw Missing("credit-validity");
        if(!valid)return 0;
        byte status=current.ReadPersonStatus(physical)??throw Missing("credit-status");
        int force=current.ReadPersonForce(physical)?.PhysicalSlot??throw Missing("credit-force");
        if(force is >=46 and <=50)return 0;
        int id=person.Id;
        bool special=unchecked((uint)(id-4111))<=9||unchecked((uint)(id-4131))<=9||
            unchecked((uint)(id-4151))<=9||unchecked((uint)(id-4171))<=9||unchecked((uint)(id-4191))<=9;
        if(special&&(current.ReadPersonFirstLocation(physical)??throw Missing("credit-first-location")) is >=1 and <=61)return 0;
        ushort flags=current.ReadArmyReturnPersonFlags196(physical)??throw Missing("credit-flags");
        if((flags&0x4000)!=0)return 0;
        var credit=Credit(physical);
        if(kind!=2 && status==1)return 0;
        if(status!=1&&(credit?.RankStageId==0||OfficerMeritRules.HasNoSelections(credit)))return 0;
        if(selectionServicePresent is true)
        {
            if(state.CampaignEventWorld?.CurrentUserIndex is not sbyte selected)throw Missing("credit-current-user");
            if(selected!=-1)return 0;
        }
        else if(selectionServicePresent is null&&state.CampaignEventWorld?.CurrentUserIndex!=-1)
            throw Missing("credit-current-user");
        if(Mode is not int mode)throw Missing("credit-mode");
        if(mode==3)throw Missing("credit-person-selection-state");
        if(kind==2&&status==1)return 2;
        var effects=scenario.Snapshot.CreditEffectParameters;
        return OfficerCommandCapacity.SumCreditSelections(credit,kind,
            slot=>effects is {FormatMarker:0x66} && (uint)slot<effects.Effects.Count&&effects.Effects[slot].Slot==slot?effects.Effects[slot].RawTypeCode:null,
            slot=>effects is {FormatMarker:0x66} && (uint)slot<effects.Effects.Count&&effects.Effects[slot].Slot==slot?effects.Effects[slot].RawAddition:null);
    }

    private void BindNormalLoadProjection()
    {
        // Normal static Army section27 has501 zero-byte bodies; constructor
        // commander/status observations are meaningful even with no gameplay
        // Army lifecycle. Preserve existing pointer/current scalar observations.
        for(int slot=0;slot<501;slot++)
        {
            var reference=current.ReadArmyUnitTableReference(slot);
            if(reference is null){current.SetArmyUnitTablePointer(slot,slot);reference=new(slot);}
            if(reference.PhysicalSlot is int physical)
            {
                current.LendNormalLoadArmyCommander(physical);
                //Commander0 already establishes invalidity; no unread action,
                //target or command state is invented.
            }
        }
        // World331F50 reconstructs manager1D7DD0 with its18 list empty.
        // A real manager/sequence owns the current observation if present.
        if(current.ReadOwnedPersonCommandManager() is null&&current.ReadCurrentPersonLinkedEntities() is null)
            current.SetCurrentPersonLinkedEntities([]);

        // Only this publication's borrowed resource projection is filled.
        // Core state and its absent simulation/clock remain untouched. These
        // runtime-only fields are constructor facts already shared by the
        // fresh-game installer; explicit current/domain observations win.
        for(int city=0;city<52;city++)
        {
            var duties=state.Cities.GetValueOrDefault(city)?.Duties;
            current.LendNormalLoadCityAssignments(city,Word(duties?.RecruiterId),Word(duties?.TrainerId));
        }
        for(int area=0;area<501;area++)
            current.LendNormalLoadAreaAssignment(area,Word(state.Areas.GetValueOrDefault(area)?.Administration?.OverseerId));
        for(int force=0;force<52;force++)
            for(int index=0;index<3;index++)current.LendNormalLoadForceAssociation(force,index);
        // Use the same real physical table admission and pool as the force
        // preview. Do not substitute Domain dictionary enumeration/order.
        if(!current.HasNormalLoadRegistryObservation(RegistryKind.Area))
        {
            var pool=current.ReadOwnedCurrentListPool() ?? new WorldIndexContext(current);
            current.SetRegistryDescriptor(RegistryKind.Area,new(pool.AllocateDescriptor().Identity));
            for(int slot=0;slot<501;slot++)
                _=ObjectRegistration.Register(current.ReadRegistryTableReference(RegistryKind.Area,slot),current);
        }
    }

    private static ushort? Word(int? identity) => identity is null ? (ushort)0 : identity is >=0 and <=65535 ? (ushort)identity : null;

    private static InvalidDataException Missing(string input) => new("new-game.person-list."+input);
}
