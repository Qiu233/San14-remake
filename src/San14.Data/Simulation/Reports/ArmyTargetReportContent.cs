using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

/// <summary>675510's actual message slots. The marker is read separately,
/// after formatting, from the enemy Army's current Hex2A.</summary>
public sealed record ArmyTargetReportFormattingInputs(ushort SpeakerPersonOperand0,
    byte EnemyForceOperand0, ushort TargetBaseOperand0, ushort RulerPersonOperand1);
public sealed record ArmyTargetReportEntry(int ArmyPhysical, BaseReference DeduplicationCity,
    ArmyTargetReportFormattingInputs Formatting, string Text, int MarkerHex, bool LocalRecordAppended);
public sealed record ArmyTargetReportSnapshot(ImmutableArray<ArmyTargetReportEntry> Entries,
    bool Completed, ArmyTargetReportFormattingInputs? ReachedFormatting);

public sealed partial class ArmyCampaignRuleAdapter
{
    private static bool IsCurrentReportAdvisor(SourceWorldContext current,TransferPersonReference person,
        Func<string,InvalidDataException> missing)
    {
        int P(int? value,string field)=>value ?? throw missing(field);
        T K<T>(T? value,string field)where T:struct=>value ?? throw missing(field);
        ushort Identity(TransferPersonReference value)=>K(current.ReadPersonIdentity(P(value.PhysicalSlot,"Person_pointer")),"Person10");
        var force=current.ReadPersonForce(P(person.PhysicalSlot,"advisor_Person")) ?? throw missing("advisor_Force");
        if(!K(current.ReadForceNativeValidity(force),"advisor_Force_validity"))return false;
        ushort raw=K(current.ReadAssignmentForceAdvisor52(P(force.PhysicalSlot,"advisor_Force")),"advisor52");
        var advisor=(raw<6001?current.ReadPersonLookupReference(raw):current.ReadTransferPersonTableReference(0)) ?? throw missing("advisor_lookup");
        return advisor.PhysicalSlot is int at&&K(current.ReadPersonNativeValidity(at),"Person_validity")&&Identity(advisor)==Identity(person);
    }
    /// <summary>The second synthetic producer only, using the same retained
    /// speaker/current/pool and existing local slots. It does not clear counts,
    /// append final buckets or declare the eight-stage Action complete.</summary>
    public ArmyTargetReportContentOperation BeginArmyTargetReportContent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
        => new(this,state,current,pool);

    public sealed class ArmyTargetReportContentOperation
    {
        private readonly ArmyCampaignRuleAdapter package;
        private readonly SourceWorldContext current;
        private readonly WorldIndexContext pool;
        private readonly List<ArmyTargetReportEntry> entries=[];
        private bool entered,completed;
        private ArmyTargetReportFormattingInputs? formatting;
        public GameState State { get; private set; }
        public ArmyTargetReportSnapshot Snapshot => new(entries.ToImmutableArray(),completed,formatting);
        internal ArmyTargetReportContentOperation(ArmyCampaignRuleAdapter package,GameState state,
            SourceWorldContext current,WorldIndexContext pool)
        {
            if(!ReferenceEquals(pool.ArmyReturnOwner,current)||!pool.OwnsArmyReturnProfile())throw Missing("same_current_pool");
            this.package=package;State=state;this.current=current;this.pool=pool;
        }

