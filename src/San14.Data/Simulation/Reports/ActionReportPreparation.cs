using System.Collections.Immutable;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Known application state is supplied by its owner; null stays unknown.</summary>
public sealed record ActionReportApplicationInputs(Func<bool?>? TutorialBlocksAreaReports = null,
    Func<bool?>? SpecialModeSuppressesSceneryReport = null,
    Func<bool?>? SpecialModeSuppressesAssociationReport = null,
    Func<bool?>? TutorialBlocksAssociationReports = null);

/// <summary>One stable selected history pointer, with its physical ring identity.
/// Bucket membership is independent of CampaignHistoryRecord.Category.</summary>
public sealed record ActionReportHistoryEntry(int PhysicalSlot, CampaignHistoryRecord Record);
public sealed record ActionReportHistoryBucket(byte Category, ImmutableArray<ActionReportHistoryEntry> Records);
public sealed record ActionReportHistorySnapshot(ImmutableArray<ActionReportHistoryBucket> Buckets,
    int ReachedPhysicalSlot, bool HistoryScanAndSortCompleted);
/// <summary>Complete known input after all relevant producers, prepared for one
/// actual list. This local fallback never becomes a saved history record.</summary>
public sealed record ActionReportPreparedList(byte Category, ImmutableArray<CampaignHistoryRecord> Records,
    bool UsesEmptyFallback);
public sealed record ActionReportStrategistPerson(int PhysicalSlot, ushort PersonId);

public sealed partial class ArmyCampaignRuleAdapter
{
    public ActionReportPreparationOperation BeginActionReportPreparation(GameState state,
        ActionReportApplicationInputs? application = null)
    {
        var current = CaptureReportContentCurrent(state);
        return BeginActionReportPreparation(state,current,
            current.ReadOwnedCurrentListPool() ?? throw new InvalidDataException("action-report.current_pool"),application);
    }
    public ActionReportPreparationOperation BeginActionReportPreparation(GameState state,
        SourceWorldContext current, WorldIndexContext pool, ActionReportApplicationInputs? application = null)
        => new(this,state,current,pool,application ?? new());

    private SourceWorldContext CaptureReportContentCurrent(GameState state)
    {
        var current = CaptureSceneryMapCurrent(state);
        PersonArmyCaptureInputs.BindCurrent(state, current);
        PersonReturnCommands.BindCurrent(state, current);
        _personAssignmentPrecallInputs.BindCurrent(state, current);
        return current;
    }

    /// <summary>Explicit application preparation, never a View redraw query.
    /// Its history snapshot stays partial with respect to synthetic producers.
    /// The same current/pool survives every reached call and exception.</summary>
    public sealed class ActionReportPreparationOperation
    {
        private static readonly byte[] order=[1,2,3,4,5,6,0];
        private readonly ArmyCampaignRuleAdapter package;
        private readonly ActionReportApplicationInputs application;
        private readonly List<ActionReportHistoryEntry>[] buckets=Enumerable.Range(0,7).Select(_=>new List<ActionReportHistoryEntry>()).ToArray();
        private bool entered, sorted, areaEntered, areaLocalCompleted, armyTargetEntered, armyTargetCompleted, baseNearbyEntered, baseNearbyCompleted, allianceExpiryEntered, allianceExpiryCompleted, objectiveEntered, objectiveCompleted, requestExpiryEntered, requestExpiryCompleted, supplyEntered, supplyCompleted, associationEntered, associationCompleted;
        private TransferPersonReference? retainedSpeaker;
        private ActionReportLocalRecords? syntheticRecords;
        private int reached=-1;
        public GameState State { get; private set; }
        public SourceWorldContext Current { get; }
        public WorldIndexContext Pool { get; }
        public AreaReportContentSnapshot? AreaIntent { get; private set; }
        public AreaReportFormattingInputs? AreaFormatting { get; private set; }
        public string? AreaFormattedText { get; private set; }
        public ArmyTargetReportSnapshot? ArmyTargetReports { get; private set; }
        public BaseNearbyEnemyReportSnapshot? BaseNearbyEnemyReports { get; private set; }
        public AllianceExpiryReportSnapshot? AllianceExpiryReports { get; private set; }
        public ForceObjectiveReportSnapshot? ForceObjectiveReports { get; private set; }
        public ForceAttackRequestExpiryReportSnapshot? ForceAttackRequestExpiryReports { get; private set; }
        public ArmySupplyReportSnapshot? ArmySupplyReports { get; private set; }
        public AssociationReportTailSnapshot? AssociationTail { get; private set; }
        public bool SyntheticPreparationCompleted => associationCompleted;
        public ActionReportHistorySnapshot HistorySnapshot => new(order.Select((category,index)=>
            new ActionReportHistoryBucket(category,buckets[index].ToImmutableArray())).ToImmutableArray(),reached,sorted);

