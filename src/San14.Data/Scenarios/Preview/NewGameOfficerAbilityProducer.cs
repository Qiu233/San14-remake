using San14.Core;

namespace San14.Data;

internal sealed record NewGameOfficerAbilityRead(OfficerAbilities? Abilities,string? UnresolvedInput);

/// <summary>Read-only effective ability inputs for the normal pre-start load.
/// Constructor facts are local typed inputs, never invented gameplay owners.</summary>
internal sealed class NewGameOfficerAbilityProducer : IOfficerAbilityContext
{
    private readonly GameState preview;
    private readonly SourceWorldContext current;
    private readonly LoadedScenario scenario;
    private readonly IReadOnlyDictionary<string,int> parameters;
    private readonly IReadOnlyDictionary<int,ForcePolicyEffects> loadedForceEffects;
    private readonly EffectiveOfficerAbilityResolver resolver;
    private readonly Dictionary<int,NewGameOfficerAbilityRead> values = [];

    internal NewGameOfficerAbilityProducer(GameState preview,SourceWorldContext current,
        LoadedScenario scenario,TacticRemakeParameterSeed seed)
    {
        if (!ArmyOperationContext.IsNewGamePreview(preview) || scenario.Metadata.FormatMarker != 0x66 ||
            seed.Language != scenario.Snapshot.Locale || current.StaticSource?.ResourceUri != scenario.ScenarioSource.ResourceUri ||
            current.StaticSource.Sha256 != scenario.ScenarioSource.Sha256 ||
            preview.ContentHashes.GetValueOrDefault(scenario.ScenarioSource.ResourceUri) != scenario.ScenarioSource.Sha256 ||
            preview.ContentHashes.GetValueOrDefault(scenario.FixedDataSource.ResourceUri) != scenario.FixedDataSource.Sha256)
            throw Missing("normal-load-owner");
        this.preview=preview;this.current=current;this.scenario=scenario;
        parameters=seed.Fields.ToDictionary(field=>field.Key,field=>field.Value);
        // CForce221110 -> reset227070 clears activation/suppression; static
        // serializer skips these windows and normal slot7 is NoOp. This is
        // the pre-start allocation, not the later new-game policy writer.
        loadedForceEffects=scenario.Snapshot.Forces.ToDictionary(row=>row.Slot,_=>ForcePolicyEffects.Empty);
        resolver=new(this);
    }

    internal NewGameOfficerAbilityRead Resolve(int officerId)
    {
        if (values.TryGetValue(officerId,out var found))return found;
        NewGameOfficerAbilityRead read;
        try
        {
            int physical=current.ResolveOfficerSource(officerId)??throw Missing("person-lookup");
            bool valid=current.ReadPersonNativeValidity(physical)??throw Missing("person-validity");
            read=new(valid ? resolver.Resolve(preview,officerId)??throw Missing("item-ability-inputs") : Zero,null);
        }
        catch(InvalidDataException error){read=new(null,error.Message);}
        values.Add(officerId,read);return read;
    }

    internal int? ReadOfficeId(int officerId)
    {
        var (person,source)=ReadLoadedPerson(officerId);
        return person.Appointment.AssignedOfficeId??(person.Appointment.ObservedOfficeId is int observed
            ? observed==0?0:null : source.RawOfficeId);
    }

    internal ushort? ReadStoredMeritWord(int officerId)
    {
        var (person,source)=ReadLoadedPerson(officerId);
        if(person.MeritState is not{ } credit)return source.CreditReference?.RawValue0;
        return credit.Merit is int merit&&merit is >=short.MinValue and <=short.MaxValue?unchecked((ushort)(short)merit):null;
    }

    internal byte? ReadCreditStage(int officerId)
    {
        var (person,source)=ReadLoadedPerson(officerId);
        return person.MeritState is{ } credit?OfficerMeritSourceAdapter.EncodeStage(credit):source.CreditReference?.RawStageCode;
    }

    private (OfficerState Person,CharacterPreviewRecord Source) ReadLoadedPerson(int officerId)
    {
        var person=preview.Officers.GetValueOrDefault(officerId)??throw Missing("person-owner");
        int physical=current.ResolveOfficerSource(officerId)??throw Missing("person-lookup");
        var source=scenario.Snapshot.Characters[physical];
        if(source.CharacterId!=officerId)throw Missing("person-source-identity");
        return(person,source);
    }

