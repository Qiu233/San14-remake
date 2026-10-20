using San14.Core;

namespace San14.Data;

// These operation-local records are requests, never an admission/playback receipt.
internal sealed record CapturedPersonLine(int EntityKind, int EntityId, int OfficerId,
    int? Action, int HexId, int Argument1, int Argument2, int Bypass,
    int? AdmissionForce, byte? AdmissionColor)
{
    public GarrisonContactChoice? GarrisonChoice { get; init; }
}
internal sealed record CapturedBattleReport(int DistrictId, int HexId, int CampaignDay,
    int OfficerId, string ArmyName, string TargetName, int TargetKind, int TargetId,
    int? ForceSlot, byte? ForceColor, byte? KingdomNameIndex, int? RulerId);
internal sealed record CapturedCombatEffect(CapturedPersonLine? PersonLine, CapturedBattleReport? Report)
{
    internal CampaignHistoryNotification? HistoryNotification { get; init; }
}

/// <summary>Captures source-time identities and positions without touching the
/// App CRT, queue, layout allocation or presentation acknowledgements. Actual
/// report calls write the candidate's persistent history at source time.</summary>
internal sealed class NormalCombatPresentationCapture(ArmyCampaignRuleAdapter package, GameState state, SourceWorldContext current,
    ArmyMapGroupingCurrentQueries queries, TacticRemakeCurrentContext tactics)
{
    private readonly List<CapturedCombatEffect> effects = [];
    internal GameState State => state;
    internal CapturedCombatEffect[] Effects => effects.ToArray();
    internal void ActorMessages(NormalCombatActorTargets row)
    {
        var actor = row.Actor;
        var target = row.Targets[0];
        foreach (var candidate in row.Targets.Skip(1))
            if (NormalCombatMixedProducer.Priority(candidate.Kind) < NormalCombatMixedProducer.Priority(target.Kind)) target = candidate;
        if (actor.Kind is 5 or 6)
        {
            if (target.Kind != 27) return;
            var reference = BaseLookup.Find(actor.Kind == 5 ? actor.PhysicalSlot : actor.PhysicalSlot + 51, current);
            int person = Person(K(current.ReadArmyReturnBaseGovernor2E(reference), "message_governor"));
            if (!K(current.ReadPersonNativeValidity(person), "message_governor_validity")) return;
            //Offline paired CE SC/TC signed threshold, see source evidence.
            const int lowGarrisonThreshold = 5000;
            byte? tier = current.ReadBasePostLoadFlag66(reference);
            int troops = K(current.ReadBaseResource(reference, BaseResourceKind.Troops), "message_Base_troops");
            //An unresolved presentation-only stored tier must not gate the
            //already determined accepted pairs or Army flag writes.
            int? action = tier == 3 || troops < lowGarrisonThreshold ? 0x11 :
                tier is null ? null : row.BaseHasFormationContact ? 0x27 : 0x28;
            Line(2, K(current.ReadBaseObjectLink(reference), "message_Base_identity"), person, action,
                current.ReadBaseCurrentHexIndex(reference), 0, 0,
                action is null ? new(tier, troops, row.BaseHasFormationContact) : null);
            return;
        }
        var army = Army(actor.PhysicalSlot);
        var runtime = army.NativeTacticRuntime ?? throw M("message_Army_runtime");
        if (target.Kind == 27 && (runtime.Flags26 & 8) != 0 &&
            K(ArmyConfigurationSourceAdapter.ReadAction(state, Army(target.PhysicalSlot)), "message_target_action") is 11 or 12)
            ArmyLine(army, 0x24, Army(target.PhysicalSlot).OfficerId, 0);
        else ArmyLine(army, 0x26, target.Kind, target.PhysicalSlot);
        if (target.Kind == 27 && actor.ForceSlot is not (>= 46 and <= 51))
            ArmyLine(Army(target.PhysicalSlot), 0x25, army.OfficerId, 0);
    }

    internal void FirstContact(UnitState army, IReadOnlyList<NormalCombatParticipant> outgoing,
        NormalCombatParticipant? incoming)
    {
        int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw M("first_contact_Army_identity");
        if (!K(current.ReadArmyUnitNativeValidity(new(physical)), "first_contact_validity")) return;
        if (K(ArmyConfigurationSourceAdapter.ReadCategory(state, army), "first_contact_category") == 2)
        {
            foreach (var row in outgoing)
            {
                if (row.Kind != 27 || row.ForceSlot is >= 46 and <= 51) continue;
                var receiver = state.ReadArmyLifecycle()?.ReceiverSlots().ElementAtOrDefault(physical)
                    ?? throw M("first_contact_receiver");
                var target = OwnedArmyLifecycleTargets.Read(state, receiver.ReceiverId)
                    ?? throw M("first_contact_command_target");
                //38/3A is the current command target, separate from provocation3C/3E.
                if (target.Kind38 == 27 && target.Identity3A == row.PhysicalSlot) continue;
                ArmyLine(army, 0x23, Army(row.PhysicalSlot).OfficerId, 0);
            }
        }
        Report(army, outgoing, incoming);
    }

    private void ArmyLine(UnitState army, int action, int first, int second)
    {
        int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw M("message_Army_identity");
        if (!K(current.ReadArmyUnitNativeValidity(new(physical)), "message_Army_validity")) return;
        int person = Person(army.OfficerId);
        var date = state.NativeMapSeason?.ThirdDate ?? throw M("message_date");
        //22D6C0 uses the person's actual current position, not Unit.HexId.
        int hex = PersonReturnCommands.ResolvePersonPosition(state, current, person, date).PhysicalSlot ?? 48400;
        Line(1, physical, person, action, hex, first, second);
    }
    private void Line(int kind, int entity, int person, int? action, int hex, int first, int second,
        GarrisonContactChoice? garrisonChoice = null)
    {
        int officer = K(current.ReadPersonIdentity(person), "message_person_identity");
        //Only these ordinary actions create their own balloon at admission.
        //Missing presentation data stays in the unadmitted request; it does
        //not introduce a new combat gate before App's early queue rejection.
        int? force = null; byte? colour = null;
        if (action is null or 0x11 or 0x27 or 0x28)
        {
            var f = current.ReadPersonForce(person);
            force = f?.PhysicalSlot;
            if (f is not null) colour = current.ReadForceReportColor(f);
        }
        effects.Add(new(new CapturedPersonLine(kind, entity, officer, action, hex, first, second, 0, force, colour)
            { GarrisonChoice = garrisonChoice }, null));
    }

    private void Report(UnitState army, IReadOnlyList<NormalCombatParticipant> outgoing, NormalCombatParticipant? incoming)
    {
        int physical = army.NativeArmyOrigin!.NativeSlotIndex!.Value;
        int district = K(queries.ReadDistrict22BC70(physical), "report_district");
        var player = current.ReadCurrentPlayerForce() ?? throw M("report_player");
        if (!K(current.ReadForceNativeValidity(player), "report_player_validity")) return;
        if (K(current.ReadDistrictForceId(district <= 51 ? district : 0), "report_district_force") != player.PhysicalSlot) return;
        int leader = Person(K(current.ReadDistrictLeader(district <= 51 ? district : 0), "report_district_leader"));
        if (!K(current.ReadPersonNativeValidity(leader), "report_leader_validity") || K(current.ReadPersonStatus(leader), "report_leader_status") != 1) return;
        NormalCombatParticipant? selected = incoming;
        if (outgoing.Count != 0)
        {
            var sorted = outgoing.ToList();
            for (int index = 1; index < sorted.Count; index++)
            {
                var value = sorted[index]; int at = index;
                while (at > 0 && ReportBefore(value, sorted[at - 1])) { sorted[at] = sorted[at - 1]; at--; }
                sorted[at] = value;
            }
            selected = sorted[0];
        }
        if (selected is null) return;
        string targetName = selected.Kind switch
        {
            27 => package.ReadHistoryArmyName(state, current, new(selected.PhysicalSlot)),
            5 => state.Cities.GetValueOrDefault(selected.PhysicalSlot)?.Name ?? throw M("report_City_name"),
            6 => state.Gates.GetValueOrDefault(selected.PhysicalSlot + 51)?.Name ?? throw M("report_Gate_name"),
            _ => throw M("report_target_kind")
        };
        var actor = new ArmyUnitReference(physical);
        //1698F0 binds actor Army0 and the selected entity's actual name as
        //String0. It does not bind the selected Army/Base as another entity.
        var formatted = package.FormatHistoryReport(state, current, 0x5F6A,
            new HistoryMessageOperands(Army0: actor, String0: targetName));
        state = formatted.State;
        ushort reporter = K(current.ReadArmyUnitCommanderId(actor), "report_raw_Commander12");
        ushort hex = K(current.ReadArmyUnitRawHex2A(actor), "report_current_Army2A");
        int day = tactics.ReadCurrentSamplerWorld(state)?.Day450 ?? throw M("report_day");
        byte recipient = ReportDistrict(actor);
        var notifications = new List<CampaignEffectRequest>();
        state = package.WriteHistory(state, current, new ReportCommand(5, recipient, hex, day, formatted.Text,
            CampaignHistoryWriter.Supplemental(state), reporter, MarkBit4: 1), notifications);
        foreach (var notification in notifications)
            effects.Add(new(null, null) { HistoryNotification = notification.HistoryNotification });
    }

    private byte ReportDistrict(ArmyUnitReference army)
    {
        ushort raw = K(current.ReadArmyUnitCommanderId(army), "report_recipient_Commander12");
        var reference = (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
            ?? throw M("report_recipient_Person_lookup");
        if (reference.PhysicalSlot is not int person) return 0;
        return K(current.ReadPersonNativeValidity(person), "report_recipient_Person_validity")
            ? K(current.ReadPersonDistrictSlot(person), "report_recipient_E0") : (byte)0;
    }
    private bool ReportBefore(NormalCombatParticipant left, NormalCombatParticipant right)
    {
        if (left.Kind is 5 or 6) return true;
        if (right.Kind is 5 or 6) return false;
        if (left.Kind != 27 || right.Kind != 27) return left.Kind == 27 || left.PhysicalSlot < right.PhysicalSlot;
        int lp = Person(Army(left.PhysicalSlot).OfficerId), rp = Person(Army(right.PhysicalSlot).OfficerId);
        bool lr = K(current.ReadPersonStatus(lp), "report_status") == 1, rr = K(current.ReadPersonStatus(rp), "report_status") == 1;
        if (lr != rr) return lr;
        int a = K(queries.ReadLeadership(left.PhysicalSlot), "report_leadership"), b = K(queries.ReadLeadership(right.PhysicalSlot), "report_leadership");
        if (a != b) return a > b;
        int at = Army(left.PhysicalSlot).Troops, bt = Army(right.PhysicalSlot).Troops;
        return at != bt ? at > bt : left.PhysicalSlot < right.PhysicalSlot;
    }
    private UnitState Army(int physical)
    {
        var receiver = state.ReadArmyLifecycle()?.ReceiverSlots().ElementAtOrDefault(physical) ?? throw M("Army_receiver");
        return receiver.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) ?? throw M("Army_active")
            : receiver.PendingImage ?? throw M("Army_image");
    }
    private int Person(int id) => current.ReadPersonLookupReference(id <= 6000 ? id : 0)?.PhysicalSlot ?? throw M("Person_pointer");
    private static T K<T>(T? value, string field) where T : struct => NormalCombatMixedProducer.Need(value, field);
    private static InvalidDataException M(string field) => NormalCombatMixedProducer.Missing(field);
}