        internal ActionReportPreparationOperation(ArmyCampaignRuleAdapter package,GameState state,
            SourceWorldContext current,WorldIndexContext pool,ActionReportApplicationInputs application)
        {
            if(!ReferenceEquals(pool.ArmyReturnOwner,current)||!pool.OwnsArmyReturnProfile())throw Missing("same_current_pool");
            this.package=package;State=state;Current=current;Pool=pool;this.application=application;
        }
        private PersonDeathHistorySelection SpeakerSelection()=>new(State,Current,Pool,
            new TacticRemakeCurrentAbilities(Current,package._scenario,package.Seed.Language,
                SimulationRuleIdentity.ForLocale(package.Seed.Language)),package._governorInputs,
            (force,effect)=>package._baseCapacityInputs.ReadPanelModifier(State,force,effect));
        private DistrictReference Primary()
        {
            var actor=OwnedCurrentPlayerForce.ReadCurrentReference(State,Current) ?? throw Missing("World3A_Force");
            return SpeakerSelection().PrimaryDistrict(actor);
        }
        private TransferPersonReference Speaker()=>SpeakerSelection().Select(Primary());

        /// <summary>678F90's persisted ring phase only. Physical0..2000 consumption
        /// and each flags store precede filtering. Its seven selected histories
        /// are then sorted by CampaignDay, physical slot; cursor is not rotated.
        /// Synthetic records have not yet been appended and no fallback is made.</summary>
        public ActionReportHistorySnapshot ConsumeHistory()
        {
            if(entered)throw Missing("history_operation_already_entered");entered=true;
            //Both parent lookups precede any history store, including a known
            //empty ring. The selected primary pointer is retained for comparison.
            _=OwnedCurrentPlayerForce.ReadCurrentReference(State,Current) ?? throw Missing("World3A_Force");
            var primary=Primary();
            for(int slot=0;slot<CampaignHistoryState.Capacity;slot++)
            {
                reached=slot;
                var owner=State.CampaignHistory ?? throw Missing("history_owner");
                if(owner.Records.IsDefault||owner.Records.Length!=CampaignHistoryState.Capacity)throw Missing("history_table2001");
                var record=owner.Records[slot];byte category=record.Category,district=record.DistrictId,flags=record.Flags;
                if((flags&4)!=0||(flags&2)==0)continue;
                record=record with{Flags=(byte)((flags&0xfd)|4)};
                State=State with{CampaignHistory=owner with{Records=owner.Records.SetItem(slot,record)}};
                bool belongs=false;
                //The actual233880 call is made before the global-bit OR. Do
                //not skip a reached player/District read because bit0 is set.
                if(district is >=1 and <=51)
                {
                    var pointer=Current.ReadDistrictTableReference(district) ?? throw Missing("history_District_table");
                    belongs=PlayerOwns(pointer);
                }
                if(((record.Flags&1)!=0||belongs)&&Nonempty(record.Text))
                    Bucket(0).Add(new(slot,record));
                int primaryPhysical=K(Current.ReadDistrictPhysicalSlot(primary),"primary_District_physical");
                if((district==primaryPhysical||(record.Flags&1)!=0)&&Nonempty(record.SupplementalText)&&category!=0)
                    Bucket(category).Add(new(slot,record));
            }
            //TC673970 compares unsigned day10, then22DF80 physical ordinal.
            //This is a post-consumption sort of history lists only.
            foreach(var bucket in buckets)
            {
                var ordered=bucket.OrderBy(entry=>entry.Record.CampaignDay).ThenBy(entry=>entry.PhysicalSlot).ToArray();
                bucket.Clear();bucket.AddRange(ordered);
            }
            sorted=true;return HistorySnapshot;
        }

        private bool PlayerOwns(DistrictReference district)
        {
            var player=OwnedCurrentPlayerForce.ReadCurrentReference(State,Current) ?? throw Missing("233880_World3A");
            if(!K(Current.ReadForceNativeValidity(player),"233880_Force_validity"))return false;
            byte owner=K(Current.ReadDistrictForceSlot(district),"233880_District10");
            return owner==K(Current.ReadForcePhysicalSlot(player),"233880_Force_physical");
        }
        private List<ActionReportHistoryEntry> Bucket(byte category)
        {
            int index=Array.IndexOf(order,category);if(index<0)throw Missing("history_category_mapping");return buckets[index];
        }