    OfficerAbilityContext? IOfficerAbilityContext.Resolve(GameState state,int officerId)
    {
        if (!ReferenceEquals(state,preview))return null;
        var person=state.Officers.GetValueOrDefault(officerId)??throw Missing("person-owner");
        int physical=current.ResolveOfficerSource(officerId)??throw Missing("person-lookup");
        var fields=current.ReadPhysicalPerson(physical)??throw Missing("person-fields");
        var source=scenario.Snapshot.Characters[physical];
        if (source.CharacterId!=officerId || person.BaseAbilities is null)throw Missing("person-source-identity");
        byte status=fields.RawStatus11E;
        if(!TacticRemakeCurrentAbilities.RoleMatches(person.Role,status))throw Missing("person-role-status");
        var behavior=OfficerBehaviorSourceAdapter.Decode(fields.RawFlags196,fields.RawFlag1FD);
        int stage=person.MeritState is{ } credit ? credit.RankStageId??throw Missing("credit-stage")
            : source.CreditReference?.RawStageCode??throw Missing("credit-stage");
        int secondary=Parameter(SimulationParameterKeys.Tactics.AbilitySecondaryBaseline);
        if (status!=1 && OfficerBehaviorRules.UsesOrdinaryPersonRules(behavior)==true && stage==1)
        {
            var options=state.NewGameOptions??throw Missing("captured-officer-setting");
            if(options.CapturedOfficersEnabled)
            {
                bool excluded=false;
                if(IsBandPerson(officerId))
                {
                    ushort location=current.ReadPersonFirstLocation(physical)??throw Missing("band-person-location");
                    if(location is >=1 and <=61)
                    {
                        int force=current.ReadPersonForce(physical)?.PhysicalSlot??throw Missing("band-person-force");
                        excluded=force is not (>=46 and <=50);
                    }
                }
                if(!excluded)secondary=Parameter(SimulationParameterKeys.Tactics.AbilitySecondaryAdjustment);
            }
        }
        // CPerson ctor/reset health161 is0; the normal static stream skips it.
        // A supplied explicit pre-start health owner takes precedence.
        var health=person.Health??OfficerHealth.Healthy;
        int healthPercent=OfficerHealthRules.AbilityModifier(health) switch
        {
            HealthAbilityModifier.Healthy=>Parameter(SimulationParameterKeys.Tactics.HealthyAbilityPercent),
            HealthAbilityModifier.InjuryLevel1=>Parameter(SimulationParameterKeys.Tactics.InjuryLevel1AbilityPercent),
            HealthAbilityModifier.InjuryLevel2=>Parameter(SimulationParameterKeys.Tactics.InjuryLevel2AbilityPercent),
            HealthAbilityModifier.InjuryLevel3=>Parameter(SimulationParameterKeys.Tactics.InjuryLevel3AbilityPercent),_=>0
        };
        var caps=new CapInput(current,physical);PersonAttributePostLoad.WriteCaps(physical,caps);
        var limits=person.AbilityState??OfficerAbilitySourceAdapter.Decode(physical,caps.Ordinary,caps.Charm,source.RawDoctrineId);
        if(limits.OrdinaryLimit is null)throw Missing("ordinary-cap");
        if(limits.CharmLimit is null)throw Missing("charm-cap");
        int? office=ReadOfficeId(officerId);
        var appointment=TacticRemakeCurrentAbilities.AppointmentFromFields(state,person.Id,person.ForceId,person.DistrictId,office,status)
            ??throw Missing("appointment");
        int doctrine=person.DoctrineId??(person.AbilityState is not null
            ? OfficerDoctrineRules.ReadDoctrineId(null,person.AbilityState):source.RawDoctrineId)??throw Missing("doctrine");
        var doctrineBonus=ReadDoctrine(state,physical,doctrine);
        return new(healthPercent,secondary,doctrineBonus,appointment,limits.OrdinaryLimit,limits.CharmLimit);
    }

    private OfficerAbilities ReadDoctrine(GameState state,int physical,int doctrine)
    {
        if(doctrine==0)return Zero;
        if(doctrine is <1 or >6)throw Missing("doctrine");
        int force=current.ReadPersonForce(physical)?.PhysicalSlot??throw Missing("doctrine-force");
        if(!loadedForceEffects.TryGetValue(force,out var initial))throw Missing("doctrine-force-constructor");
        var effects=(force==0?state.UnassignedForcePolicyEffects:state.Forces.GetValueOrDefault(force)?.PolicyEffects)??initial;
        for(int kind=0;kind<5;kind++)
        {
            int effect=kind+doctrine*5-3;
            if(effects.IsEnabled(effect) is not bool enabled)throw Missing("doctrine-activation."+effect);
            if(enabled)throw Missing("doctrine-current-panel."+effect);
        }
        return Zero;
    }

    private int Parameter(string key)=>parameters.TryGetValue(key,out int value)?value:throw Missing("parameter."+key);
    private static bool IsBandPerson(int id)=>unchecked((uint)(id-4111))<=9||unchecked((uint)(id-4131))<=9||
        unchecked((uint)(id-4151))<=9||unchecked((uint)(id-4171))<=9||unchecked((uint)(id-4191))<=9;
    private static InvalidDataException Missing(string field)=>new("new-game.ability."+field);
    private static readonly OfficerAbilities Zero=new(0,0,0,0,0);

    private sealed class CapInput(SourceWorldContext source,int physical):IPersonAttributePostLoadState
    {
        internal byte? Ordinary {get;private set;}
        internal byte? Charm {get;private set;}
        public int? ReadPersonPostLoadPhysicalSlot(int receiver)=>receiver==physical?physical:null;
        public ushort? ReadPersonIdentity(int receiver)=>receiver==physical?source.ReadPersonIdentity(receiver):null;
        public byte? ReadPersonPostLoadByte(int receiver,int offset)=>receiver!=physical?null:offset switch
        {0x224=>Ordinary,0x225=>Charm,_=>source.ReadPersonPostLoadByte(receiver,offset)};
        public void WritePersonPostLoadByte(int receiver,int offset,byte value)
        {
            if(receiver!=physical)throw Missing("cap-receiver");
            if(offset==0x224)Ordinary=value;else if(offset==0x225)Charm=value;else throw Missing("cap-field");
        }
    }
}
