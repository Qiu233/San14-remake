using System.Collections.ObjectModel;
using System.Globalization;
using San14.Core;

namespace San14.Data;

internal sealed record ForceDetailDiplomacyPublication(
    IReadOnlyDictionary<int, string> Text,
    IReadOnlyDictionary<int, string> UnresolvedFields)
{
    public ForceInformationRelation? Relation { get; init; }
    public IReadOnlySet<int> HiddenNodes { get; init; } = new HashSet<int>();
}

public sealed partial class ArmyCampaignRuleAdapter
{
    //906C50 establishes the current player as the common getter context.
    //All getters share this current; none captures another world or consumes RNG.
    internal ForceDetailDiplomacyPublication ReadForceDetailDiplomacyCurrent(
        GameState state, SourceWorldContext current, int forceId)
    {
        var text = new Dictionary<int, string>();
        var unresolved = new Dictionary<int, string>();
        var hidden = new HashSet<int>();
        ForceInformationRelation? relation = null;
        var force = current.ReadForceTableReference(forceId)
            ?? throw ForceDetailDiplomacyMissing("Force_pointer");
        int own = ForceDiplomacyNeed(current.ReadForcePhysicalSlot(force), "Force_ordinal");
        var player = new Lazy<ForceReference>(() => current.ReadCurrentPlayerForce()
            ?? throw ForceDetailDiplomacyMissing("current_Player_Force"));
        int Player() => ForceDiplomacyNeed(current.ReadForcePhysicalSlot(player.Value), "Player_ordinal");
        var objective = new Lazy<ForceCombatObjective>(() => state.Forces.GetValueOrDefault(forceId)?.CombatObjective
            ?? throw ForceDetailDiplomacyMissing("Force_combat_objective"));
        var requesting = new Lazy<ForceReference>(() => ResolveForce(
            ForceDiplomacyNeed(objective.Value.RequestingForceId, "Force15F_requester")));
        bool RequestActive() => ForceDiplomacyNeed(current.ReadForceNativeValidity(requesting.Value), "requester_validity");
        var coalitionRole = new Lazy<int>(ReadCoalitionRole);
        var intelligenceSuppressed = new Lazy<bool>(IntelligenceSuppressed);

        Read(133, () => (AllianceMonths() != 0) ? Data(1, 0x338) : own == Player() ? Data(1, 0x353) : "");
        Read(138, () => AllianceMonths() is int months && months != 0 ? Months(months) : "");
        Read(144, () => RequestActive() ? new CampaignHistoryWriter(_repository, _scenario, Seed.Language)
            .FallbackName(state, current, requesting.Value, (person, full) => full ? person.Name
                : person.CurrentSurname ?? throw ForceDetailDiplomacyMissing("requester_ruler_surname")) : "");
        Read(149, () =>
        {
            var target = BaseLookup.Find(ForceDiplomacyNeed(objective.Value.TargetLocationId, "Force160_request_Base"), current);
            if (!ForceDiplomacyNeed(current.ReadBaseNativeValidity(target), "request_Base_validity")) return "";
            int location = ForceDiplomacyNeed(current.ReadBaseObjectLink(target), "request_Base4E");
            return target.Kind == BaseKind.City
                ? state.Cities.GetValueOrDefault(location)?.Name ?? throw ForceDetailDiplomacyMissing("request_City_name")
                : state.Gates.GetValueOrDefault(location)?.Name ?? throw ForceDetailDiplomacyMissing("request_Gate_name");
        });
        Read(154, () => RequestActive() ? Months(ForceDiplomacyNeed(objective.Value.RemainingMonths, "Force161_request_months")) : "");
        Read(159, () => RequestActive() ? ForceDiplomacyNumber(ForceDiplomacyNeed(objective.Value.Progress, "Force162_request_progress")) : "");
        Read(165, () => coalitionRole.Value is int role && role != 0 ? Data(0, 0x801 + role) : "");
        Read(170, () => coalitionRole.Value != 0
            ? Months(ForceDiplomacyNeed(state.NativeArmySimulation?.World.Flags165C, "World165C_coalition_months")) : "");
        Read(175, () => (Flags() & 6) == 6 ? Data(1, 0x338) : "");
        Read(182, () => Intelligence(() => ReadForceMilitaryInformationTotalCurrent(current, own)));
        Read(187, () => Intelligence(() =>
        {
            BindPersonnelHireGenerationEconomyCurrent(state, current);
            return ReadHireCapitalAssociationGoldExpenseCurrent(state, current, force);
        }));
        try
        {
            //The text looks back from the target to the context; the marker
            //looks forward from the actual player. These are not symmetric.
            if (!ForceDiplomacyNeed(current.ReadForceNativeValidity(player.Value), "Player_validity") || own == Player())
                hidden.Add(128);
            else
            {
                int value = DLevel(state, DRelations(state, Player())[own]);
                string label = Player() == 0 ? Data(1, 0x353)
                    : Data(0, 0x141 + DLevel(state, DRelations(state, own)[Player()]));
                int[] sprites = [0x221, 0x221, 0x222, 0x21F, 0x21C, 0x21C];
                relation = new(label, value, sprites[value]);
            }
        }
        catch (InvalidDataException error) { unresolved[128] = error.Message; }
        return new(new ReadOnlyDictionary<int, string>(text), new ReadOnlyDictionary<int, string>(unresolved))
            { Relation = relation, HiddenNodes = hidden };

        void Read(int node, Func<string> getter)
        {
            try { text[node] = getter(); }
            catch (InvalidDataException error) { unresolved[node] = error.Message; }
        }
        string Data(int section, int index) => ReadHistoryDataText(section, index);
        string Months(int count)
        {
            //The normal TC and SC Data1/045F each contain one signed %d.
            string format = Data(1, 0x45F);
            int at = format.IndexOf("%d", StringComparison.Ordinal);
            if (at < 0 || format.IndexOf('%', at + 2) >= 0 || format[..at].Contains('%'))
                throw ForceDetailDiplomacyMissing("Data1_045F_format");
            return format[..at] + ForceDiplomacyNumber(count) + format[(at + 2)..];
        }
        int AllianceMonths() => ReadReportForceRelationByte(state, force, Player(), ForceDetailDiplomacyMissing) & 0x7F;
        ForceReference ResolveForce(int raw) => current.ReadForceTableReference(raw is >= 0 and < 52 ? raw : 0)
            ?? throw ForceDetailDiplomacyMissing("request_Force_pointer");
        byte Flags()
        {
            if (state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(own, out var reset) == true)
                return ForceDiplomacyNeed(reset[0x12], "Force12");
            return ForceDiplomacyNeed(state.NativeArmySimulation?.Forces.GetValueOrDefault(own)?.Flags12, "Force12");
        }
        int ReadCoalitionRole()
        {
            int target = ForceDiplomacyNeed(state.NativeArmySimulation?.World.SelectedForce1656, "World1656_coalition_target");
            if (target == 0) return 0;
            if (own == target) return 1;
            if (own == ForceDiplomacyNeed(state.NativeForceUnregisterCurrent?.OtherForce1657, "World1657_coalition_leader")) return 2;
            return (Flags() & 2) != 0 ? 3 : 0;
        }
        bool IntelligenceSuppressed()
        {
            var control = ReadTutorialControlState(state);
            int header = ForceDiplomacyNeed(TacticRemakeCurrentBattleControl.ReadHeader(control), "tutorial_controller");
            if (header != 0 && ForceDiplomacyNeed(TacticRemakeCurrentBattleControl.ReadSelectedLessonIndex(control), "selected_lesson") != -1)
                return true;
            int mode = ForceDiplomacyNeed(current.EventConditions.ScenarioMode, "Scenario_mode");
            if (mode == 3) throw ForceDetailDiplomacyMissing("CCCPersonSelectState_presence");
            return false;
        }
        string Intelligence(Func<int> getter)
        {
            if (intelligenceSuppressed.Value) return Data(1, 0x353);
            if (!ForceDiplomacyNeed(current.ReadForceNativeValidity(force), "Force_validity")) return "";
            if (own is >= 46 and <= 50) return Data(1, 0x353);
            return ForceDiplomacyNumber(getter());
        }
    }