        /// <summary>The first original synthetic operation, including its
        /// saved Area flags and every reached random/text/local prefix.</summary>
        public AreaReportContentSnapshot? PrepareAreaLocalRecord(ActionReportLocalRecords records)
        {
            ArgumentNullException.ThrowIfNull(records);
            if (!sorted) throw Missing("history_scan_and_sort_not_completed");
            if (areaEntered) throw Missing("area_operation_already_entered");
            areaEntered = true; records.BeginPreparation(); syntheticRecords = records;
            _ = OwnedCurrentPlayerForce.ReadCurrentReference(State, Current) ?? throw Missing("synthetic_actor");
            var speaker = retainedSpeaker = Speaker();
            var operation = package.BeginAreaReportContent(State, Current, Pool,
                new(application.TutorialBlocksAreaReports, application.SpecialModeSuppressesSceneryReport));
            try
            {
                AreaIntent = operation.ProduceWithLocalRecord(speaker,
                    (state, input, publish) => package.FormatActionAreaReport(state, Current, input, publish).Text,
                    (identity, marker, text) => records.AppendAreaWarning(identity, marker, text));
                areaLocalCompleted = true;
                return AreaIntent;
            }
            finally
            {
                State = operation.State; AreaIntent = operation.Snapshot;
                AreaFormatting = operation.FormattingInputs; AreaFormattedText = operation.FormattedText;
            }
        }

        /// <summary>Continues the actual second synthetic call only after the
        /// first returned normally, retaining its selected speaker and slots.
        /// No later producer, final collection or ActionReady is implied.</summary>
        public ArmyTargetReportSnapshot PrepareArmyTargetLocalRecords(ActionReportLocalRecords records)
        {
            if(!areaLocalCompleted||retainedSpeaker is null)throw Missing("area_local_stage_not_completed");
            if(!ReferenceEquals(records,syntheticRecords))throw Missing("same_local_records");
            if(armyTargetEntered)throw Missing("army_target_operation_already_entered");armyTargetEntered=true;
            var operation=package.BeginArmyTargetReportContent(State,Current,Pool);
            try
            {
                var result=operation.Produce(retainedSpeaker,records);
                armyTargetCompleted=true;return result;
            }
            finally{State=operation.State;ArmyTargetReports=operation.Snapshot;}
        }

        /// <summary>The third original call follows the normally completed
        /// second call, retaining its speaker, current/pool and local slots.</summary>
        public BaseNearbyEnemyReportSnapshot PrepareBaseNearbyEnemyLocalRecords(ActionReportLocalRecords records)
        {
            if(!armyTargetCompleted||retainedSpeaker is null)throw Missing("army_target_stage_not_completed");
            if(!ReferenceEquals(records,syntheticRecords))throw Missing("same_local_records");
            if(baseNearbyEntered)throw Missing("base_nearby_operation_already_entered");baseNearbyEntered=true;
            var operation=package.BeginBaseNearbyEnemyReportContent(State,Current,Pool);
            try
            {
                var result=operation.Produce(retainedSpeaker,records);
                baseNearbyCompleted=true;return result;
            }
            finally{State=operation.State;BaseNearbyEnemyReports=operation.Snapshot;}
        }

        /// <summary>The fourth original call follows only a normally returned
        /// third call, using its retained speaker and same local/current/pool.</summary>
        public AllianceExpiryReportSnapshot PrepareAllianceExpiryLocalRecords(ActionReportLocalRecords records)
        {
            if(!baseNearbyCompleted||retainedSpeaker is null)throw Missing("base_nearby_stage_not_completed");
            if(!ReferenceEquals(records,syntheticRecords))throw Missing("same_local_records");
            if(allianceExpiryEntered)throw Missing("alliance_expiry_operation_already_entered");allianceExpiryEntered=true;
            var operation=package.BeginForceAllianceExpiryReportContent(State,Current,Pool);
            try
            {
                var result = operation.Produce(retainedSpeaker, records);
                allianceExpiryCompleted = true; return result;
            }
            finally { State = operation.State; AllianceExpiryReports = operation.Snapshot; }
        }

