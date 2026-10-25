using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //42AC90/2A3390 owns one invitation. Its selected members are retained across
    //the real Yes/No boundary; answering never repeats selection or sampling.
    internal GameState ApplyNormalTurnMonthlyCoalitionFormationCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, uint? restoreApp = null,
        CampaignGroupingCheckpoint? grouping = null)
        => new NormalTurnCoalitionFormation(this, state, current, pool).Run(restoreApp, grouping);

    internal CommandResult ConfirmNormalTurnCoalitionInvitation(GameState state,
        ConfirmCoalitionInvitationOrder order, CommandContext context)
    {
        if (state.NativeNormalTurnContinuation is not { NextAction: 25, PendingCoalitionInvitation: { } request } continuation ||
            request.PlayerForceId != order.ForceId || request.TargetForceId != order.TargetForceId)
            return CommandResult.Reject("This coalition invitation is not awaiting a decision.", "coalition.invitation.pending");
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            var pool = current.ReadOwnedCurrentListPool() ?? throw CoalitionFormationMissing("answer_pool");
            BindNormalTurnPersonnelCurrent(state, current);
            var result = new NormalTurnCoalitionFormation(this, state, current, pool).Decide(request, order.Accepted);
            result = OwnedArmyReturnCurrentFields.PublishCurrent(result, current);
            result = OwnedArmyLifecycleDetachedInputs.PublishCurrent(result, current);
            var arena = result.ReadArmyLifecycle() ?? throw CoalitionFormationMissing("answer_lifecycle");
            return CommandResult.Accept(result.WithArmyLifecycle(arena with { Indexes = pool.ExportOwnedState() }) with
            { NativeNormalTurnContinuation = continuation with { NextAction = 26, PendingCoalitionInvitation = null } });
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, error.Message); }
    }

    private sealed class NormalTurnCoalitionFormation(ArmyCampaignRuleAdapter package,
        GameState state, SourceWorldContext current, WorldIndexContext pool)
    {
        private readonly List<CampaignEffectRequest> effects = [];

        internal GameState Run(uint? restoreApp, CampaignGroupingCheckpoint? grouping)
        {
            var native = state.NativeArmySimulation ?? throw Missing("native_owner");
            if (K(native.World.Flags165C, "World165C") != 0) return state;
            var world = state.CampaignEventWorld ?? throw Missing("World48_owner");
            if (world.ScenarioId == 71) return state;
            var season = state.NativeMapSeason ?? throw Missing("calendar");
            var cooldown = native.World.PlayerCoalitionCooldownDate ?? throw Missing("World16A0_cooldown");
            if (DateValue(season.ThirdDate) < DateValue(cooldown)) return state;
            if (package.Seed.Language != "TC") throw Missing("locale_rules");
            int difficulty = Difficulty();
            int minimum = difficulty switch { 0 => 108, 1 => 90, 2 => 72, _ => 54 };
            if (unchecked(world.ElapsedPeriodOffset + (int)season.AdvancedPeriods) <= minimum) return state;
            foreach (var force in Forces())
                if ((K(state.NativeArmySimulation?.Forces.GetValueOrDefault(Ordinal(force))?.DiplomacyFlags178, "Force178") & 1) != 0) return state;
            var target = package.SelectNormalTurnCoalitionTargetCurrent(state, current, pool);
            if (!Valid(target)) return state;

            var candidates = Temporary();
            bool released = false;
            try
            {
                int targetRuler = Ruler(target);
                if (!ValidPerson(targetRuler)) return state;
                foreach (var force in Forces())
                {
                    int ordinal = Ordinal(force), targetId = Ordinal(target);
                    if (force == target || ordinal is >= 46 and <= 50 ||
                        (ReadReportForceRelationByte(state, force, targetId, Missing) & 127) != 0 ||
                        targetId < 52 && DRelations(state, K(force.PhysicalSlot, "candidate_Force"))[targetId] >= 70) continue;
                    int ruler = Ruler(force);
                    if (!ValidPerson(ruler)) continue;
                    var likes = Temporary();
                    bool excluded;
                    try
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            int raw = K(OwnedPersonRelationInputs.ReadCurrentLikeIdentity(state, ruler, i), "candidate_like_C0");
                            int person = Person(raw);
                            if (ValidPerson(person)) Add(likes, RegistryKind.Person, person);
                        }
                        excluded = Values(likes, RegistryKind.Person).Contains(targetRuler);
                    }
                    finally { Release(likes); }
                    if (!excluded) Add(candidates, RegistryKind.Force, K(force.PhysicalSlot, "candidate_pointer"));
                }
                var members = Values(candidates, RegistryKind.Force).ToImmutableArray();
                if (members.Length < 3) return state;
                bool hasPlayer = members.Any(id => Player(new(id)));
                if (!Player(target) && !hasPlayer && Forces().Any(Player)) return state;
                var sampler = package.ReadArmyDestructionSampler(state, current) ?? throw Missing("trial_World450_45C");
                int sample = TacticEffectSourceSamplerMath.Sample(100, 1, Ordinal(target), season.ThirdDate.Year,
                    season.ThirdDate.Month, 0, sampler);
                int chance = difficulty switch { 0 => 20, 1 => 30, 2 => 35, _ => 40 };
                if (sample >= chance) return state;
                int? camera = null;
                if (hasPlayer)
                {
                    var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("player_Force");
                    if (Valid(player))
                    {
                        int ruler = Ruler(player);
                        if (ValidPerson(ruler))
                        {
                            var home = BaseLookup.Find(K(current.ReadPersonFirstLocation(ruler), "camera_ruler_E2"), current);
                            if (K(current.ReadBaseNativeValidity(home), "camera_Base_validity"))
                            { camera = HomeHex(home); Visual(ForceCoalitionVisualOperation.CameraFocus, camera); }
                        }
                    }
                    var question = package.PrepareNormalTurnCoalitionInvitationCurrent(state, current, target, members);
                    state = question.State;
                    if (question.Request is { } request)
                    {
                        Flush();
                        Release(candidates); released = true;
                        state = Publish();
                        return state with { NativeNormalTurnContinuation = new(25,
                            restoreApp ?? throw Missing("normal_period_saved_App"), ImmutableArray<int>.Empty, null,
                            grouping ?? throw Missing("normal_period_grouping_checkpoint"))
                            { PendingCoalitionInvitation = request with { CameraHexId = camera } } };
                    }
                    Failed(target, camera);
                    return state;
                }
                Form(target, members, camera);
                return state;
            }
            finally { if (!released) Release(candidates); }
        }

        internal GameState Decide(CoalitionInvitationRequest request, bool accepted)
        {
            var target = Force(request.TargetForceId);
            //The original modal scope cannot advance the world while waiting.
            //No candidate selection or random trial is repeated here.
            int playerRuler = Person(request.PlayerRulerId), targetRuler = Ruler(target);
            var reply = Format(accepted ? 0x5E89 : 0x5E8E, new(Person0: request.PlayerRulerId,
                Person1: request.SpeakerOfficerId, Person2: accepted ? Id(targetRuler) : 0));
            Speech(playerRuler, reply, false);
            Flush();
            if (accepted)
            {
                var retained = Temporary();
                try
                {
                    foreach (int id in request.OrderedMemberForceIds) Add(retained, RegistryKind.Force, K(Force(id).PhysicalSlot, "answer_member"));
                    Form(target, Values(retained, RegistryKind.Force), request.CameraHexId);
                }
                finally { Release(retained); }
            }
            else Failed(target, request.CameraHexId);
            return Publish();
        }

        private void Form(ForceReference target, IReadOnlyList<int> members, int? camera)
        {
            var order = state.ForceProcessingOrder?.ForceIds ?? throw Missing("saved_coalition_preference_order");
            var ranked = Temporary();
            try
            {
                foreach (int id in order)
                    Add(ranked, RegistryKind.Force, K(Force(id).PhysicalSlot, "ranked_Force_pointer"));
                //2A3AAF's first match jumps out of both loops to2A3B03.
                ForceReference? leader = null;
                foreach (int id in Values(ranked, RegistryKind.Force))
                    if (members.Contains(id)) { leader = new(id); break; }
                if (leader is null || !Valid(leader)) return;
                int ruler = Ruler(leader);
                if (!ValidPerson(ruler)) return;
                var operands = new HistoryMessageOperands(Person0: 5012, Person1: 0, Person2: Id(ruler),
                    Force0: Ordinal(leader), Force1: Ordinal(target));
                var main = Format(0x5E94, operands);
                var detail = Format(0x5E95, operands);
                int day = (package.ReadArmyDestructionSampler(state, current) ?? throw Missing("history_World450")).Day450;
                var home = BaseLookup.Find(K(current.ReadPersonFirstLocation(Ruler(target)), "history_target_home"), current);
                state = package.WriteHistory(state, current, new(1, 0, HomeHex(home), day, main.Text, detail.Text,
                    5012, MarkBit0: 1, MarkBit4: 0), effects);
                if (Player(target))
                    Speech(Person(5012), Format(0x5E96, new(Person0: 5012,
                        Person1: Id(Ruler(target)), Person2: Id(ruler))), true);
                Visual(ForceCoalitionVisualOperation.NarrationEnter, null, 40);
                var announcement = Format(0x5E97, new(Person0: Id(ruler), Force0: Ordinal(target)));
                Sound(129); Announcement(announcement.Text);
                Visual(ForceCoalitionVisualOperation.NarrationExit);
                Flush();
                state = package.ApplyNormalTurnCoalitionActivationCurrent(state, current, pool, target, leader, members, effects);
                Flush();
                Restore(camera);
            }
            finally
            {
                Release(ranked);
            }
        }

        private void Failed(ForceReference target, int? camera)
        {
            Sound(6);
            Announcement(Format(0x5E93, new(Force0: Ordinal(target))).Text);
            Restore(camera); Flush();
        }
        private void Restore(int? camera)
        { if (camera.HasValue) { Visual(ForceCoalitionVisualOperation.CameraRestore); Flush(); } }
        private HistoryTextResult Format(int root, HistoryMessageOperands operands)
        { var value = package.FormatNormalTurnReport(state, current, root, operands); state = value.State; return value; }
        private void Speech(int person, HistoryTextResult line, bool conditional)
            => effects.Add(CreateStrategicCommandPresentation(current, state, Id(person), line, conditional));
        private void Sound(int id) => effects.Add(new(null, null) { TalentCommandPresentation =
            new(TalentCommandPresentationOperation.SoundCue, id, null, null, null, null) });
        private void Announcement(string text) => effects.Add(new(null, null) { TalentCommandPresentation =
            new(TalentCommandPresentationOperation.Announcement, null, null, null, null, text) });
        private void Visual(ForceCoalitionVisualOperation operation, int? hex = null, int? scene = null)
            => effects.Add(new(null, null) { ForceCoalitionVisual = new(operation, hex) { SceneKind = scene } });
        private void Flush()
        {
            if (effects.Count == 0) return;
            state = state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw Missing("pending_owner"))
                .Append(CampaignEffectSource.MonthlyMaintenance, state.NativeClock?.UpdateOrdinal ?? throw Missing("revision"), effects) };
            effects.Clear();
        }
        private GameState Publish()
            => state.WithArmyLifecycle((state.ReadArmyLifecycle() ?? throw Missing("lifecycle")) with { Indexes = pool.ProjectOwnedRoles() });
        private int Difficulty()
        {
            int selector = K(BattleRemakeCurrentSettings.ReadSelectorE58(state), "settings_selector");
            return K(BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x134 : 0x28), "difficulty");
        }
        private static long DateValue(NativeMapSeasonDate date)
            => (long)date.Year * 8640 + (date.Month - 1) * 720 + (date.Day - 1) * 24 + date.Hour;
        private IEnumerable<ForceReference> Forces()
        {
            foreach (var row in current.ReadRegistryReferences(RegistryKind.Force) ?? throw Missing("ForceC0"))
                yield return row is { Kind: RegistryKind.Force, PhysicalSlot: int id } ? new(id) : throw Missing("ForceC0_payload");
        }
        private ForceReference Force(int id) => current.ReadForceTableReference(id < 52 ? id : 0) ?? throw Missing("Force_table");
        private bool Valid(ForceReference force) => K(current.ReadForceNativeValidity(force), "Force_validity");
        private bool Player(ForceReference force) => AbsorptionPlayer(current, state, force);
        private int Ordinal(ForceReference force) => K(current.ReadForcePhysicalSlot(force), "Force_identity");
        private int Person(int id) => K((id < 6001 ? current.ReadPersonLookupReference(id)
            : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_lookup");
        private bool ValidPerson(int physical) => K(current.ReadPersonNativeValidity(physical), "Person_validity");
        private int Id(int physical) => K(current.ReadPersonIdentity(physical), "Person_identity");
        private int Ruler(ForceReference force) => Person(K(current.ReadForceRecord(K(force.PhysicalSlot, "ruler_Force"))?.RawRuler10, "Force_ruler10"));
        private int HomeHex(BaseReference home)
        {
            int raw = K(current.ReadBaseObjectLink(home), "home_Object_link");
            var obj = current.ReadObjectTableReference(raw < 3001 ? raw : 0) ?? throw Missing("home_Object");
            return K(current.ReadObjectNativeValidity(obj), "home_Object_validity") ? K(current.ReadObjectHexIndex(obj), "home_Object_Hex") : 48400;
        }
        private HexListHandle Temporary() => current.CreateTemporaryHexList() ?? throw Missing("temporary_descriptor");
        private void Add(HexListHandle list, RegistryKind kind, int physical)
        {
            var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw Missing("temporary_node");
            if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, new(kind, physical));
        }
        private List<int> Values(HexListHandle list, RegistryKind kind)
        {
            var result = new List<int>();
            var node = current.ReadRegistryHead(new(list.Identity)) ?? throw Missing("temporary_head");
            while (node.Identity.HasValue)
            {
                var value = current.ReadRegistryNodePayload(node) ?? throw Missing("temporary_payload");
                if (value.Kind != kind) throw Missing("temporary_payload_kind");
                result.Add(K(value.PhysicalSlot, "temporary_payload_pointer"));
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("temporary_next");
            }
            return result;
        }
        private void Release(HexListHandle list) { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
        private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => CoalitionFormationMissing(field);
    }
    private static InvalidDataException CoalitionFormationMissing(string field)
        => new("normal.turn.coalition.formation." + field);
}
