using San14.Core;

namespace San14.Data;

public sealed record OriginalArmyMoraleSourceEpoch(string Language,string RuleVersion,
    string ParameterRevision,string ClockRevision,long UpdateOrdinal,int ArmyId,int? NativeArmySlot,
    string DefinitionSourceUri,string DefinitionSourceSha256,string WriterRevision);
public sealed record OriginalArmyMoraleSourceReceipt(byte After1A,byte? Before1A,int? Requested,
    int? CurrentGetterCap,int? DefinitionCap,int? SetterCap,OriginalBaseReference? SelectedBase,
    OriginalArmyMoraleSourceEpoch Epoch,string NativeWriter);

/// <summary>Source Army byte1A, independent of the provisional Core Morale.
/// Creation reads the saved native Base44 owner through the current registered
/// lookup; current queries read only the saved Army byte. Unknown aftermath
/// remains nullable. Current Force caps are queried through the paired owner.</summary>
public sealed class OriginalArmyMoraleSource
{
    private readonly OriginalTacticRemakeCurrentContext current;
    private readonly OriginalEventInitializationContext definitions;
    public string Revision { get; }
    public OriginalArmyMoraleSource(OriginalTacticRemakeCurrentContext current,
        OriginalEventInitializationContext currentDefinitions,string revision)
    {
        this.current=current??throw new ArgumentNullException(nameof(current));
        definitions=currentDefinitions??throw new ArgumentNullException(nameof(currentDefinitions));
        if(string.IsNullOrWhiteSpace(revision))throw new InvalidDataException("Army morale source writer revision is missing.");
        Revision=revision;
    }
    public static string BaseMorale44Key(OriginalBaseReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if(reference.Kind is not(OriginalBaseKind.City or OriginalBaseKind.Gate)||reference.PhysicalSlot is null or <0||
            reference.Kind==OriginalBaseKind.City&&reference.PhysicalSlot>51||reference.Kind==OriginalBaseKind.Gate&&reference.PhysicalSlot>10)
            throw new InvalidDataException("Army morale Base44 owner requires a concrete native kind and physical slot.");
        return $"native-base-morale44:{(reference.Kind==OriginalBaseKind.City?"city":"gate")}:{reference.PhysicalSlot}";
    }
    public byte? ReadCurrentArmyMorale(GameState state,UnitState army)=>
        Epoch(state,army,true) is not null?army.NativeMorale1A:null;

    public OriginalArmyMoraleSourceReceipt? ReadCurrentReceipt(GameState state,UnitState army)
    {
        var epoch=Epoch(state,army,true);
        if(epoch is null||army.NativeMorale1A is not byte raw||current.ForceMoraleMaximum(state,army.ForceId) is not int cap)return null;
        return new(raw,raw,Math.Min(raw,cap),cap,null,null,null,epoch,"raw1A + getter22EDA0/Force268DD0");
    }
    public byte? ResolveCreationMorale(GameState state,PreparedDeployment deployment,UnitState army)=>
        ResolveCreationReceipt(state,deployment,army)?.After1A;

    public OriginalArmyMoraleSourceReceipt? ResolveCreationReceipt(GameState state,PreparedDeployment deployment,UnitState army)
    {
        var epoch=Epoch(state,army,false);
        if(epoch is null||state.Units.ContainsKey(army.Id)||army.OfficerId!=deployment.OfficerId||
            army.SourceLocationId!=deployment.SourceLocationId||army.ForceId!=deployment.ForceId||
            ReadCommanderValidity(state,army) is not true||ResolveCommanderBase(state,army) is not { } selected)return null;
        bool? valid=definitions.ReadBaseNativeValidity(selected);
        if(valid is null)return null;
        // Generic2D2D2B skips the setter for an invalid Base. The fresh reset
        // explicitly wrote Army1A=0 before the creator selected the receiver.
        if(!valid.Value)return new(0,null,null,null,null,null,selected,epoch,"fresh reset226C50; invalid Base skips2D2D35");
        int saved44;
        if(selected.Kind==OriginalBaseKind.City)
        {
            //The actual creator loads byte[R15+44] after the registered Base
            //lookup (SC2D2D2D/TC2D2EAD). A City's current owner is the saved
            //return runtime, not the once-only parameter seed or an older
            //operation capture. Core City.Morale remains independent.
            if(selected.PhysicalSlot is not int citySlot||
                !state.Cities.TryGetValue(citySlot,out CityState? city)||city.Id!=citySlot||
                city.NativeReturnRuntime?.Morale44 is not byte owned44)return null;
            saved44=owned44;
        }
        else if(selected.Kind==OriginalBaseKind.Gate)
        {
            //A Gate has no independent current Base44 projection in Core yet.
            //Keep its explicitly saved typed owner; never infer it from City,
            //a generic morale number or a freshly recaptured scenario row.
            string key=BaseMorale44Key(selected);
            if(state.NativeArmySimulation!.Parameters.Float32Values.ContainsKey(key)||
                !state.NativeArmySimulation.Parameters.Int32Values.TryGetValue(key,out saved44)||saved44 is <0 or >255)return null;
        }
        else return null;
        if(current.ForceMoraleMaximum(state,army.ForceId) is not int cap)return null;
        return new(SetByte2419A0(saved44,cap),null,saved44,null,null,cap,selected,epoch,"2D2D2D Base44 ->2419A0");
    }

    public byte? ResolveLogisticsMorale(GameState state,UnitState army,bool connected)=>
        ResolveLogisticsReceipt(state,army,connected)?.After1A;

