using San14.Core;

namespace San14.Data;

/// <summary>Operation-time application predicates. Null providers/results remain
/// unknown at the native gate; neither predicate has an ordinary-mode default.</summary>
public sealed record AreaReportApplicationInputs(
    Func<bool?>? TutorialBlocksAreaReports = null,
    Func<bool?>? SpecialModeSuppressesSceneryReport = null);

public enum AreaReportOrigin { Person, Scenery, Item }
public sealed record AreaReportSelection(AreaReference? Area, int First, int Second, int Third, int Reserved);
/// <summary>Page-local formatting intent, separate from the persisted history ring.
/// The presentation owner expands the actual person0/base0/person1 operands and retains record
/// flag bits, capacity and formatted-text lifetime.</summary>
public sealed record AreaReportContentSnapshot(int AreaPhysical, AreaReportOrigin Origin,
    int MessageId, ushort SpeakerPersonOperand0, ushort CityBaseOperand0, ushort RulerPersonOperand1, int MarkerHex,
    int Category = 1, int District = 0, int Day = 0);

public sealed partial class ArmyCampaignRuleAdapter
{
    public AreaReportContentOperation BeginAreaReportContent(GameState state,
        AreaReportApplicationInputs? application = null)
    {
        var current = CaptureReportContentCurrent(state);
        return BeginAreaReportContent(state, current,
            current.ReadOwnedCurrentListPool() ?? throw new InvalidDataException("report.area.current_pool"), application);
    }