        public ForceObjectiveReportSnapshot PrepareForceObjectiveLocalRecords(ActionReportLocalRecords records)
        {
            RequireStage(allianceExpiryCompleted, records, "alliance_expiry");
            if (objectiveEntered) throw Missing("force_objective_operation_already_entered"); objectiveEntered = true;
            var operation = package.BeginForceObjectiveReportContent(State, Current, Pool);
            try
            {
                var result = operation.Produce(retainedSpeaker!, records);
                objectiveCompleted = true; return result;
            }
            finally { State = operation.State; ForceObjectiveReports = operation.Snapshot; }
        }
        public ForceAttackRequestExpiryReportSnapshot PrepareForceAttackRequestExpiryLocalRecord(ActionReportLocalRecords records)
        {
            RequireStage(objectiveCompleted, records, "force_objective");
            if (requestExpiryEntered) throw Missing("request_expiry_operation_already_entered"); requestExpiryEntered = true;
            var operation = package.BeginForceAttackRequestExpiryReportContent(State, Current, Pool);
            try
            {
                var result = operation.Produce(retainedSpeaker!, records);
                requestExpiryCompleted = true; return result;
            }
            finally { State = operation.State; ForceAttackRequestExpiryReports = operation.Snapshot; }
        }
        public ArmySupplyReportSnapshot PrepareArmySupplyAggregate(ActionReportLocalRecords records)
        {
            RequireStage(requestExpiryCompleted, records, "request_expiry");
            if (supplyEntered) throw Missing("army_supply_operation_already_entered"); supplyEntered = true;
            var operation = package.BeginArmySupplyReportContent(State, Current, Pool);
            try
            {
                var result = operation.Produce(retainedSpeaker!, records);
                supplyCompleted = true; return result;
            }
            finally { State = operation.State; ArmySupplyReports = operation.Snapshot; }
        }
        public AssociationReportTailSnapshot PrepareAssociationTail(ActionReportLocalRecords records)
        {
            RequireStage(supplyCompleted, records, "army_supply");
            if (associationEntered) throw Missing("association_operation_already_entered"); associationEntered = true;
            var operation = package.BeginAssociationReportTailContent(State, Current, Pool, application);
            try
            {
                var result = operation.Produce(retainedSpeaker!);
                associationCompleted = true; return result;
            }
            finally { State = operation.State; AssociationTail = operation.Snapshot; }
        }
        public void PrepareSyntheticRecords(ActionReportLocalRecords records)
        {
            PrepareAreaLocalRecord(records);
            PrepareArmyTargetLocalRecords(records);
            PrepareBaseNearbyEnemyLocalRecords(records);
            PrepareAllianceExpiryLocalRecords(records);
            PrepareForceObjectiveLocalRecords(records);
            PrepareForceAttackRequestExpiryLocalRecord(records);
            PrepareArmySupplyAggregate(records);
            PrepareAssociationTail(records);
        }
        private void RequireStage(bool completed, ActionReportLocalRecords records, string stage)
        {
            if (!completed || retainedSpeaker is null) throw Missing(stage + "_stage_not_completed");
            if (!ReferenceEquals(records, syntheticRecords)) throw Missing("same_local_records");
        }

        /// <summary>The680C30 speaker/portrait input, freshly selected from
        /// actual World3A/primary District. This does not fabricate its many
        /// category-specific strategist messages or play/ACK their effects.</summary>
        public ActionReportStrategistPerson ReadStrategistPerson()
        {
            var person=Speaker();int physical=K(person.PhysicalSlot,"strategist_Person_pointer");
            _=K(Current.ReadPersonNativeValidity(physical),"strategist_Person_validity");
            return new(physical,K(Current.ReadPersonIdentity(physical),"strategist_Person10"));
        }

        /// <summary>67CAC0/67CE80 list phase for an explicitly complete known
        /// collection. Never call this on an unresolved synthetic/history bucket;
        /// partial HistorySnapshot is a different type and is not auto-promoted.</summary>
        public ActionReportPreparedList PrepareKnownList(byte category,IReadOnlyList<CampaignHistoryRecord>? completeRecords)
        {
            if(Array.IndexOf(order,category)<0)throw Missing("list_category");
            if(completeRecords is null)throw Missing("complete_list_records");
            var records=completeRecords.Select(record=>record ?? throw Missing("list_record_pointer")).Where(record=>Nonempty(category==0 ? record.Text : record.SupplementalText)).ToImmutableArray();
            if(records.Length!=0)return new(category,records,false);
            var messages=new NarrationResourceMessages(package._repository.LoadMessages(package.Seed.Language,MessageKind.Game).Messages);
            string text=messages.ResolveActionReportEmpty();
            //The constructor clears both windows first. Copy uses the same
            //30DED0 bounded/terminator contract as the saved history writer.
            var primary=new ushort[55];var supplemental=new ushort[85];
            ReportPool.CopyTextWindow(text,55,(index,value)=>primary[index]=value);
            ReportPool.CopyTextWindow(text,85,(index,value)=>supplemental[index]=value);
            var person=Speaker();int physical=K(person.PhysicalSlot,"fallback_Person_pointer");
            ushort identity=K(Current.ReadPersonIdentity(physical),"fallback_Person10");
            var local=CampaignHistoryRecord.Empty with{Flags=3,PersonId=(ushort)Math.Clamp((int)identity,1,6000),
                Text=primary.ToImmutableArray(),SupplementalText=supplemental.ToImmutableArray()};
            return new(category,[local],true);
        }
        private static bool Nonempty(ImmutableArray<ushort> window)
        {if(window.IsDefaultOrEmpty)throw Missing("fixed_text_window");return window[0]!=0;}
        private static T K<T>(T? value,string field)where T:struct=>value ?? throw Missing(field);
        private static InvalidDataException Missing(string field)=>new("action-report.prepare."+field);
    }
}