    //364310: the actual registered District collection, current Force filter,
    //native-validity gate and signed Int32 accumulation. Pure borrowed-current
    //consumer shared by the ordinary Force page and the live edit draft.
    internal static int ReadForceMilitaryInformationTotalCurrent(SourceWorldContext current, int force)
    {
        int total = 0;
        foreach (var entry in current.ReadArmyReturnRegisteredDistricts()
            ?? throw ForceDetailDiplomacyMissing("District_registry"))
        {
            var district = entry ?? throw ForceDetailDiplomacyMissing("District_pointer");
            int at = ForceDiplomacyNeed(district.PhysicalSlot, "District_physical");
            if (ForceDiplomacyNeed(current.ReadDistrictForceId(at), "District10") != force) continue;
            if (!ForceDiplomacyNeed(current.ReadDistrictNativeValidity(district), "District_validity")) continue;
            total = unchecked(total + ReadDistrictAdministrationIntelligenceCurrent(current, at));
        }
        return total;
    }

    private static string ForceDiplomacyNumber(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static T ForceDiplomacyNeed<T>(T? value, string field) where T : struct
        => value ?? throw ForceDetailDiplomacyMissing(field);
    private static InvalidDataException ForceDetailDiplomacyMissing(string field)
        => new("force-detail.diplomacy." + field);
}
