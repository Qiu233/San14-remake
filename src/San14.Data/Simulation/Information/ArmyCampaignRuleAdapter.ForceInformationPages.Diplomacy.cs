using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class ForceInformationCurrent
    {
        private readonly Dictionary<int, ForceDetailDiplomacyPublication> diplomacyPublications = [];
        private readonly Dictionary<(int Force, int Column), int> diplomacyScalars = [];
        private bool? diplomacySuppressed;

        private PreparedCell DiplomacyCell(int forceId, int column) => column switch
        {
            12 => MakeCell(() => DiplomacyRelationText(forceId), () => DiplomacyRelationKey(forceId)),
            13 => MakeCell(() => DiplomacyText(forceId, 133), () =>
                DiplomacyAllianceMonths(forceId) != 0 ? new NumberKey(1) : new NumberKey(int.MaxValue, true)),
            14 => MakeCell(() => DiplomacyText(forceId, 138), () =>
            {
                int months = DiplomacyAllianceMonths(forceId);
                return new NumberKey(months, months == 0);
            }),
            21 => MakeCell(() => DiplomacyText(forceId, 144), () => DiplomacyRequestActive(forceId)
                ? new NameKey(DiplomacyText(forceId, 144)) : new NameKey("", true)),
            //The requested Base's validity is independent of requester validity.
            22 => MakeCell(() => DiplomacyText(forceId, 149), () => DiplomacyRequestBaseValid(forceId)
                ? new NameKey(DiplomacyText(forceId, 149)) : new NameKey("", true)),
            23 => MakeCell(() => DiplomacyText(forceId, 154), () => DiplomacyRequestActive(forceId)
                ? new NumberKey(DiplomacyRequestMonths(forceId)) : new NumberKey(int.MaxValue, true)),
            24 => MakeCell(() => DiplomacyText(forceId, 159), () => DiplomacyRequestActive(forceId)
                ? new NumberKey(DiplomacyRequestProgress(forceId)) : new NumberKey(int.MaxValue, true)),
            25 => MakeCell(() => DiplomacyText(forceId, 165), () =>
            {
                int role = DiplomacyCoalitionRole(forceId);
                return new NumberKey(role, role == 0);
            }),
            26 => MakeCell(() => DiplomacyText(forceId, 170), () => DiplomacyCoalitionRole(forceId) != 0
                ? new NumberKey(Need(state.NativeArmySimulation?.World.Flags165C, "diplomacy.coalition_months"))
                : new NumberKey(int.MaxValue, true)),
            //345CB0 writes numeric tag0 unconditionally. Unlike columns13/25,
            //a false flag combination is a genuine numeric0, not empty tag2.
            27 => MakeCell(() => DiplomacyText(forceId, 175), () =>
                new NumberKey((DiplomacyFlags(forceId) & 6) == 6 ? 1 : 0)),
            37 => MakeCell(() => DiplomacyText(forceId, 182), () => DiplomacyOrdinary(forceId)
                ? new NumberKey(DiplomacyScalar(forceId, 37)) : new NumberKey(int.MaxValue, true)),
            38 => MakeCell(() => DiplomacyText(forceId, 187), () => DiplomacyOrdinary(forceId)
                ? new NumberKey(DiplomacyScalar(forceId, 38)) : new NumberKey(int.MaxValue, true)),
            41 => MakeCell(() => DiplomacyInnerText(forceId, 41), () => DiplomacyOrdinary(forceId)
                ? new NumberKey((DiplomacyFlags(forceId) & 8) != 0 ? 1 : 0) : new NumberKey(int.MaxValue, true)),
            43 => MakeCell(() => DiplomacyInnerText(forceId, 43), () => DiplomacyOrdinary(forceId)
                ? new NumberKey(DiplomacyRanking(forceId)) : new NumberKey(int.MaxValue, true)),
            _ => throw Missing("diplomacy.column." + column)
        };

        private string DiplomacyText(int forceId, int node)
        {
            if (!diplomacyPublications.TryGetValue(forceId, out var publication))
                diplomacyPublications.Add(forceId, publication = rules.ReadForceDetailDiplomacyCurrent(state, Current, forceId));
            if (publication.UnresolvedFields.TryGetValue(node, out string? issue)) throw new InvalidDataException(issue);
            return publication.Text.TryGetValue(node, out string? text) ? text : throw Missing("diplomacy.text." + node);
        }

        private int DiplomacyIdentity(int forceId) => Need(Current.ReadForcePhysicalSlot(Force(forceId)), "diplomacy.Force_identity");
        private int DiplomacyPlayer()
        {
            var player = Current.ReadCurrentPlayerForce() ?? throw Missing("diplomacy.current_player");
            return Need(Current.ReadForcePhysicalSlot(player), "diplomacy.player_identity");
        }
        private string DiplomacyRelationText(int forceId)
        {
            int player = DiplomacyPlayer(), own = DiplomacyIdentity(forceId);
            if (player == 0 || own == player) return Data(1, 0x353);
            //362760 uses only target->context. The detail marker also reads
            //context->target; that additional direction is not a list operand.
            return Data(0, 0x141 + DLevel(state, DiplomacyFriendship(own, player)));
        }
        private SortKey DiplomacyRelationKey(int forceId)
        {
            int player = DiplomacyPlayer(), own = DiplomacyIdentity(forceId);
            return player == 0 || own == player ? new NumberKey(int.MaxValue, true)
                : new NumberKey(DiplomacyFriendship(own, player));
        }
        private byte DiplomacyFriendship(int actor, int other)
        {
            if ((uint)other >= 52) return 0; //2ADA50/34F730's actual indexed-read gate.
            if (state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(actor, out var image) == true)
                return Need(image[0x13 + other], "diplomacy.reset_friendship13");
            return DRelations(state, actor)[other];
        }
        private int DiplomacyAllianceMonths(int forceId) => ReadReportForceRelationByte(state,
            Force(forceId), DiplomacyPlayer(), Missing) & 0x7F;

        private ForceCombatObjective DiplomacyObjective(int forceId) =>
            state.Forces.GetValueOrDefault(DiplomacyIdentity(forceId))?.CombatObjective
                ?? throw Missing("diplomacy.request_objective");
        private bool DiplomacyRequestActive(int forceId)
        {
            int raw = Need(DiplomacyObjective(forceId).RequestingForceId, "diplomacy.requester15F");
            var requester = Current.ReadForceTableReference(raw is >= 0 and < 52 ? raw : 0)
                ?? throw Missing("diplomacy.requester_pointer");
            return Need(Current.ReadForceNativeValidity(requester), "diplomacy.requester_validity");
        }
        private bool DiplomacyRequestBaseValid(int forceId)
        {
            int location = Need(DiplomacyObjective(forceId).TargetLocationId, "diplomacy.request_Base160");
            var target = BaseLookup.Find(location, Current);
            return Need(Current.ReadBaseNativeValidity(target), "diplomacy.request_Base_validity");
        }
        private int DiplomacyRequestMonths(int forceId) =>
            Need(DiplomacyObjective(forceId).RemainingMonths, "diplomacy.request_months161");
        private int DiplomacyRequestProgress(int forceId) =>
            Need(DiplomacyObjective(forceId).Progress, "diplomacy.request_progress162");
        private int DiplomacyCoalitionRole(int forceId)
        {
            int target = Need(state.NativeArmySimulation?.World.SelectedForce1656, "diplomacy.coalition_target1656");
            if (target == 0) return 0;
            int own = DiplomacyIdentity(forceId);
            if (own == target) return 1;
            if (own == Need(state.NativeForceUnregisterCurrent?.OtherForce1657, "diplomacy.coalition_leader1657")) return 2;
            return (DiplomacyFlags(forceId) & 2) != 0 ? 3 : 0;
        }
        private byte DiplomacyFlags(int forceId)
        {
            int own = DiplomacyIdentity(forceId);
            if (state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(own, out var image) == true)
                return Need(image[0x12], "diplomacy.reset_Flags12");
            return own == 0 ? Need(state.NativeForceUnregisterCurrent?.PlaceholderFlags12, "diplomacy.placeholder_Flags12")
                : Need(state.NativeArmySimulation?.Forces.GetValueOrDefault(own)?.Flags12, "diplomacy.Flags12");
        }
        private bool DiplomacyOrdinary(int forceId) => Valid(forceId) && !Special(DiplomacyIdentity(forceId));

        private int DiplomacyScalar(int forceId, int column)
        {
            if (diplomacyScalars.TryGetValue((forceId, column), out int saved)) return saved;
            int value;
            if (column == 37)
            {
                int own = DiplomacyIdentity(forceId), total = 0;
                foreach (var entry in Current.ReadArmyReturnRegisteredDistricts() ?? throw Missing("diplomacy.District_registry"))
                {
                    var district = entry ?? throw Missing("diplomacy.District_pointer");
                    int at = Need(district.PhysicalSlot, "diplomacy.District_identity");
                    if (Need(Current.ReadDistrictForceId(at), "diplomacy.District10") != own ||
                        !Need(Current.ReadDistrictNativeValidity(district), "diplomacy.District_validity")) continue;
                    total = unchecked(total + ReadDistrictAdministrationIntelligenceCurrent(Current, at));
                }
                value = total;
            }
            else
            {
                rules.BindPersonnelHireGenerationEconomyCurrent(state, Current);
                value = rules.ReadHireCapitalAssociationGoldExpenseCurrent(state, Current, Force(forceId));
            }
            diplomacyScalars.Add((forceId, column), value);
            return value;
        }

        private bool DiplomacySuppressed()
        {
            if (diplomacySuppressed is bool known) return known;
            var control = rules.ReadTutorialControlState(state);
            int header = Need(TacticRemakeCurrentBattleControl.ReadHeader(control), "diplomacy.tutorial_header");
            if (header != 0 && Need(TacticRemakeCurrentBattleControl.ReadSelectedLessonIndex(control), "diplomacy.selected_lesson") != -1)
                return (diplomacySuppressed = true).Value;
            int mode = Need(Current.EventConditions.ScenarioMode, "diplomacy.Scenario_mode");
            //There is no live CCCPersonSelectState owner in the ordinary
            //campaign. Do not invent its absence if that separate mode arrives.
            if (mode == 3) throw Missing("diplomacy.CCCPersonSelectState_presence");
            return (diplomacySuppressed = false).Value;
        }
        private string DiplomacyInnerText(int forceId, int column)
        {
            if (DiplomacySuppressed()) return Data(1, 0x353);
            if (!Valid(forceId)) return "";
            if (Special(DiplomacyIdentity(forceId))) return Data(1, 0x353);
            if (column == 43) return Number(DiplomacyRanking(forceId));
            byte flags = DiplomacyFlags(forceId);
            return (flags & 8) != 0 ? Data(1, 0x338) : (flags & 0x10) != 0 ? Data(1, 0x339) : "";
        }
        private ushort DiplomacyRanking(int forceId) => Need(state.NativeForceUnregisterCurrent?.ForceRankings
            .GetValueOrDefault(DiplomacyIdentity(forceId))?.Word1BC, "diplomacy.Force1BC");
    }
}
