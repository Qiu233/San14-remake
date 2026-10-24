using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

/// <summary>674DF0's original slots. Marker lookup is a separate fresh read
/// after formatting and the second speaker identity read.</summary>
public sealed record BaseNearbyEnemyReportFormattingInputs(ushort SpeakerPersonOperand0,
    ushort FootholdBaseOperand0, ushort RulerPersonOperand1);
public sealed record BaseNearbyEnemyReportEntry(BaseReference Receiver,
    BaseNearbyEnemyReportFormattingInputs Formatting, string Text, int MarkerHex, bool LocalRecordAppended);
public sealed record BaseNearbyEnemyReportSnapshot(ImmutableArray<BaseNearbyEnemyReportEntry> Entries,
    bool Completed, BaseNearbyEnemyReportFormattingInputs? ReachedFormatting, string? ReachedFormattedText);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The actual third synthetic call, over the retained speaker and
    /// ordinary local slots. It neither resets counts nor completes Action.</summary>
    public BaseNearbyEnemyReportContentOperation BeginBaseNearbyEnemyReportContent(GameState state,
        SourceWorldContext current, WorldIndexContext pool) => new(this,state,current,pool);

    //22D880 returns48400 for an invalid Object. The ordinary record writer,
    //rather than a Hex lookup fallback, performs the final0..48399 clamp.
    private static int ReadReportBaseMarker(SourceWorldContext current,BaseReference receiver,
        Func<string,InvalidDataException> missing)
    {
        ushort link=current.ReadBaseObjectLink(receiver) ?? throw missing("marker_Base4E");
        var obj=current.ReadObjectTableReference(link<=3000?link:0) ?? throw missing("marker_Object");
        bool valid=current.ReadObjectNativeValidity(obj) ?? throw missing("marker_Object_validity");
        return valid ? current.ReadObjectHexIndex(obj) ?? throw missing("marker_Object10") : 48400;
    }

    public sealed class BaseNearbyEnemyReportContentOperation
    {
        private readonly ArmyCampaignRuleAdapter package;
        private readonly SourceWorldContext current;
        private readonly WorldIndexContext pool;
        private readonly List<BaseNearbyEnemyReportEntry> entries=[];
        private bool entered,completed;
        private BaseNearbyEnemyReportFormattingInputs? formatting;
        private string? formattedText;
        public GameState State { get; private set; }
        public BaseNearbyEnemyReportSnapshot Snapshot => new(entries.ToImmutableArray(),completed,formatting,formattedText);

        internal BaseNearbyEnemyReportContentOperation(ArmyCampaignRuleAdapter package,GameState state,
            SourceWorldContext current,WorldIndexContext pool)
        {
            if(!ReferenceEquals(pool.ArmyReturnOwner,current)||!pool.OwnsArmyReturnProfile())throw Missing("same_current_pool");
            this.package=package;State=state;this.current=current;this.pool=pool;
        }

        public BaseNearbyEnemyReportSnapshot Produce(TransferPersonReference speaker,
            ActionReportLocalRecords records,int limit=int.MaxValue)
        {
            ArgumentNullException.ThrowIfNull(records);
            if(entered)throw Missing("operation_already_entered");entered=true;
            if(speaker.PhysicalSlot is not int speakerPhysical||!K(current.ReadPersonNativeValidity(speakerPhysical),"Person_validity"))
            {completed=true;return Snapshot;}
            //32EF60 rereads World3A/22E930, independently of the retained
            //speaker's Force; it adds no actor native-validity/advisor gate.
            var actor=OwnedCurrentPlayerForce.ReadCurrentReference(State,current) ?? throw Missing("World3A_Force");
            var selection=new PersonDeathHistorySelection(State,current,pool,
                new TacticRemakeCurrentAbilities(current,package._scenario,package.Seed.Language,
                    SimulationRuleIdentity.ForLocale(package.Seed.Language)),package._governorInputs,
                (force,effect)=>package._baseCapacityInputs.ReadPanelModifier(State,force,effect));
            var primary=selection.PrimaryDistrict(actor);
            if(!K(current.ReadDistrictNativeValidity(primary),"primary_District_validity"))
            {completed=true;return Snapshot;}
            byte rawForce=K(current.ReadDistrictForceSlot(primary),"primary_District10");
            var force=current.ReadForceTableReference(rawForce<52?rawForce:0) ?? throw Missing("primary_Force");
            if(!K(current.ReadForceNativeValidity(force),"primary_Force_validity"))
            {completed=true;return Snapshot;}

            //229600 resolves manager80 head before capturing its District key.
            //The iterator has stack state only; no shared temporary is allocated.
            var descriptor=current.ReadBaseRegistryHandle() ?? throw Missing("Base80_descriptor");
            var node=current.ReadHexListHead(new(descriptor.Identity)) ?? throw Missing("Base80_head");
            int key=K(current.ReadDistrictPhysicalSlot(primary),"Base_begin_captured_District");
            node=NextEligible(node,key);
            while(true)
            {
                //2297A0 constructs a fresh null end, including for empty begin.
                _=K(current.ReadDistrictPhysicalSlot(primary),"Base_fresh_end_District");
                if(!node.Identity.HasValue)break;
                var receiver=Base(node);
                if(entries.Count>=limit)break;
                if(ReadNearbyEnemy(receiver))
                {
                    formatting=null;formattedText=null;
                    ushort person0=Identity(speaker);
                    ushort base0=K(current.ReadBaseObjectLink(receiver),"message_Base4E");
                    int ordinal=K(current.ReadForcePhysicalSlot(force),"message_Force_virtual60");
                    ushort person1=ordinal==0?(ushort)5014:
                        K(current.ReadForceRecord(P(force.PhysicalSlot,"message_Force"))?.RawRuler10,"message_ruler10");
                    formatting=new(person0,base0,person1);
                    string text=package.FormatActionBaseNearbyReport(State,current,formatting,value=>State=value).Text;
                    formattedText=text;
                    ushort recordSpeaker=Identity(speaker);
                    int marker=ReadReportBaseMarker(current,receiver,Missing);
                    bool added=records.AppendBaseNearbyWarning(recordSpeaker,marker,text);
                    //Attempts advance after the same reached reads even when
                    //the independent261-slot ordinary storage is already full.
                    entries.Add(new(receiver,formatting,text,Math.Clamp(marker,0,48399),added));
                }
                node=current.ReadHexListNodeNext(node) ?? throw Missing("Base_live_next");
                node=NextEligible(node,key);
            }
            completed=true;return Snapshot;
        }

        private HexListNodeReference NextEligible(HexListNodeReference node,int key)
        {
            while(node.Identity.HasValue)
            {
                if(unchecked((uint)(key-1))<51&&K(current.ReadBaseDistrictSlot(Base(node)),"Base_virtual78")==key)return node;
                node=current.ReadHexListNodeNext(node) ?? throw Missing("Base_filter_live_next");
            }
            return node;
        }
        private BaseReference Base(HexListNodeReference node)
        {
            var value=current.ReadBaseRegistryNodePayload(new(node.Identity)) ?? throw Missing("Base_payload");
            return value.Kind is BaseKind.City or BaseKind.Gate?value:throw Missing("Base_payload_kind");
        }
        private bool ReadNearbyEnemy(BaseReference receiver)
        {
            //Existing saved bit0 of Base67. Physical ownership follows the
            //existing CE City/Gate binding, never a premature mutable4E read.
            int physical=P(receiver.PhysicalSlot,"Base_pointer");
            bool? value=receiver.Kind switch
            {
                BaseKind.City when (uint)physical<52 => physical==0?State.UnassignedCityHasNearbyEnemyArmy:
                    State.Cities.GetValueOrDefault(physical)?.HasNearbyEnemyArmy,
                BaseKind.Gate when (uint)physical<11 => physical==0?State.UnassignedGateHasNearbyEnemyArmy:
                    State.Gates.GetValueOrDefault(physical+51)?.HasNearbyEnemyArmy,
                _ => null
            };
            return K(value,"Base67_nearby_enemy");
        }
        private ushort Identity(TransferPersonReference person)=>K(current.ReadPersonIdentity(P(person.PhysicalSlot,"Person_pointer")),"Person10");
        private static T K<T>(T? value,string field)where T:struct=>value ?? throw Missing(field);
        private static int P(int? value,string field)=>value ?? throw Missing(field);
        private static InvalidDataException Missing(string field)=>new("report.base-nearby."+field);
    }
}
