using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState ApplyNormalTurnSelfRecommendationsCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, uint restoreApp,
        CampaignGroupingCheckpoint? grouping, ImmutableArray<int>? remaining = null)
        => new NormalTurnRecommendations(this, state, current, pool).Run(restoreApp, grouping, remaining);

    internal CommandResult ConfirmNormalTurnSelfRecommendation(GameState state,
        ConfirmSelfRecommendationOrder order, CommandContext context)
    {
        if (state.NativeNormalTurnContinuation is not { PendingSelfRecommendation: { } request } continuation ||
            request.OfficerId != order.OfficerId || request.ForceId != order.ForceId)
            return CommandResult.Reject("This recommendation is not awaiting a decision.", "recommendation.pending");
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            var pool = current.ReadOwnedCurrentListPool() ?? throw RecommendationMissing("shared_pool");
            BindNormalTurnPersonnelCurrent(state, current);
            var source = new NormalTurnRecommendations(this, state, current, pool);
            var result = source.Decide(request, order.Accepted);
            result = OwnedArmyReturnCurrentFields.PublishCurrent(result, current);
            result = OwnedArmyLifecycleDetachedInputs.PublishCurrent(result, current);
            var arena = result.ReadArmyLifecycle() ?? throw RecommendationMissing("lifecycle");
            return CommandResult.Accept(result.WithArmyLifecycle(arena with { Indexes = pool.ExportOwnedState() }) with
            { NativeNormalTurnContinuation = continuation with { PendingSelfRecommendation = null } });
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, error.Message); }
    }

    private void BindNormalTurnPersonnelCurrent(GameState state, SourceWorldContext current)
    {
        OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
        PersonReturnCommands.BindCurrent(state, current);
        //A resumed Turn owns the same full assignment publication contract.
        //Missing-only binding preserves every receiver already staged here.
        _personAssignmentPrecallInputs.BindMissingPersonAreaVectors(state, current);
        current.CurrentPlayerForce = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
        _baseCapacityInputs.BindCurrent(state, current);
        OwnedArmyReturnServiceInputs.BindCurrent(state, current);
        current.ArmyReturnTransferMode = ReadApplicationGameplay()?.TutorialServicePresent switch
        {
            false => new(false, null), true => new(true, null), _ => null
        };
        current.TransferModeStateReader = () => current.ArmyReturnTransferMode;
    }

    private sealed class NormalTurnRecommendations(ArmyCampaignRuleAdapter package, GameState input,
        SourceWorldContext current, WorldIndexContext pool)
    {
        private GameState state = input;
        private TacticRemakeCurrentAbilities? abilities;

        internal GameState Run(uint restoreApp, CampaignGroupingCheckpoint? grouping, ImmutableArray<int>? remaining)
        {
            var order = remaining ?? Collect();
            for (int index = 0; index < order.Length; index++)
            {
                if (Restricted()) continue;
                int person = Physical(order[index]);
                var target = package.ReadNormalTurnSelfRecommendationTargetCurrent(state, current, person);
                var district = current.ReadBaseDistrictReference(target.Home) ?? throw Missing("target_District78");
                if (!BaseCaptureRewardSource.ReadPlayerRulerDistrict(current, district, RecommendationMissing))
                {
                    FinishAccepted(person, target, false);
                    continue;
                }
                int ruler = Id(target.RulerPhysical ?? throw Missing("target_ruler"));
                var opening = Format(0x5DBA, new(Person0: ruler, Person1: Id(person), Person2: Id(person)));
                Sound(8);
                Append([new(null, null) { MonthlyMaintenance = new(MonthlyMaintenanceOperation.ConditionalSpeakerText,
                    null, 5012, opening.Text) }]);
                //831190 owns an explicit decision. Its body text is prepared
                //once, after the opening, while the officer is still free.
                var question = Format(0x5DC6, new(Person0: Id(person), Person1: ruler));
                var request = new SelfRecommendationRequest(Id(person), K(current.ReadForcePhysicalSlot(target.Force), "Force60"),
                    K(district.PhysicalSlot, "District_pointer"), ruler, question.Text);
                state = PublishCurrent();
                return state with { NativeNormalTurnContinuation = new(16, restoreApp,
                    order.Skip(index + 1).ToImmutableArray(), request,
                    grouping ?? throw Missing("normal_period_grouping_checkpoint")) };
            }
            state = PublishCurrent();
            return state with { NativeNormalTurnContinuation = null };
        }

        internal GameState Decide(SelfRecommendationRequest request, bool accepted)
        {
            int person = Physical(request.OfficerId);
            var target = package.ReadNormalTurnSelfRecommendationTargetCurrent(state, current, person);
            var district = current.ReadBaseDistrictReference(target.Home) ?? throw Missing("answer_District78");
            int ruler = Id(target.RulerPhysical ?? throw Missing("answer_ruler"));
            if (K(current.ReadForcePhysicalSlot(target.Force), "answer_Force60") != request.ForceId ||
                K(district.PhysicalSlot, "answer_District") != request.DistrictId || ruler != request.RulerOfficerId ||
                K(current.ReadPersonStatus(person), "answer_PersonE6") != 5 ||
                !BaseCaptureRewardSource.ReadPlayerRulerDistrict(current, district, RecommendationMissing))
                throw Missing("answer_changed_owner");
            if (accepted) FinishAccepted(person, target, true);
            else
            {
                var reply = Format(0x5DF8, new(Person0: Id(person), Person1: ruler));
                Sound(6); Speech(person, reply);
                var before = current.ReadOfficerAftermath(person) ?? throw Missing("answer_resentment_owner");
                int raw = Math.Min(ruler, 6000);
                bool mapped = state.Officers.ContainsKey(raw);
                current.SetOfficerAftermath(person, before with
                {
                    Resentment = new(mapped ? raw : null, 12),
                    SourceKnowledge = (before.SourceKnowledge ?? new(Id(person), null, null, null)) with
                    { UnmappedResentmentTarget = mapped ? null : raw }
                });
                state = current.PublishOfficerAftermath(state, Id(person));
            }
            return state;
        }

        private void FinishAccepted(int person, NormalTurnSelfRecommendationTarget target, bool showDialogue)
        {
            int ruler = Id(target.RulerPhysical ?? throw Missing("accept_ruler"));
            if (showDialogue)
            {
                var welcome = Format(0x5DC7, new(Person0: ruler, Person1: Id(person)));
                Sound(5); Speech(Physical(ruler), welcome);
                var answer = Format(0x5DDB, new(Person0: Id(person), Person1: ruler));
                Speech(person, answer);
            }
            int force = K(current.ReadForcePhysicalSlot(target.Force), "history_Force60");
            string main = Format(0x5E0C, new(Person0: Id(person), Force0: force)).Text;
            string detail = Format(0x521D, new(Person0: Id(person), Person1: ruler, Force0: force)).Text;
            var district = current.ReadBaseDistrictReference(target.Home) ?? throw Missing("history_District78");
            int position = K(PersonReturnCommands.ResolvePersonPosition(state, current, person,
                () => state.NativeMapSeason?.ThirdDate ?? throw Missing("history_date")).PhysicalSlot, "history_Hex");
            if (current.ReadOwnedPersonCommandManager() is { } tasks) state = state with { PersonnelCommands = tasks };
            var sampler = package.ReadArmyDestructionSampler(state, current) ?? throw Missing("history_day450");
            var effects = new List<CampaignEffectRequest>();
            state = package.WriteHistory(state, current, new(2, K(district.PhysicalSlot, "history_District"),
                position, sampler.Day450, main, detail, Id(person), MarkBit0: 1, MarkBit4: 1), effects);
            Append(effects);
            state = package.JoinNormalTurnSelfRecommendationCurrent(state, current, person, target.Home);
        }

        private ImmutableArray<int> Collect()
        {
            //The real temporary selection is allocated before any free-person
            //movement/NPC join, and all its original nodes are released before
            //publishing the semantic ordered remainder at the player stop.
            Lock(true);
            HexListHandle list;
            try { list = current.CreateTemporaryHexList() ?? throw Missing("selection_descriptor"); }
            finally { Lock(false); }
            try
            {
                foreach (var pointer in current.ReadRegistryReferences(RegistryKind.Person) ?? throw Missing("Person100"))
                {
                    if (pointer is not { Kind: RegistryKind.Person, PhysicalSlot: int physical }) throw Missing("Person100_payload");
                    if (K(current.ReadPersonStatus(physical), "PersonE6") != 5 ||
                        (K(current.ReadArmyReturnPersonFlags196(physical), "Person15E") & 0x4000) != 0 || Restricted()) continue;
                    var applied = package.ApplyNormalTurnFreePersonCurrent(state, current, pool, physical);
                    state = applied.State;
                    if (!applied.PlayerApplication) continue;
                    var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw Missing("selection_node");
                    if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, new(RegistryKind.Person, physical));
                }
                var rows = new List<(HexListNodeReference Node, int Physical)>();
                for (var node = current.ReadHexListHead(list) ?? throw Missing("selection_head"); node.Identity.HasValue;
                    node = current.ReadHexListNodeNext(node) ?? throw Missing("selection_next"))
                {
                    var value = current.ReadRegistryNodePayload(new(node.Identity)) ?? throw Missing("selection_payload");
                    if (value.Kind != RegistryKind.Person) throw Missing("selection_kind");
                    rows.Add((node, K(value.PhysicalSlot, "selection_Person")));
                }
                //2503D0/256B30: stable insertion of existing nodes, with the
                //source's initial Area(index).City lookup even for other E4 kinds.
                for (int i = 1; i < rows.Count; i++)
                {
                    var moved = rows[i]; int at = i;
                    while (at > 0 && Before(moved.Physical, rows[at - 1].Physical))
                    { rows[at] = rows[at - 1]; at--; }
                    rows[at] = moved;
                }
                current.ReorderCurrentList(list, rows.Select(row => row.Node).ToArray());
                return rows.Select(row => Id(row.Physical)).ToImmutableArray();
            }
            finally
            {
                Lock(true);
                try { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
                finally { Lock(false); }
            }
        }

        private bool Before(int first, int second)
        {
            var a = SortHome(first); var b = SortHome(second);
            int ag = K(current.ReadBaseResource(a, BaseResourceKind.Gold), "sort_City_gold34");
            int bg = K(current.ReadBaseResource(b, BaseResourceKind.Gold), "sort_City_gold34");
            if (ag != bg) return ag > bg;
            int ao = K(a.PhysicalSlot, "sort_City_ordinal"), bo = K(b.PhysicalSlot, "sort_City_ordinal");
            if (ao != bo) return ao < bo;
            int ac = Charm(first), bc = Charm(second);
            return ac != bc ? ac > bc : Id(first) < Id(second);
        }
        private BaseReference SortHome(int person)
        {
            var location = PersonPositionRepair.DecodeSecondLocation(K(current.ReadPersonSecondLocation(person), "sort_PersonE4"));
            var area = current.ReadAreaTableReference(location.Index < 501 ? location.Index : 0) ?? throw Missing("sort_Area");
            int id = K(current.ReadAreaCitySlot(area), "sort_Area35");
            return current.ReadCityTableReference(id < 52 ? id : 0) ?? throw Missing("sort_City");
        }
        private int Charm(int person)
        {
            abilities ??= new(current, package._scenario, package.Seed.Language, SimulationRuleIdentity.ForLocale(package.Seed.Language));
            return K(abilities.ResolveDimension(state, Id(person), 4,
                (force, effect) => package._baseCapacityInputs.ReadPanelModifier(state, force, effect)), "sort_effective_charm");
        }
        private bool Restricted() => package.ReadApplicationInformationRestriction(state) ?? throw Missing("application_selection_service");
        private int Physical(int id) => K((id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_pointer");
        private int Id(int person) => K(current.ReadPersonIdentity(person), "Person10");
        private HistoryTextResult Format(int root, HistoryMessageOperands operands)
        { var formatted = package.FormatNormalTurnReport(state, current, root, operands); state = formatted.State; return formatted; }
        private void Sound(int id) => Append([new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.SoundCue, id, null, null, null, null) }]);
        private void Speech(int person, HistoryTextResult formatted) => Append([CreateSingleOfficerPresentation(current, state, Id(person), formatted,
            TalentCommandPresentationOperation.ConditionalOfficerText, "normal.turn.recommendation")]);
        private void Append(IReadOnlyList<CampaignEffectRequest> effects)
        {
            if (effects.Count != 0) state = state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw Missing("pending_owner"))
                .Append(CampaignEffectSource.MonthlyMaintenance, state.NativeClock?.UpdateOrdinal ?? throw Missing("revision"), effects) };
        }
        private GameState PublishCurrent()
        {
            var result = OwnedArmyReturnCurrentFields.PublishCurrent(state, current);
            result = OwnedArmyLifecycleDetachedInputs.PublishCurrent(result, current);
            var arena = result.ReadArmyLifecycle() ?? throw Missing("lifecycle");
            return result.WithArmyLifecycle(arena with { Indexes = pool.ExportOwnedState() });
        }
        private void Lock(bool enter)
        {
            if (!K(current.ReadRegistryLockEnabled(), "registry_lock")) return;
            if (enter) current.EnterRegistryLock(); else current.ExitRegistryLock();
        }
        private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => RecommendationMissing(field);
    }

    private static InvalidDataException RecommendationMissing(string field) => new("normal.turn.self-recommendation." + field);
}