        public ArmyTargetReportSnapshot Produce(TransferPersonReference speaker,ActionReportLocalRecords records)
        {
            ArgumentNullException.ThrowIfNull(records);
            if(entered)throw Missing("operation_already_entered");entered=true;
            if(!Valid(speaker)||!IsCurrentReportAdvisor(current,speaker,Missing)){completed=true;return Snapshot;}
            var actor=current.ReadPersonForce(P(speaker.PhysicalSlot,"speaker")) ?? throw Missing("actor_Force");
            var selection=new PersonDeathHistorySelection(State,current,pool,
                new TacticRemakeCurrentAbilities(current,package._scenario,package.Seed.Language,
                    SimulationRuleIdentity.ForLocale(package.Seed.Language)),package._governorInputs,
                (force,effect)=>package._baseCapacityInputs.ReadPanelModifier(State,force,effect));
            var primary=selection.PrimaryDistrict(actor);
            foreach(var value in current.ReadRegistryReferences(RegistryKind.Force) ?? throw Missing("Force_C0"))
            {
                if(value is not {Kind:RegistryKind.Force})throw Missing("Force_C0_payload");
                var other=new ForceReference(value.PhysicalSlot);
                int actorPhysical=K(current.ReadForcePhysicalSlot(actor),"actor_virtual60");
                int otherPhysical=K(current.ReadForcePhysicalSlot(other),"other_virtual60");
                if(otherPhysical==actorPhysical)continue;
                int relationIndex=K(current.ReadForcePhysicalSlot(other),"other_fresh_virtual60");
                if((Relation(actor,relationIndex)&0x7f)!=0)continue;
                //0C16A0(actor,1) is precisely actor.Flags12 bit1. It is not
                //an application/Title predicate, and other12 is short-circuited.
                if((Flags(actor)&2)!=0&&(Flags(other)&2)!=0)continue;
                HexListHandle? seen=null;
                try
                {
                    seen=Temporary();
                    //222FD0 reads manager60 head before capturing Force key.
                    var descriptor=current.ReadRegistryDescriptor(RegistryKind.Army) ?? throw Missing("Army60_descriptor");
                    var node=current.ReadRegistryHead(descriptor) ?? throw Missing("Army60_head");
                    int capturedKey=K(current.ReadForcePhysicalSlot(other),"Army_begin_captured_Force");
                    node=NextEligible(node,capturedKey);
                    while(true)
                    {
                        //223250 constructs a fresh end and reads this receiver
                        //even for an already-null begin or a limit break.
                        _=K(current.ReadForcePhysicalSlot(other),"Army_fresh_end_Force");
                        if(!node.Identity.HasValue)break;
                        var army=Army(node);
                        if(entries.Count>=int.MaxValue)break;
                        var target=current.ReadArmyLifecycleTargetFields(P(army.PhysicalSlot,"Army_pointer")) ?? throw Missing("Army_target_owner");
                        byte kind=K(target.Kind38,"Army38");ushort raw=K(target.Identity3A,"Army3A");
                        BaseReference? retained=null;
                        AreaReference? area=null;
                        if(kind==5)retained=current.ReadCityTableReference(raw<52?raw:0) ?? throw Missing("target_City");
                        else if(kind==6)retained=current.ReadGateTableReference(raw<11?raw:0) ?? throw Missing("target_Gate");
                        else if(kind==10)area=current.ReadAreaTableReference(raw<501?raw:0) ?? throw Missing("target_Area");
                        else if(kind==9)
                        {
                            var hex=current.ReadHexTableReference(raw<48400?raw:0) ?? throw Missing("target_Hex");
                            ushort areaKey=K(current.ReadHexAreaSlot(hex),"target_Hex12");
                            area=current.ReadAreaTableReference(areaKey<501?areaKey:0) ?? throw Missing("target_Hex_Area");
                        }
                        if(retained is not null||area is not null)
                        {
                            BaseReference ResolveCity()=>area is not null ? AreaCity(area) : kind==6
                                ? CurrentGateMapQueries.FindCity(retained!,current) : retained!;
                            var comparison=area is not null?AreaCity(area):retained!;
                            int district=K(current.ReadBaseDistrictSlot(comparison),"target_virtual78");
                            if(district==K(current.ReadDistrictPhysicalSlot(primary),"primary_physical"))
                            {
                                var dedup=ResolveCity();
                                if(!Contains(seen,dedup))
                                {
                                    //Area/Gate normalization is repeated at
                                    //the real append point, rather than cached.
                                    var appended=ResolveCity();Append(seen,appended);
                                    ushort person0=Identity(speaker);
                                    byte force0=current.ReadArmyUnitRawForceSlot(army);
                                    //The Gate branch formats its retained Gate
                                    //4E, while its dedup payload is normalized City.
                                    var messageBase=area is not null?AreaCity(area):retained!;
                                    ushort base0=K(current.ReadBaseObjectLink(messageBase),"message_Base4E");
                                    int forcePhysical=K(current.ReadForcePhysicalSlot(actor),"message_actor_virtual60");
                                    ushort person1=forcePhysical==0?(ushort)5014:
                                        K(current.ReadForceRecord(P(actor.PhysicalSlot,"message_actor"))?.RawRuler10,"message_ruler10");
                                    formatting=new(person0,force0,base0,person1);
                                    string text=package.FormatActionArmyTargetReport(State,current,formatting,value=>State=value).Text;
                                    ushort recordSpeaker=Identity(speaker);
                                    int marker=K(current.ReadArmyUnitRawHex2A(army),"record_Army2A");
                                    bool added=records.AppendArmyTargetWarning(recordSpeaker,marker,text);
                                    entries.Add(new(P(army.PhysicalSlot,"record_Army"),appended,formatting,text,
                                        Math.Clamp(marker,0,48399),added));
                                }
                            }
                        }
                        node=current.ReadRegistryNodeNext(node) ?? throw Missing("Army_live_next");
                        node=NextEligible(node,capturedKey);
                    }
                }
                finally{if(seen is not null)Release(seen);}
            }
            completed=true;return Snapshot;
        }

