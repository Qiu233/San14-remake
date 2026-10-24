using San14.Core;

namespace San14.Data;

public sealed record ForceAttackRequestExpiryFormattingInputs(ushort SpeakerPersonOperand0,
    int RequestingForceOperand0, int RemainingMonthsInteger0, ushort RulerPersonOperand1);
public sealed record ForceAttackRequestExpiryReportSnapshot(bool Completed,
    ForceAttackRequestExpiryFormattingInputs? ReachedFormatting, string? ReachedFormattedText,
    int? MarkerHex, bool LocalRecordAppended);

public sealed partial class ArmyCampaignRuleAdapter
{
    public ForceAttackRequestExpiryReportContentOperation BeginForceAttackRequestExpiryReportContent(GameState state,
        SourceWorldContext current, WorldIndexContext pool) => new(this, state, current, pool);

    /// <summary>676C80's actual one-month, zero-progress attack request warning.</summary>
    public sealed class ForceAttackRequestExpiryReportContentOperation
    {
        private readonly ArmyCampaignRuleAdapter package;
        private readonly SourceWorldContext current;
        private readonly WorldIndexContext pool;
        private bool entered, completed, appended;
        private ForceAttackRequestExpiryFormattingInputs? formatting;
        private string? text;
        private int? marker;
        public GameState State { get; private set; }
        public ForceAttackRequestExpiryReportSnapshot Snapshot => new(completed, formatting, text, marker, appended);
        internal ForceAttackRequestExpiryReportContentOperation(ArmyCampaignRuleAdapter package,
            GameState state, SourceWorldContext current, WorldIndexContext pool)
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile()) throw Missing("same_current_pool");
            this.package = package; State = state; this.current = current; this.pool = pool;
        }
        public ForceAttackRequestExpiryReportSnapshot Produce(TransferPersonReference speaker, ActionReportLocalRecords records)
        {
            ArgumentNullException.ThrowIfNull(records);
            if (entered) throw Missing("operation_already_entered"); entered = true;
            if (speaker.PhysicalSlot is not int person || !K(current.ReadPersonNativeValidity(person), "Person_validity"))
            { completed = true; return Snapshot; }
            var actor = OwnedCurrentPlayerForce.ReadCurrentReference(State, current) ?? throw Missing("World3A_Force");
            var primary = new PersonDeathHistorySelection(State, current, pool,
                new TacticRemakeCurrentAbilities(current, package._scenario, package.Seed.Language,
                    SimulationRuleIdentity.ForLocale(package.Seed.Language)), package._governorInputs,
                (force, effect) => package._baseCapacityInputs.ReadPanelModifier(State, force, effect)).PrimaryDistrict(actor);
            if (!K(current.ReadDistrictNativeValidity(primary), "primary_District_validity"))
            { completed = true; return Snapshot; }
            byte rawForce = K(current.ReadDistrictForceSlot(primary), "primary_District10");
            var force = current.ReadForceTableReference(rawForce < 52 ? rawForce : 0) ?? throw Missing("primary_Force");
            if (!K(current.ReadForceNativeValidity(force), "primary_Force_validity"))
            { completed = true; return Snapshot; }
            int physical = P(force.PhysicalSlot, "Force_pointer");
            var owner = State.Forces.GetValueOrDefault(physical)?.CombatObjective;
            int requester = ReadByte(physical, 0x15f, owner?.RequestingForceId, "requester_Force15F");
            var other = current.ReadForceTableReference(requester < 52 ? requester : 0) ?? throw Missing("requester_Force_table");
            if (!K(current.ReadForceNativeValidity(other), "requester_Force_validity"))
            { completed = true; return Snapshot; }
            int months = ReadByte(physical, 0x161, owner?.RemainingMonths, "request_RemainingMonths161");
            if (months != 0 && months <= 1 && ReadProgress(physical, owner?.Progress) == 0)
            {
                ushort speakerId = Identity(speaker);
                int otherId = K(current.ReadForcePhysicalSlot(other), "message_other_virtual60");
                int freshMonths = ReadByte(physical, 0x161, owner?.RemainingMonths, "message_RemainingMonths161");
                int ordinal = K(current.ReadForcePhysicalSlot(force), "message_Force_virtual60");
                ushort ruler = ordinal == 0 ? (ushort)5014 : K(current.ReadForceRecord(physical)?.RawRuler10, "message_ruler10");
                formatting = new(speakerId, otherId, freshMonths, ruler);
                text = package.FormatActionAttackRequestExpiryReport(State, current, formatting, value => State = value).Text;
                ushort freshSpeaker = Identity(speaker);
                int target = ReadByte(physical, 0x160, owner?.TargetLocationId, "marker_TargetLocation160");
                marker = ReadReportBaseMarker(current, BaseLookup.Find(target, current), Missing);
                appended = records.AppendAttackRequestExpiryWarning(freshSpeaker, marker.Value, text);
            }
            completed = true; return Snapshot;
        }
        private int ReadByte(int physical, int offset, int? value, string field)
        {
            if (State.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(physical, out var reset) == true)
                return K(reset[offset], field);
            int known = K(value, field);
            return known is >= 0 and <= 255 ? known : throw Missing(field + "_range");
        }
        private int ReadProgress(int physical, int? value)
        {
            if (State.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(physical, out var reset) == true)
                return K(reset[0x162], "Force162_low") | K(reset[0x163], "Force162_high") << 8;
            return K(value, "Force162_Progress");
        }
        private ushort Identity(TransferPersonReference person) => K(current.ReadPersonIdentity(P(person.PhysicalSlot, "Person_pointer")), "Person10");
        private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static int P(int? value, string field) => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("report.attack-request-expiry." + field);
    }
}
