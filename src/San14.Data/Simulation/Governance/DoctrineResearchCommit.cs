using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public CommandResult ApplyDoctrineResearch(GameState state, ForceState force,
        IReadOnlyList<DoctrineResearchStep> steps, CommandContext context)
    {
        try
        {
            if (steps.Count == 0) return CommandResult.Reject("No research was selected.", "research.selection");
            var world = CaptureOwnedArmyReturnWorld(state);
            var current = world.Context;
            //The Core strategy order owner has admitted this direct normal
            //command. The UI must separately supply its actual controller host.
            var rules = CaptureDoctrineResearchRules(state, current, force.Id, true);
            if (!rules.HostAvailable) return CommandResult.Reject("Research requires the player strategy controller.", "research.host");
            var working = rules.Baseline.Copy();
            var result = state;
            bool redefined = false;
            foreach (var step in steps)
            {
                if (step.Operation == DoctrineResearchOperation.Research && step.PanelId is int id)
                {
                    if (id is < 2 or > 79 || !rules.Panels.TryGetValue(id, out var panel))
                        return CommandResult.Reject("The research panel is invalid.", "research.panel");
                    int reason = DoctrineResearchSource.CanResearch(rules, working, id);
                    if (reason != 0) return CommandResult.Reject("The panel is no longer eligible for research.", "research.eligibility." + reason);
                    DoctrineResearchSource.Apply(working, rules.Baseline, panel,
                        rules.RulerDoctrineId, rules.MatchingDiscount, rules.PanelByLayoutSlot);
                }
                else if (step.Operation == DoctrineResearchOperation.Redefine && step.PanelId is null)
                {
                    if (rules.ApplicationRestricted is null) throw ResearchMissing("application_restriction");
                    if (rules.ApplicationRestricted == true || rules.Gold < rules.RedefineGold ||
                        DoctrineResearchSource.BookCost(rules.Baseline, working, rules.BookCostPerPanel) >= rules.Books ||
                        DoctrineResearchSource.ResearchCount(working, rules.Panels) == 0 ||
                        !rules.RedefineCapacityAvailable(working))
                        return CommandResult.Reject("The force cannot redefine its research now.", "research.redefine");
                    DoctrineResearchSource.Redefine(working, rules.Baseline, rules.Panels,
                        rules.RulerDoctrineId, rules.MatchingDiscount, rules.PanelByLayoutSlot);
                    redefined = true;
                    result = PublishDoctrineResearchWorkspace(result, force.Id, working);
                    result = ClampResourcesAfterDoctrineResearch(result, current, rules.Baseline, working, force.Id);
                }
                else return CommandResult.Reject("The research operation is invalid.", "research.operation");
            }
            if (ResearchWorkspaceEquals(rules.Baseline, working))
                return CommandResult.Reject("Research has no changes to commit.", "research.unchanged");
            int books = DoctrineResearchSource.BookCost(rules.Baseline, working, rules.BookCostPerPanel);
            if (rules.Books - CommandBookAvailability.Reserved(state, rules.DistrictId) < books)
                return CommandResult.Reject("There are insufficient command books.", "research.command_books");
            result = PublishDoctrineResearchWorkspace(result, force.Id, working);
            //6CEB10 formats its one ruler line before6C6390 publishes the
            //resource clamps, command books and optional redefinition charge.
            var report = FormatDoctrineResearchReport(result, current, 0x5c7f, rules.RulerId);
            result = report.State;
            var request = CreateDoctrineResearchPresentation(current, state, rules.RulerId, report);
            result = ClampResourcesAfterDoctrineResearch(result, current, rules.Baseline, working, force.Id);
            result = SpendDoctrineResearchBooks(result, current, rules.DistrictId, books);
            if (redefined)
            {
                var treasury = ReadPersonnelHireTreasuryCurrent(current, rules.DistrictId);
                result = BaseCurrencyAdditionSource.Add(result, current, treasury, -rules.RedefineGold,
                    reference => BasePostLoad.ReadResourceCapacities(reference, current).Gold).State;
            }
            long revision = result.NativeMapSeason?.AdvancedCalendarSteps ?? throw ResearchMissing("calendar_owner");
            var outbox = result.CampaignEffectRequests ?? throw ResearchMissing("presentation_owner");
            return CommandResult.Accept(result with { CampaignEffectRequests =
                outbox.Append(CampaignEffectSource.DoctrineResearchCommands, revision, [request]) });
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, error.Message); }
    }

    private static bool ResearchWorkspaceEquals(DoctrineResearchWorkspace first, DoctrineResearchWorkspace second)
        => first.Points.SequenceEqual(second.Points) && first.Researched.SetEquals(second.Researched) &&
            first.Pending.SetEquals(second.Pending) && first.Available.SetEquals(second.Available);

    private static GameState PublishDoctrineResearchWorkspace(GameState state, int forceId, DoctrineResearchWorkspace working)
    {
        var force = state.Forces.GetValueOrDefault(forceId) ?? throw ResearchMissing("publish_Force");
        var reserves = state.ForceCreditReserves ?? throw ResearchMissing("publish_reserves");
        return state with
        {
            Forces = new(state.Forces) { [forceId] = force with
                { PolicyEffects = new(working.Researched.ToArray(), working.Pending.ToArray()),
                  DoctrineResearch = new(working.Available.ToImmutableHashSet()) } },
            ForceCreditReserves = reserves with { PointsByForce = reserves.PointsByForce.SetItem(forceId, working.Points.ToImmutableArray()) }
        };
    }

    private static GameState SpendDoctrineResearchBooks(GameState state, SourceWorldContext current, int districtId, int amount)
    {
        var district = state.Districts.GetValueOrDefault(districtId) ?? throw ResearchMissing("payment_District");
        int before = district.OrdersRemaining ?? throw ResearchMissing("payment_District14");
        int after = Math.Clamp(unchecked(before - amount), 0, 200);
        var native = state.NativeForceUnregisterCurrent ?? throw ResearchMissing("payment_District_window_owner");
        if (!native.DistrictWindows10.TryGetValue(districtId, out var fields) || fields.Length < 5)
            throw ResearchMissing("payment_District_window");
        var window = fields.ToArray(); window[4] = (byte)after;
        current.SetDistrictRawByte(districtId, 0x14, (byte)after);
        return state with { Districts = new(state.Districts) { [districtId] = district with { OrdersRemaining = after } },
            NativeForceUnregisterCurrent = native with
                { DistrictWindows10 = new(native.DistrictWindows10) { [districtId] = window } } };
    }

    private GameState ClampResourcesAfterDoctrineResearch(GameState state, SourceWorldContext current,
        DoctrineResearchWorkspace committed, DoctrineResearchWorkspace working, int forceId)
    {
        _baseCapacityInputs.BindCurrent(state, current);
        //224FB0: losing the actual committed Gate-capacity panel clamps the
        //force's registered Gate3C. It does not alter City troops or wounds.
        if (committed.Researched.Contains(56) && !working.Researched.Contains(56))
            foreach (var target in current.ReadRegisteredBases() ?? throw ResearchMissing("Gate_registry"))
            {
                if (target is null) throw ResearchMissing("Gate_registry_pointer");
                if (!ResearchNeed(current.ReadBaseNativeValidity(target), "Gate_validity")) continue;
                int location = ResearchNeed(current.ReadBaseObjectLink(target), "Gate_location");
                if (location is < 52 or > 61) continue;
                if (ReadResearchBaseForce(current, target) != forceId) continue;
                int ready = ResearchNeed(current.ReadBaseResource(target, BaseResourceKind.Troops), "Gate_troops");
                int cap = BasePostLoad.ReadResourceCapacities(target, current).Troops;
                current.SetBaseResource(target, BaseResourceKind.Troops, Math.Clamp(ready, 0, cap));
                state = PublishCurrentBaseCombatQuantities(state, current, target, true, false, false);
            }
        //224E40: the corresponding construction-capacity loss visits actual
        //generic Objects of definition category2, retaining the native16 bit.
        if (committed.Researched.Contains(62) && !working.Researched.Contains(62))
            foreach (var obj in current.ReadRegisteredObjectReferences() ?? throw ResearchMissing("Object_registry"))
            {
                if (obj is null) throw ResearchMissing("Object_registry_pointer");
                if (!ResearchNeed(current.ReadObjectNativeValidity(obj), "Object_validity")) continue;
                int rawForce = ResearchNeed(current.ReadObjectForceSlot13(obj), "Object_Force");
                var force = current.ReadForceTableReference(rawForce < 52 ? rawForce : 0) ?? throw ResearchMissing("Object_Force_lookup");
                if (force.PhysicalSlot != forceId) continue;
                var record = current.ReadPhysicalObject(ResearchNeed(obj.PhysicalSlot, "Object_receiver")) ?? throw ResearchMissing("Object_record");
                var definition = current.ReadObjectDefinitionReference(record.RawField12 < 31 ? record.RawField12 : 0)
                    ?? throw ResearchMissing("Object_definition");
                if (!ResearchNeed(current.ReadObjectDefinitionValidity(definition), "Object_definition_validity") ||
                    ResearchNeed(current.ReadObjectDefinitionRaw2C(definition), "Object_category") != 2) continue;
                int maximum = ResearchNeed(current.ReadObjectDefinitionBaseDurability(definition), "Object_definition_capacity");
                int hp = ResearchNeed(current.ReadObjectRawField14(obj), "Object_HP");
                ushort flags = ResearchNeed(current.ReadMapObjectFields16(obj), "Object_flags16");
                int after = Math.Min(hp, (flags & 1) == 0 ? unchecked(maximum - 1) : maximum);
                current.WriteObjectPostLoadField14(obj, after);
                state = PublishCurrentObjectDurability(state, current, obj,
                    ResearchNeed(current.ReadObjectRawField14(obj), "Object_stored_HP"));
            }
        return state;
    }

    private static int ReadResearchBaseForce(SourceWorldContext current, BaseReference target)
    {
        int raw = ResearchNeed(current.ReadBaseForceSlot(target), "Base_Force");
        return ResearchNeed((current.ReadForceTableReference(raw < 52 ? raw : 0)
            ?? throw ResearchMissing("Base_Force_lookup")).PhysicalSlot, "Base_Force_receiver");
    }
}
