using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState SynchronizeDiplomacyProjection(GameState state)
    {
        var native = state.NativeArmySimulation ?? throw DiplomacyMissing("native_relation_owner");
        var relations = new Dictionary<long, DiplomaticRelationship>();
        var treaties = new Dictionary<long, DiplomaticTreaty>();
        foreach (var first in state.Forces.Values)
        {
            if (!first.IsActive || first.Id == 0) continue;
            foreach (var second in state.Forces.Values)
            {
                if (!second.IsActive || second.Id <= first.Id) continue;
                var fields = native.Forces.GetValueOrDefault(first.Id) ?? throw DiplomacyMissing("native_relation_Force");
                var values = fields.NormalAftermath?.Relations13 ?? throw DiplomacyMissing("native_relation13");
                if (values.Length != 52 || fields.RelationsEA is not { Length: 52 } months) throw DiplomacyMissing("native_relation_dimensions");
                int level = 5;
                for (int index = 1; index <= 5; index++)
                {
                    int threshold = ArmyDestructionParameterSeed.ReadCurrent(state, SimulationParameterKeys.ForceRelations.LevelThreshold(index))
                        ?? throw DiplomacyMissing("native_relation_threshold");
                    if (values[second.Id] >= threshold) continue;
                    level = index - 1; break;
                }
                long key = PlayableDiplomacy.PairKey(first.Id, second.Id);
                relations[key] = new(first.Id, second.Id, (DiplomaticRelation)level, "san14:ce-native-relations13");
                int remaining = months[second.Id] & 0x7F;
                if (remaining != 0)
                    treaties[key] = new(first.Id, second.Id, DiplomaticTreatyKind.Alliance, state.PeriodNumber,
                        checked(state.PeriodNumber + remaining * 3), "san14:ce-native-relationsEA");
            }
        }
        return state with { Diplomacy = new("san14:ce-native-diplomacy", relations, treaties,
            state.Diplomacy?.Missions ?? [], state.Diplomacy?.AttackRequests ?? [], state.Diplomacy?.NextMissionId ?? 1) };
    }

    internal GameState ApplyDiplomacyRelationCurrent(GameState state, SourceWorldContext current,
        int first, int second, int adjustment, List<CampaignEffectRequest> effects)
    {
        int before = ReadDiplomacyRelationCurrent(state, current, first, second).Level;
        state = ForceRelationChangeCurrent.ApplyWithoutPresentation(state, current, first, second, adjustment,
            key => ArmyDestructionParameterSeed.ReadCurrent(state, key) ?? throw DiplomacyMissing("relation_parameter_" + key));
        int after = ReadDiplomacyRelationCurrent(state, current, first, second).Level;
        var mode = current.ArmyReturnTransferMode ?? throw DiplomacyMissing("relation_presentation_mode");
        if (mode.Present && TalentKnown(mode.Value, "relation_presentation_mode_value") != 0) return state;
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw DiplomacyMissing("relation_player");
        int playerId = TalentKnown(player.PhysicalSlot, "relation_player_identity");
        if (playerId == 0 || playerId != first && playerId != second) return state;
        int root;
        if (before != after) root = 0x3055 + after;
        else if (adjustment < 0) root = 0x305B;
        else if (adjustment == 0) return state;
        else
        {
            var own = current.ReadForceTableReference(first) ?? throw DiplomacyMissing("relation_first");
            var other = current.ReadForceTableReference(second) ?? throw DiplomacyMissing("relation_second");
            var a = PersonReturnPathSource.ReadForceAdvantage(state, current, own, 8, 100);
            _ = a.Value ?? throw DiplomacyMissing(a.UnresolvedOwner ?? "relation_advantage8_first");
            var b = PersonReturnPathSource.ReadForceAdvantage(state, current, other, 8, 100);
            _ = b.Value ?? throw DiplomacyMissing(b.UnresolvedOwner ?? "relation_advantage8_second");
            root = a.QualifiedStates.Length != 0 || b.QualifiedStates.Length != 0 ? 0x305D : 0x305C;
        }
        var message = FormatDiplomacyReportCurrent(state, current, root, new(Force0: first, Force1: second));
        effects.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.SoundCue, 8, null, null, null, null) });
        effects.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.Announcement, null, null, null, null, message.Text) });
        return message.State;
    }
}