    public OriginalArmyMoraleSourceReceipt? ResolveLogisticsReceipt(GameState state,UnitState army,bool connected)
    {
        var epoch=Epoch(state,army,true);
        if(epoch is null||army.NativeMorale1A is not byte raw||army.NativeTacticRuntime is not { } runtime)return null;
        bool former=(runtime.Flags26&2)!=0;
        if(former==connected)return new(raw,raw,null,null,null,null,null,epoch,"276AB0 unchanged logistics bit; retain1A");
        //TC276DB0 reconnect uses the same three-cap writer for type1/2.
        //Transport301DD0 changes48/1F8, never this independent1A.
        //The distinct1->0 morale callback remains outside this finite owner.
        if(runtime.Status10 is not (1 or 2)||former||!connected)return null;
        int? definitionCap=current.ForceMoraleMaximum(state,army.ForceId);
        int? getterCap=current.ForceMoraleMaximum(state,army.ForceId);
        if(definitionCap is null||getterCap is null||!state.NativeArmySimulation!.Parameters.Int32Values.TryGetValue(
            SimulationParameterKeys.Morale.LogisticsGain,out int gain))return null;
        int requested=Math.Min(unchecked(Math.Min((int)raw,getterCap.Value)+gain),definitionCap.Value);
        int? setterCap=current.ForceMoraleMaximum(state,army.ForceId);
        if(setterCap is null)return null;
        return new(SetByte2419A0(requested,setterCap.Value),raw,requested,getterCap,definitionCap,setterCap,null,epoch,
            "277021..277059 reconnect ->2419A0");
    }

    /// <summary>Signed request branch, signed Force cap, then BL low-byte store.
    /// A negative cap can wrap when a nonnegative request reaches the store.</summary>
    public static byte SetByte2419A0(int requested,int currentForceCap)=>requested<0? (byte)0:
        unchecked((byte)Math.Min(requested,currentForceCap));
    public static byte ReconnectByte276AB0(byte current1A,int currentGetterCap,int definitionCap,int gain,int setterCap)=>
        SetByte2419A0(Math.Min(unchecked(Math.Min((int)current1A,currentGetterCap)+gain),definitionCap),setterCap);

    /// <summary>Current Person lookup identity, owned native validity fields and
    /// first11A/E2 feed the actual registered-Base lookup. A known invalid Person
    /// returns the native null Base receiver; missing ownership stays unknown.</summary>
    public OriginalBaseReference? ResolveCommanderBase(GameState state,UnitState army)
    {
        if(Epoch(state,army,state.Units.ContainsKey(army.Id)) is null)return null;
        bool? valid=ReadCommanderValidity(state,army);
        if(valid is null)return null;
        if(!valid.Value)return new(OriginalBaseKind.City,null);
        int physical=state.Officers[army.OfficerId].NativeAbilityRuntime!.PhysicalSlot;
        if(definitions.ReadPersonFirstLocation(physical) is not ushort first)return null;
        try{return OriginalBaseLookup.Find(first,definitions);}
        catch(InvalidDataException){return null;}
    }

    private bool? ReadCommanderValidity(GameState state,UnitState army)
    {
        if(army.OfficerId is <0 or >6000||state.NativeArmySimulation?.Parameters is not { } parameters||definitions.StaticSource is not { } source||
            !state.Officers.TryGetValue(army.OfficerId,out OfficerState? officer)||
            officer.NativeAbilityRuntime is not { } person||person.Revision!=OriginalTacticRemakeCurrentAbilities.ProfileId||
            person.Language!=parameters.Language||person.RuleVersion!=parameters.RuleVersion||
            person.SourceScenarioUri!=source.ResourceUri||person.SourceScenarioSha256!=source.Sha256||
            person.PhysicalSlot is <1 or >1850||definitions.ReadPersonLookupReference(army.OfficerId)?.PhysicalSlot!=person.PhysicalSlot||
            definitions.ReadPersonIdentity(person.PhysicalSlot)!=army.OfficerId||
            person.Status11E is not byte status||person.Flags196 is not ushort flags||person.Extra1FD is not byte tail)return null;
        return (flags&0x4000)!=0?tail!=0:unchecked((uint)(army.OfficerId-5001))<=99||status!=0;
    }

    private OriginalArmyMoraleSourceEpoch? Epoch(GameState state,UnitState army,bool existing)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(army);
        if(!current.IsBoundCurrentState(state)||state.NativeClock is not { } clock||state.NativeArmySimulation?.Parameters is not { } parameters||
            string.IsNullOrWhiteSpace(clock.Revision)||string.IsNullOrWhiteSpace(parameters.Revision)||
            definitions.StaticSource is not { } source||string.IsNullOrWhiteSpace(source.ResourceUri)||
            state.ContentHashes.GetValueOrDefault(source.ResourceUri)!=source.Sha256||
            parameters.Language is not("SC" or "TC"))return null;
        if(existing&&(state.Units.GetValueOrDefault(army.Id)!=army||current.ArmyValid(state,army)!=true||army.NativeArmyOrigin is not { } origin||
            origin.Language!=parameters.Language||origin.RuleVersion!=parameters.RuleVersion||
            origin.NativeSlotIndex is null or <1 or >500||army.NativeTacticRuntime is null))return null;
        if(state.NativeArmySimulation.World.SamplerEpoch is { } sampler&&
            (sampler.NativeClockRevision!=clock.Revision||sampler.SourceScenarioUri!=source.ResourceUri||sampler.SourceScenarioSha256!=source.Sha256))return null;
        return new(parameters.Language,parameters.RuleVersion,parameters.Revision,clock.Revision,clock.UpdateOrdinal,
            army.Id,existing?army.NativeArmyOrigin!.NativeSlotIndex:null,source.ResourceUri,source.Sha256,Revision);
    }
}
