using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

/// <summary>6749F0's four actual formatter slots. The indexed low-seven-bit
/// counter is read afresh after the independent Force operand read.</summary>
public sealed record AllianceExpiryReportFormattingInputs(ushort SpeakerPersonOperand0,
    int OtherForceOperand0,int RemainingAllianceCounterInteger0,ushort RulerPersonOperand1);
public sealed record AllianceExpiryReportEntry(ForceReference OtherForce,
    AllianceExpiryReportFormattingInputs Formatting,string Text,BaseReference MarkerBase,
    int MarkerHex,bool LocalRecordAppended);
public sealed record AllianceExpiryReportSnapshot(ImmutableArray<AllianceExpiryReportEntry> Entries,
    bool Completed,AllianceExpiryReportFormattingInputs? ReachedFormatting,string? ReachedFormattedText);

public sealed partial class ArmyCampaignRuleAdapter
{
    //The existing saved EA authority, shared by the second and fourth report
    //consumers. The optional model-query bank has no saved-capture producer.
    //Keep each caller's original Unknown prefix and each reached fresh read.
    private static byte ReadReportForceRelationByte(GameState state,ForceReference actor,int index,
        Func<string,InvalidDataException> missing)
    {
        if((uint)index>=52)throw missing("relation_index");
        int physical=actor.PhysicalSlot ?? throw missing("relation_actor");
        byte[]? values=physical==0?state.NativeForceUnregisterCurrent?.PlaceholderRelationsEA:
            state.NativeArmySimulation?.Forces.GetValueOrDefault(physical)?.RelationsEA;
        return values is {Length:52}?values[index]:throw missing("actor_EA");
    }

    public ForceAllianceExpiryReportContentOperation BeginForceAllianceExpiryReportContent(GameState state,
        SourceWorldContext current,WorldIndexContext pool)=>new(this,state,current,pool);

    public sealed class ForceAllianceExpiryReportContentOperation
    {
        private readonly ArmyCampaignRuleAdapter package;
        private readonly SourceWorldContext current;
        private readonly WorldIndexContext pool;
        private readonly List<AllianceExpiryReportEntry> entries=[];
        private bool entered,completed;
        private AllianceExpiryReportFormattingInputs? formatting;
        private string? formattedText;
        public GameState State { get; private set; }
        public AllianceExpiryReportSnapshot Snapshot=>new(entries.ToImmutableArray(),completed,formatting,formattedText);

        internal ForceAllianceExpiryReportContentOperation(ArmyCampaignRuleAdapter package,GameState state,
            SourceWorldContext current,WorldIndexContext pool)
        {
            if(!ReferenceEquals(pool.ArmyReturnOwner,current)||!pool.OwnsArmyReturnProfile())throw Missing("same_current_pool");
            this.package=package;State=state;this.current=current;this.pool=pool;
        }