        private RegistryListNodeReference NextEligible(RegistryListNodeReference node,int key)
        {
            while(node.Identity.HasValue)
            {
                if(unchecked((uint)(key-1))<51)
                {
                    var army=Army(node);
                    if(current.ReadArmyUnitRawForceSlot(army)==key&&
                        !K(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(State,current,P(army.PhysicalSlot,"filter_Army")),"Army233560"))return node;
                }
                node=current.ReadRegistryNodeNext(node) ?? throw Missing("Army_filter_live_next");
            }
            return node;
        }
        private ArmyUnitReference Army(RegistryListNodeReference node)
        {
            var value=current.ReadRegistryNodePayload(node);
            return value is {Kind:RegistryKind.Army}?new(value.PhysicalSlot):throw Missing("Army_node_payload");
        }
        private BaseReference AreaCity(AreaReference area)
        {byte raw=K(current.ReadAreaCitySlot(area),"Area35");return current.ReadCityTableReference(raw<52?raw:0) ?? throw Missing("Area_City");}
        private byte Relation(ForceReference actor,int index)
            => ReadReportForceRelationByte(State,actor,index,Missing);
        private byte Flags(ForceReference force)
        {
            int physical=P(force.PhysicalSlot,"flags_Force");
            byte? flags=State.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(physical,out var reset)==true
                ?reset[0x12]:State.NativeArmySimulation?.Forces.GetValueOrDefault(physical)?.Flags12;
            return K(flags,"Force12");
        }
        private bool Valid(TransferPersonReference person)=>person.PhysicalSlot is int at&&K(current.ReadPersonNativeValidity(at),"Person_validity");
        private ushort Identity(TransferPersonReference person)=>K(current.ReadPersonIdentity(P(person.PhysicalSlot,"Person_pointer")),"Person10");
        private bool Contains(HexListHandle list,BaseReference city)
        {
            var node=current.ReadRegistryHead(new(list.Identity)) ?? throw Missing("dedup_head");
            while(node.Identity.HasValue)
            {
                var value=current.ReadRegistryNodePayload(node);
                var pointer=value is {Kind:RegistryKind.City} ?new BaseReference(BaseKind.City,value.PhysicalSlot):throw Missing("dedup_payload");
                if(pointer==city)return true;
                node=current.ReadRegistryNodeNext(node) ?? throw Missing("dedup_live_next");
            }
            return false;
        }
        private HexListHandle Temporary(){Enter();var value=current.CreateTemporaryHexList() ?? throw Missing("temporary_descriptor");Exit();return value;}
        private void Append(HexListHandle list,BaseReference value)
        {Enter();var node=current.AllocateRegistryNode(new(list.Identity)) ?? throw Missing("temporary_node");if(node.Identity.HasValue)current.WriteRegistryNodePayload(node,new(RegistryKind.City,value.PhysicalSlot));Exit();}
        private void Release(HexListHandle list){Enter();current.ClearTemporaryHexList(list);current.ReleaseTemporaryHexList(list);Exit();}
        private void Enter(){if(K(current.ReadRegistryLockEnabled(),"lock_enabled"))current.EnterRegistryLock();}
        private void Exit(){if(K(current.ReadRegistryLockEnabled(),"unlock_enabled"))current.ExitRegistryLock();}
        private static T K<T>(T? value,string field)where T:struct=>value ?? throw Missing(field);
        private static int P(int? value,string field)=>value ?? throw Missing(field);
        private static InvalidDataException Missing(string field)=>new("report.army-target."+field);
    }
}
