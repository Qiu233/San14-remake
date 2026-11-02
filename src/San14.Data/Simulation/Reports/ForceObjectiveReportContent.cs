using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed record ForceObjectiveReportFormattingInputs(ushort SpeakerPersonOperand0,
    int OtherForceOperand0, ushort ObjectiveBaseOperand0, ushort RulerPersonOperand1);
public sealed record ForceObjectiveReportEntry(ForceReference OtherForce, BaseReference ObjectiveBase,
    ForceObjectiveReportFormattingInputs Formatting, string Text, int MarkerHex, bool LocalRecordAppended);
public sealed record ForceObjectiveReportSnapshot(ImmutableArray<ForceObjectiveReportEntry> Entries,
    bool Completed, ForceObjectiveReportFormattingInputs? ReachedFormatting, string? ReachedFormattedText);

public sealed partial class ArmyCampaignRuleAdapter
{
    public ForceObjectiveReportContentOperation BeginForceObjectiveReportContent(GameState state,
        SourceWorldContext current, WorldIndexContext pool) => new(this, state, current, pool);

    /// <summary>677030: objectives whose real target base is currently owned
    /// by the speaker's Force. No pending-diplomacy surrogate is consulted.</summary>
    public sealed class ForceObjectiveReportContentOperation
    {
        private readonly ArmyCampaignRuleAdapter package;
        private readonly SourceWorldContext current;
        private readonly WorldIndexContext pool;
        //Advice reuses selection with an explicit nonmutating text consumer.
        private readonly Func<ForceObjectiveReportFormattingInputs,string>? textOnly;
        private readonly List<ForceObjectiveReportEntry> entries = [];
        private bool entered, completed;
        private ForceObjectiveReportFormattingInputs? formatting;
        private string? formattedText;
        public GameState State { get; private set; }
        public ForceObjectiveReportSnapshot Snapshot => new(entries.ToImmutableArray(), completed, formatting, formattedText);

        internal ForceObjectiveReportContentOperation(ArmyCampaignRuleAdapter package, GameState state,
            SourceWorldContext current, WorldIndexContext pool, Func<ForceObjectiveReportFormattingInputs,string>? textOnly = null)
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile()) throw Missing("same_current_pool");
            this.package = package; State = state; this.current = current; this.pool = pool; this.textOnly=textOnly;
        }

        public ForceObjectiveReportSnapshot Produce(TransferPersonReference speaker,
            ActionReportLocalRecords records, int limit = int.MaxValue)
        {
            ArgumentNullException.ThrowIfNull(records);
            if (entered) throw Missing("operation_already_entered");
            entered = true;
            if (speaker.PhysicalSlot is not int person || !K(current.ReadPersonNativeValidity(person), "Person_validity") ||
                !IsCurrentReportAdvisor(current, speaker, Missing))
            { completed = true; return Snapshot; }
            var actor = current.ReadPersonForce(person) ?? throw Missing("speaker_Force");
            // The source calls this selector even though its return is ignored.
            _ = new PersonDeathHistorySelection(State, current, pool,
                new TacticRemakeCurrentAbilities(current, package._scenario, package.Seed.Language,
                    SimulationRuleIdentity.ForLocale(package.Seed.Language)), package._governorInputs,
                (force, effect) => package._baseCapacityInputs.ReadPanelModifier(State, force, effect)).PrimaryDistrict(actor);
            var descriptor = current.ReadRegistryDescriptor(RegistryKind.Force) ?? throw Missing("Force_C0_descriptor");
            var node = current.ReadRegistryHead(descriptor) ?? throw Missing("Force_C0_head");
            while (node.Identity.HasValue && entries.Count < limit)
            {
                var payload = current.ReadRegistryNodePayload(node);
                if (payload is not { Kind: RegistryKind.Force }) throw Missing("Force_C0_payload");
                var other = new ForceReference(payload.PhysicalSlot);
                var target = BaseLookup.Find(ReadTarget(other), current);
                if (K(current.ReadBaseNativeValidity(target), "target_Base_validity") &&
                    K(current.ReadBaseForceSlot(target), "target_Base_virtualB0") == K(current.ReadForcePhysicalSlot(actor), "actor_virtual60"))
                {
                    formatting = null; formattedText = null;
                    ushort speakerId = Identity(speaker);
                    int forceId = K(current.ReadForcePhysicalSlot(other), "message_other_virtual60");
                    ushort baseId = K(current.ReadBaseObjectLink(target), "message_Base4E");
                    int actorId = K(current.ReadForcePhysicalSlot(actor), "message_actor_virtual60");
                    ushort ruler = actorId == 0 ? (ushort)5014 :
                        K(current.ReadForceRecord(P(actor.PhysicalSlot, "message_actor"))?.RawRuler10, "message_ruler10");
                    formatting = new(speakerId, forceId, baseId, ruler);
                    string text = textOnly is {} render ? render(formatting) : package.FormatActionForceObjectiveReport(State, current, formatting, value => State = value).Text;
                    formattedText = text;
                    ushort freshSpeaker = Identity(speaker);
                    //22EA10 uses the other Force ruler's current home, not the target base.
                    ushort otherRuler = K(current.ReadForceRecord(P(other.PhysicalSlot, "marker_Force"))?.RawRuler10, "marker_ruler10");
                    var rulerPerson = (otherRuler <= 6000 ? current.ReadPersonLookupReference(otherRuler) : current.ReadTransferPersonTableReference(0))
                        ?? throw Missing("marker_ruler_lookup");
                    ushort home = K(current.ReadPersonFirstLocation(P(rulerPerson.PhysicalSlot, "marker_ruler")), "marker_ruler_E2");
                    int marker = ReadReportBaseMarker(current, BaseLookup.Find(home, current), Missing);
                    bool appended = records.AppendForceObjectiveWarning(freshSpeaker, marker, text);
                    entries.Add(new(other, target, formatting, text, Math.Clamp(marker, 0, 48399), appended));
                }
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("Force_C0_live_next");
            }
            completed = true;
            return Snapshot;
        }

        private int ReadTarget(ForceReference force)
        {
            int physical = P(force.PhysicalSlot, "objective_Force");
            // Actual reset bytes override the domain owner after a real reset.
            if (State.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(physical, out var reset) == true)
                return K(reset[0x160], "Force160");
            return K(State.Forces.GetValueOrDefault(physical)?.CombatObjective?.TargetLocationId, "Force160_target_location");
        }
        private ushort Identity(TransferPersonReference person) => K(current.ReadPersonIdentity(P(person.PhysicalSlot, "Person_pointer")), "Person10");
        private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static int P(int? value, string field) => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("report.force-objective." + field);
    }
}