    /// <summary>Uses the caller's actual operation-local current and pool. No
    /// second capture, registry seed, tutorial ACK or random stream is installed.</summary>
    public AreaReportContentOperation BeginAreaReportContent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, AreaReportApplicationInputs? application = null)
        => new(this, state, current, pool, application ?? new());

    public sealed class AreaReportContentOperation
    {
        private readonly ArmyCampaignRuleAdapter package;
        private readonly SourceWorldContext current;
        private readonly WorldIndexContext pool;
        private readonly AreaReportApplicationInputs application;
        public GameState State { get; private set; }
        public AreaReportContentSnapshot? Snapshot { get; private set; }
        public AreaReportSelection? Selection { get; private set; }
        public AreaReportFormattingInputs? FormattingInputs { get; private set; }
        public string? FormattedText { get; private set; }
        internal AreaReportContentOperation(ArmyCampaignRuleAdapter package, GameState state,
            SourceWorldContext current, WorldIndexContext pool, AreaReportApplicationInputs application)
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile()) throw Missing("same_current_pool");
            this.package=package; State=state; this.current=current; this.pool=pool; this.application=application;
            AreaFlagsSourceAdapter.BindMutation(current, ()=>State, value=>State=value);
        }

        private PersonDeathHistorySelection SpeakerSelector() => new(State, current, pool,
            new TacticRemakeCurrentAbilities(current, package._scenario, package.Seed.Language,
                SimulationRuleIdentity.ForLocale(package.Seed.Language)), package._governorInputs,
            (force,effect)=>package._baseCapacityInputs.ReadPanelModifier(State,force,effect));

        /// <summary>The actual parent actor/primary/speaker path, followed by
        /// this producer. Other seven synthetic producers remain separate.</summary>
        public AreaReportContentSnapshot? Produce()
        {
            var actor=OwnedCurrentPlayerForce.ReadCurrentReference(State,current) ?? throw Missing("actor");
            var speaker=SpeakerSelector().Select(SpeakerSelector().PrimaryDistrict(actor));
            return Produce(speaker);
        }

        /// <summary>6773F0's supplied speaker receiver from the enclosing Action.
        /// Returns one formatting intent, or known no record. An exception leaves
        /// State containing every reached Area store and all temporaries retired.</summary>
        public AreaReportContentSnapshot? Produce(TransferPersonReference speaker)
            => ProduceCore(speaker,null,null);

        internal AreaReportContentSnapshot? ProduceWithLocalRecord(TransferPersonReference speaker,
            Func<GameState,AreaReportFormattingInputs,Action<GameState>,string> format,
            Action<ushort,int,string> append)
            => ProduceCore(speaker,format,append);

        private AreaReportContentSnapshot? ProduceCore(TransferPersonReference speaker,
            Func<GameState,AreaReportFormattingInputs,Action<GameState>,string>? format,
            Action<ushort,int,string>? append)
        {
            Snapshot=null; Selection=null; FormattingInputs=null; FormattedText=null;
            if (!Valid(speaker) || !Advisor(speaker)) return null;
            if (Gate(application.TutorialBlocksAreaReports,"tutorial_before_clear")) return null;
            var speakerForce=current.ReadPersonForce(P(speaker.PhysicalSlot,"speaker")) ?? throw Missing("speaker_Force");
            foreach(var area in current.ReadRegisteredAreaReferences() ?? throw Missing("Area_B0"))
            {
                var reached=area ?? throw Missing("Area_B0_payload");
                current.WriteAreaFlags38(reached,(byte)(K(current.ReadAreaFlags38(reached),"Area38_clear") & 0x7f));
            }
            HexListHandle? output=null;
            try
            {
                output=Temporary(); Selection=Select(output);
                var selected=Selection.Area;
                if(selected is null || !K(current.ReadAreaNativeValidity(selected),"selected_validity")) return null;
                var city=City(selected);
                int message;
                AreaReportOrigin origin;
                if(K(current.ReadBaseNativeValidity(city),"selected_City_validity") && Selection.First!=0)
                { message=0x500f; origin=AreaReportOrigin.Person; }
                else if(Selection.Second!=0)
                {
                    if(Gate(application.SpecialModeSuppressesSceneryReport,"special_mode_scenery"))return null;
                    if(Gate(application.TutorialBlocksAreaReports,"tutorial_scenery"))return null;
                    message=0x53cd; origin=AreaReportOrigin.Scenery;
                }
                else if(Selection.Third!=0) { message=0x5016; origin=AreaReportOrigin.Item; }
                else return null;
                current.WriteAreaFlags38(selected,(byte)(K(current.ReadAreaFlags38(selected),"Area38_set")|0x80));
                ushort person0=Identity(speaker);
                ushort base0=K(current.ReadBaseObjectLink(City(selected)),"message_Base4E");
                int forcePhysical=K(current.ReadForcePhysicalSlot(speakerForce),"message_Force_physical");
                ushort person1=forcePhysical==0 ? (ushort)5014 :
                    K(current.ReadForceRecord(P(speakerForce.PhysicalSlot,"message_Force"))?.RawRuler10,"message_Force10");
                if(format is not null)
                {
                    FormattingInputs=new(message,person0,base0,person1);
                    FormattedText=format(State,FormattingInputs,value=>State=value);
                }
                //The actual format precedes the fresh speaker ID/marker reads.
                //The original intent-only entry keeps its previously signed reads.
                ushort recordSpeaker=format is null ? person0 : Identity(speaker);
                //22D880 returns48400 for an invalid Object; record writers clamp.
                var markerCity=City(selected);
                int marker=ReadReportBaseMarker(current,markerCity,Missing);
                Snapshot=new(P(selected.PhysicalSlot,"selected_pointer"),origin,message,person0,base0,person1,Math.Clamp(marker,0,48399));
                if(append is not null)append(recordSpeaker,marker,FormattedText!);
                return Snapshot;
            }
            finally { if(output is not null)Release(output); }
        }

        /// <summary>2B32E0. Caller retains output; five internal descriptors are
        /// real shared-pool allocations, including the unused fourth Area list.</summary>
        public AreaReportSelection Select(HexListHandle output)
        {
            current.ClearTemporaryHexList(output);
            var empty=new AreaReportSelection(null,0,0,0,0);
            var actor=OwnedCurrentPlayerForce.ReadCurrentReference(State,current) ?? throw Missing("selector_actor");
            var primary=SpeakerSelector().PrimaryDistrict(actor);
            if(!K(current.ReadForceNativeValidity(actor),"selector_Force_validity") ||
                !K(current.ReadDistrictNativeValidity(primary),"selector_District_validity"))return empty;
            var samplerSpeaker=SpeakerSelector().Select(primary);
            if(Valid(samplerSpeaker))_ = Advisor(samplerSpeaker); //actual ignored predicate result
            var allocated=new List<HexListHandle>();
            try
            {
                var a=New();var b=New();var c=New();_ = New();var excluded=New();
                new PersonAppearanceSource(package,State,current,pool).FillExcluded(excluded);
                foreach(int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Person100"))
                {
                    int person=P(entry,"Person100_payload");
                    if(K(current.ReadPersonStatus(person),"PersonE6")!=8 || Contains(excluded,RegistryKind.Person,person))continue;
                    var location=PersonPositionRepair.DecodeSecondLocation(K(current.ReadPersonSecondLocation(person),"PersonE4"));
                    AreaReference? area=location.Kind switch
                    {
                        5=>CurrentGateMapQueries.FindArea(current.ReadCityTableReference(location.Index<52 ? location.Index : 0) ?? throw Missing("City_table"),current),
                        6=>CurrentGateMapQueries.FindArea(current.ReadGateTableReference(location.Index<11 ? location.Index : 0) ?? throw Missing("Gate_table"),current),
                        10=>current.ReadAreaTableReference(location.Index<501 ? location.Index : 0) ?? throw Missing("Area_table"),
                        _=>null
                    };
                    if(area is not null && K(current.ReadAreaNativeValidity(area),"Person_Area_validity") &&
                        ActorOrdinal(actor)==K(current.ReadAreaCurrentForceSlot(area),"Person_Area_Force"))Add(area,a);
                }
                for(int slot=0;slot<101;slot++)
                {
                    var scenery=current.ReadSceneryTableReference(slot) ?? throw Missing("Scenery_table");
                    if(!K(current.ReadSceneryNativeValidity(scenery),"Scenery_validity") ||
                        (K(current.ReadSceneryFlags47(scenery),"Scenery47")&1)!=0)continue;
                    var hex=SceneryHex(scenery);
                    int rawArea=hex.PhysicalSlot.HasValue ? K(current.ReadHexAreaSlot(hex),"Scenery_Hex12") : 0;
                    var area=current.ReadAreaTableReference(rawArea<501 ? rawArea : 0) ?? throw Missing("Scenery_Area");
                    if(!K(current.ReadAreaNativeValidity(area),"Scenery_Area_validity"))continue;
                    if(ActorOrdinal(actor)!=K(current.ReadAreaCurrentForceSlot(area),"Scenery_Area_Force"))continue;
                    byte own=K(current.ReadHexForceSlot(SceneryHex(scenery)),"Scenery_ownHex14");
                    if(own==ActorOrdinal(actor))Add(area,b);
                }
                for(int slot=0;slot<202;slot++)
                {
                    var item=current.ReadRegistryTableReference(RegistryKind.Item,slot) ?? throw Missing("Item_table");
                    if(K(current.ReadRegistryItemRawField50(item),"Item50")!=2)continue;
                    int physical=P(item.PhysicalSlot,"Item_pointer");
                    if(State.Items is not { } items || !items.TryGetValue(physical,out var owned) || owned is null)throw Missing("Item4C_owner");
                    ushort location=K(owned.Lifecycle?.RetainedLocationCode,"Item4C");
                    var area=current.ReadAreaTableReference(location<501 ? location : 0) ?? throw Missing("Item_Area");
                    if(K(current.ReadAreaNativeValidity(area),"Item_Area_validity") &&
                        ActorOrdinal(actor)==K(current.ReadAreaCurrentForceSlot(area),"Item_Area_Force"))Add(area,c);
                }
                ushort id=Identity(samplerSpeaker); //still read for count0/1
                int count=K(current.ReadTemporaryHexListCount(output),"output_count");
                var world=count<2 ? null : package.Context(current).ReadCurrentSamplerWorld(State) ?? throw Missing("sampler450_45C");
                int index=TacticEffectSourceSamplerMath.Sample(count,1,id,0,0,0,world);
                var selected=current.ReadCurrentListValues(output).Skip(index).FirstOrDefault();
                if(selected is null)return empty;
                if(selected is not RegistryObjectReference{Kind:RegistryKind.Area,PhysicalSlot:int areaPhysical})throw Missing("output_Area_payload");
                bool first=Contains(a,RegistryKind.Area,areaPhysical);
                bool second=!first && Contains(b,RegistryKind.Area,areaPhysical);
                bool third=!first && !second && Contains(c,RegistryKind.Area,areaPhysical);
                return new(new(areaPhysical),first?1:0,second?1:0,third?1:0,0);
                HexListHandle New(){var value=Temporary();allocated.Add(value);return value;}
                void Add(AreaReference area,HexListHandle category)
                {int at=P(area.PhysicalSlot,"append_Area");if(!Contains(output,RegistryKind.Area,at))Append(output,new(RegistryKind.Area,at));if(!Contains(category,RegistryKind.Area,at))Append(category,new(RegistryKind.Area,at));}
            }
            finally { for(int i=allocated.Count-1;i>=0;i--)Release(allocated[i]); }
        }

        private BaseReference City(AreaReference area)
        {byte raw=K(current.ReadAreaCitySlot(area),"Area35");return current.ReadCityTableReference(raw<52 ? raw : 0) ?? throw Missing("Area_City");}
        private HexReference SceneryHex(SceneryReference scenery)
        {ushort raw=K(current.ReadSceneryHexIndex(scenery),"Scenery44");return current.ReadHexTableReference(raw<48400 ? raw : 0) ?? throw Missing("Scenery_Hex");}
        private int ActorOrdinal(ForceReference actor)=>K(current.ReadForcePhysicalSlot(actor),"actor_virtual60");
        private bool Valid(TransferPersonReference person)=>person.PhysicalSlot is int at && K(current.ReadPersonNativeValidity(at),"Person_validity");
        private ushort Identity(TransferPersonReference person)=>K(current.ReadPersonIdentity(P(person.PhysicalSlot,"Person_pointer")),"Person10");
        private bool Advisor(TransferPersonReference person)
            => IsCurrentReportAdvisor(current,person,Missing);
        private bool Contains(HexListHandle list,RegistryKind kind,int physical)
        {
            foreach(var value in current.ReadCurrentListValues(list))
            {if(value is not RegistryObjectReference pointer || pointer.Kind!=kind)throw Missing("temporary_payload");if(pointer.PhysicalSlot==physical)return true;}
            return false;
        }
        private HexListHandle Temporary(){Enter();var value=current.CreateTemporaryHexList() ?? throw Missing("temporary_descriptor");Exit();return value;}
        private void Append(HexListHandle list,RegistryObjectReference value)
        {Enter();var node=current.AllocateRegistryNode(new(list.Identity)) ?? throw Missing("temporary_node");if(node.Identity.HasValue)current.WriteRegistryNodePayload(node,value);Exit();}
        private void Release(HexListHandle list){Enter();current.ClearTemporaryHexList(list);current.ReleaseTemporaryHexList(list);Exit();}
        private void Enter(){if(K(current.ReadRegistryLockEnabled(),"lock_enabled"))current.EnterRegistryLock();}
        private void Exit(){if(K(current.ReadRegistryLockEnabled(),"unlock_enabled"))current.ExitRegistryLock();}
        private static bool Gate(Func<bool?>? read,string owner)=>read?.Invoke() ?? throw Missing(owner);
        private static T K<T>(T? value,string owner) where T:struct=>value ?? throw Missing(owner);
        private static int P(int? value,string owner)=>value ?? throw Missing(owner);
        private static InvalidDataException Missing(string owner)=>new("report.area."+owner);
    }
}
