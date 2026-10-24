using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed record ArmySupplyReportFormattingInputs(int MessageId, ushort SpeakerPersonOperand0,
    ArmyUnitReference? ArmyOperand0, ushort? HomeBaseOperand0, int ArmyCountInteger0, ushort RulerPersonOperand1);
public sealed record ArmySupplyReportSnapshot(ImmutableArray<int> ArmyPhysicalSlots, bool Completed,
    ArmySupplyReportFormattingInputs? ReachedFormatting, string? ReachedFormattedText, bool AggregateAppended);

public sealed partial class ArmyCampaignRuleAdapter
{
    public ArmySupplyReportContentOperation BeginArmySupplyReportContent(GameState state,
        SourceWorldContext current, WorldIndexContext pool) => new(this, state, current, pool);

    /// <summary>676310: actual registered armies without logistics flag2 in
    /// the player's primary district. The category5 record has a separate slot.</summary>
    public sealed class ArmySupplyReportContentOperation
    {
        private readonly ArmyCampaignRuleAdapter package;
        private readonly SourceWorldContext current;
        private readonly List<int> selected = [];
        private bool entered, completed, appended;
        private ArmySupplyReportFormattingInputs? formatting;
        private string? formattedText;
        public GameState State { get; private set; }
        public ArmySupplyReportSnapshot Snapshot => new(selected.ToImmutableArray(), completed, formatting, formattedText, appended);

        internal ArmySupplyReportContentOperation(ArmyCampaignRuleAdapter package, GameState state,
            SourceWorldContext current, WorldIndexContext pool)
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile()) throw Missing("same_current_pool");
            this.package = package; State = state; this.current = current;
        }

        public ArmySupplyReportSnapshot Produce(TransferPersonReference speaker, ActionReportLocalRecords records)
        {
            ArgumentNullException.ThrowIfNull(records);
            if (entered) throw Missing("operation_already_entered");
            entered = true;
            if (speaker.PhysicalSlot is not int person || !K(current.ReadPersonNativeValidity(person), "Person_validity"))
            { completed = true; return Snapshot; }
            var actor = OwnedCurrentPlayerForce.ReadCurrentReference(State, current) ?? throw Missing("World3A_Force");
            var pool = current.ReadOwnedCurrentListPool() ?? throw Missing("current_pool");
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

            HexListHandle? temporary = null;
            try
            {
                Enter(); temporary = current.CreateTemporaryHexList() ?? throw Missing("temporary_descriptor"); Exit();
                var descriptor = current.ReadRegistryDescriptor(RegistryKind.Army) ?? throw Missing("Army60_descriptor");
                var node = current.ReadRegistryHead(descriptor) ?? throw Missing("Army60_head");
                while (node.Identity.HasValue)
                {
                    var payload = current.ReadRegistryNodePayload(node);
                    if (payload is not { Kind: RegistryKind.Army, PhysicalSlot: int physical }) throw Missing("Army60_payload");
                    var army = new ArmyUnitReference(physical);
                    int forceId = K(current.ReadForcePhysicalSlot(force), "force_virtual60");
                    if (current.ReadArmyUnitRawForceSlot(army) == forceId &&
                        ReadFacilityHistoryDistrict(current, army) == K(current.ReadDistrictPhysicalSlot(primary), "primary_virtual60") &&
                        (Flags26(physical) & 2) == 0)
                    {
                        Enter();
                        var element = current.AllocateRegistryNode(new(temporary.Identity)) ?? throw Missing("temporary_node");
                        if (element.Identity.HasValue)
                        {
                            current.WriteRegistryNodePayload(element, new(RegistryKind.Army, physical));
                            selected.Add(physical);
                        }
                        Exit();
                    }
                    node = current.ReadRegistryNodeNext(node) ?? throw Missing("Army60_live_next");
                }
                int count = K(current.ReadTemporaryHexListCount(temporary), "temporary_count");
                if (count != 0)
                {
                    ushort speakerId = Identity(speaker);
                    ArmyUnitReference? first = null;
                    ushort? home = null;
                    int message;
                    if (count == 1)
                    {
                        var head = current.ReadRegistryHead(new(temporary.Identity)) ?? throw Missing("temporary_head");
                        var value = current.ReadRegistryNodePayload(head);
                        if (value is not { Kind: RegistryKind.Army }) throw Missing("temporary_first_Army");
                        first = new(value.PhysicalSlot);
                        //22DB70's pointer-derived Army identity is carried by first.
                        ushort commander = K(current.ReadArmyUnitCommanderId(first), "first_Army12");
                        var commanderPerson = (commander <= 6000 ? current.ReadPersonLookupReference(commander) : current.ReadTransferPersonTableReference(0))
                            ?? throw Missing("first_Commander_lookup");
                        home = commanderPerson.PhysicalSlot is int at && K(current.ReadPersonNativeValidity(at), "first_Commander_validity")
                            ? K(current.ReadPersonFirstLocation(at), "first_Commander_E2") : (ushort)0;
                        message = 0x5032;
                    }
                    else message = 0x5039;
                    int ordinal = K(current.ReadForcePhysicalSlot(force), "message_Force_virtual60");
                    ushort ruler = ordinal == 0 ? (ushort)5014 :
                        K(current.ReadForceRecord(P(force.PhysicalSlot, "message_Force"))?.RawRuler10, "message_ruler10");
                    formatting = new(message, speakerId, first, home, count, ruler);
                    string text = package.FormatActionArmySupplyReport(State, current, formatting, value => State = value).Text;
                    formattedText = text;
                    //Formatting and fresh ID occur even if the dedicated slot was already occupied.
                    appended = records.AppendArmySupplyAggregate(Identity(speaker), text);
                }
            }
            finally
            {
                if (temporary is not null)
                { Enter(); current.ClearTemporaryHexList(temporary); current.ReleaseTemporaryHexList(temporary); Exit(); }
            }
            completed = true;
            return Snapshot;
        }

        private byte Flags26(int physical)
        {
            var slots = State.ReadArmyLifecycle()?.ReceiverSlots() ?? throw Missing("Army_runtime_owner");
            if ((uint)physical >= (uint)slots.Length) throw Missing("Army_runtime_physical");
            var slot = slots[physical];
            var image = slot.ActiveUnitId is int id ? State.Units.GetValueOrDefault(id) : slot.PendingImage;
            return K(image?.NativeTacticRuntime?.Flags26, "Army26");
        }
        private ushort Identity(TransferPersonReference person) => K(current.ReadPersonIdentity(P(person.PhysicalSlot, "Person_pointer")), "Person10");
        private void Enter() { if (K(current.ReadRegistryLockEnabled(), "lock_enabled")) current.EnterRegistryLock(); }
        private void Exit() { if (K(current.ReadRegistryLockEnabled(), "unlock_enabled")) current.ExitRegistryLock(); }
        private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static int P(int? value, string field) => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("report.army-supply." + field);
    }
}