        public AllianceExpiryReportSnapshot Produce(TransferPersonReference speaker,ActionReportLocalRecords records,
            int limit=int.MaxValue)
        {
            ArgumentNullException.ThrowIfNull(records);
            if(entered)throw Missing("operation_already_entered");entered=true;
            if(speaker.PhysicalSlot is not int speakerPhysical||!K(current.ReadPersonNativeValidity(speakerPhysical),"Person_validity"))
            {completed=true;return Snapshot;}
            //World3A and its primary are queried again at the real fourth call;
            //neither the retained speaker's Force nor the prior stage is reused.
            var worldActor=OwnedCurrentPlayerForce.ReadCurrentReference(State,current) ?? throw Missing("World3A_Force");
            var selection=new PersonDeathHistorySelection(State,current,pool,
                new TacticRemakeCurrentAbilities(current,package._scenario,package.Seed.Language,
                    SimulationRuleIdentity.ForLocale(package.Seed.Language)),package._governorInputs,
                (force,effect)=>package._baseCapacityInputs.ReadPanelModifier(State,force,effect));
            var primary=selection.PrimaryDistrict(worldActor);
            if(!K(current.ReadDistrictNativeValidity(primary),"primary_District_validity"))
            {completed=true;return Snapshot;}
            byte raw=K(current.ReadDistrictForceSlot(primary),"primary_District10");
            var actor=current.ReadForceTableReference(raw<52?raw:0) ?? throw Missing("primary_Force");
            if(!K(current.ReadForceNativeValidity(actor),"primary_Force_validity"))
            {completed=true;return Snapshot;}

            var descriptor=current.ReadRegistryDescriptor(RegistryKind.Force) ?? throw Missing("Force_C0_descriptor");
            var node=current.ReadRegistryHead(descriptor) ?? throw Missing("Force_C0_head");
            while(node.Identity.HasValue)
            {
                //The source obtains payload BEFORE the limit test. No extra
                //self/other-validity, coalition or Army exclusion gate exists.
                var other=Force(node);
                if(entries.Count>=limit)break;
                int first=Ordinal(other,"predicate_first_other_virtual60");
                if((ReadReportForceRelationByte(State,actor,first,Missing)&0x7f)!=0)
                {
                    int second=Ordinal(other,"predicate_second_other_virtual60");
                    if((ReadReportForceRelationByte(State,actor,second,Missing)&0x7f)<=1)
                    {
                        formatting=null;formattedText=null;
                        ushort person0=Identity(speaker);
                        int force0=Ordinal(other,"message_other_virtual60");
                        int relationIndex=Ordinal(other,"message_relation_other_virtual60");
                        int counter=ReadReportForceRelationByte(State,actor,relationIndex,Missing)&0x7f;
                        int actorOrdinal=Ordinal(actor,"message_actor_virtual60");
                        ushort person1=actorOrdinal==0?(ushort)5014:
                            K(current.ReadForceRecord(P(actor.PhysicalSlot,"message_actor"))?.RawRuler10,"message_ruler10");
                        formatting=new(person0,force0,counter,person1);
                        string text=package.FormatActionAllianceExpiryReport(State,current,formatting,value=>State=value).Text;
                        formattedText=text;
                        ushort recordSpeaker=Identity(speaker);
                        var home=RulerHome(other);
                        int marker=ReadReportBaseMarker(current,home,Missing);
                        bool added=records.AppendAllianceExpiryWarning(recordSpeaker,marker,text);
                        //The per-producer count advances even when local261 is
                        //full, after formatting and all fresh marker queries.
                        entries.Add(new(other,formatting,text,home,Math.Clamp(marker,0,48399),added));
                    }
                }
                node=current.ReadRegistryNodeNext(node) ?? throw Missing("Force_C0_live_next");
            }
            completed=true;return Snapshot;
        }

        private ForceReference Force(RegistryListNodeReference node)
        {
            var value=current.ReadRegistryNodePayload(node);
            return value is {Kind:RegistryKind.Force}?new(value.PhysicalSlot):throw Missing("Force_C0_payload");
        }
        private BaseReference RulerHome(ForceReference other)
        {
            //22EA10 reads this retained receiver's raw ruler, independently
            //of P4's Force-table lookup and without Person native-validity.
            ushort raw=K(current.ReadForceRecord(P(other.PhysicalSlot,"marker_Force"))?.RawRuler10,"marker_ruler10");
            var person=(raw<=6000?current.ReadPersonLookupReference(raw):current.ReadTransferPersonTableReference(0))
                ?? throw Missing("marker_ruler_lookup");
            ushort first=K(current.ReadPersonFirstLocation(P(person.PhysicalSlot,"marker_ruler_pointer")),"marker_ruler_E2");
            return BaseLookup.Find(first,current);
        }
        private int Ordinal(ForceReference force,string field)
        {
            _=P(force.PhysicalSlot,field+"_pointer");
            return K(current.ReadForcePhysicalSlot(force),field);
        }
        private ushort Identity(TransferPersonReference person)=>K(current.ReadPersonIdentity(P(person.PhysicalSlot,"Person_pointer")),"Person10");
        private static T K<T>(T? value,string field)where T:struct=>value ?? throw Missing(field);
        private static int P(int? value,string field)=>value ?? throw Missing(field);
        private static InvalidDataException Missing(string field)=>new("report.alliance-expiry."+field);
    }
}
