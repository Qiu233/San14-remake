using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed record BaseCapturePersonDecision(int PersonPhysical, int PersonId, byte StatusE6,
    bool CapturePredicateReached, PersonArmyCaptureReceipt? Capture, string Group);
public sealed record BaseCapturePersonPrefixReceipt(int Location4E, int OldForcePhysical, int WinnerForcePhysical,
    int[] HomeArmyOrder, int[] HomePersonOrder, BaseCapturePersonDecision[] Decisions,
    int[] OrdinaryCapturedOrder, int[] SpecialCapturedOrder, int[] NoncapturedOrder,
    ReturningPersonHomeReceipt[] HomeWrites, PersonArmyCaptureWholeReceipt[] CustodyWrites,
    HexListHandle CapturedTemporary, HexListHandle NoncapturedTemporary,
    HexListHandle SpecialTemporary, HexListHandle ChangedForceTemporary)
{
    public ArmyCause4Receipt[] HomeArmyCause4 { get; init; } = [];
    public bool HasCompleteDiagnosticHistory { get; init; } = true;
}
public sealed record BaseCapturePersonPrefixResult(GameState? State,
    BaseCapturePersonPrefixReceipt? Receipt, SourceWorldContext? Current, string? UnresolvedOwner);

/// <summary>2851B0's ordinary HomeArmy/Person grouping and noncaptured260310
/// before26DA10. Successful lists stay live for the enclosing capture's tail;
/// Release is called there, after the last source consumer. Unknown leaves no
/// public state; all reached writes and temporaries belonged to the private world.</summary>
public sealed class BaseCapturePersonPrefix(ArmyCampaignRuleAdapter package)
{
    public const string Revision = "ce-2851B0-ordinary-Base-Person-prefix-20261008";

    public BaseCapturePersonPrefixResult TryApply(GameState input, BaseReference target,
        ArmyUnitReference winnerArmy, NativeMapSeasonDate? operationThirdDate = null,
        TacticSourceSamplerWorld? sampler = null, CaptureApplicationOwners? application = null)
        => Apply(input, target, winnerArmy, operationThirdDate, sampler, application, null, null);

    internal BaseCapturePersonPrefixResult TryApplyCurrent(GameState input, SourceWorldContext current,
        BaseReference target, ArmyUnitReference winnerArmy, NativeMapSeasonDate? operationThirdDate,
        TacticSourceSamplerWorld? sampler, BaseCaptureOperationContext? continuationContext = null,
        DistrictReference? winnerDistrict = null)
        => Apply(input, target, winnerArmy, operationThirdDate, sampler, null, current, continuationContext, winnerDistrict);

    private BaseCapturePersonPrefixResult Apply(GameState input, BaseReference target,
        ArmyUnitReference winnerArmy, NativeMapSeasonDate? operationThirdDate,
        TacticSourceSamplerWorld? sampler, CaptureApplicationOwners? application,
        SourceWorldContext? operationCurrent, BaseCaptureOperationContext? continuationContext,
        DistrictReference? winnerDistrict = null)
    {
        bool borrowed = operationCurrent is not null;
        SourceWorldContext? current = null;
        HexListHandle? captured = null, noncaptured = null, special = null, changedForce = null;
        bool published = false;
        try
        {
            var world = operationCurrent is null ? package.CaptureOwnedArmyReturnWorld(input) :
                OwnedArmyReturnWorld.Borrow(input, operationCurrent, operationCurrent.ReadOwnedCurrentListPool()
                    ?? throw Missing("borrowed_current_pool"));
            current = world.Context;
            var restored = input.BaseCaptureContinuation;
            if (restored is { PendingCapturedRulerTreatment: not null } || input.PersonDeathContinuation is not null)
                throw Missing("Base_capture_player_decision_pending");
            continuationContext = restored?.Operation ?? continuationContext;
            if (restored is not null && continuationContext is null) throw Missing("Base_capture_resume_context");
            int link = K(current.ReadBaseObjectLink(target), "Base4E");
            if (link is < 1 or > 61 || !K(current.ReadBaseNativeValidity(target), "Base validity")) throw Missing("branch.invalid_Base");
            int oldForce = restored?.Operation.PreviousForceId ?? K(current.ReadBaseForceSlot(target), "Base B0");
            if (oldForce is < 1 or > 45) throw Missing("branch.nonordinary_old_Force");
            var oldForceReference = current.ReadForceTableReference(oldForce) ?? throw Missing("old Force pointer");
            if (restored is null && !K(current.ReadForceNativeValidity(oldForceReference), "old Force validity")) throw Missing("branch.invalid_old_Force");
            bool winnerArmyValid = K(current.ReadArmyUnitNativeValidity(winnerArmy), "winner Army validity");
            if (!winnerArmyValid && winnerDistrict is null) throw Missing("branch.invalid_winner_Army");
            var targetDistrict = winnerDistrict ?? current.ReadDistrictTableReference(K(current.ReadPersonDistrictSlot(
                Commander(current, winnerArmy)), "winner_DistrictE0")) ?? throw Missing("winner_District_pointer");
            var winningForce = winnerArmyValid ? current.ReadArmyUnitForceReference(winnerArmy)
                : current.ReadForceTableReference(K(current.ReadDistrictForceSlot(targetDistrict), "winner_District10"))
                    ?? throw Missing("winner_District_Force_pointer");
            int newForce = K(current.ReadForcePhysicalSlot(winningForce), "winner Force physical");
            if (newForce is < 1 or > 45 || newForce == oldForce || !K(current.ReadForceNativeValidity(winningForce), "winner Force validity"))
                throw Missing("branch.nonordinary_winner_Force");
            int winnerPerson = winnerArmyValid ? Commander(current, winnerArmy)
                : K(current.ReadPersonLookupReference(0)?.PhysicalSlot, "winner_Person0_pointer");
            if (winnerArmyValid && !K(current.ReadPersonNativeValidity(winnerPerson), "winner Commander validity")) throw Missing("branch.invalid_winner_Commander");

            var homeArmies = restored?.People.HomeArmySlots.ToList() ?? [];
            var homeArmyCause4 = new List<ArmyCause4Receipt>();
            var homes = restored?.People.HomeOfficerIds.ToList() ?? [];
            var decisions = new List<BaseCapturePersonDecision>();
            var working = input;
            NativeMapSeasonDate? sourceDate = operationThirdDate ?? input.NativeMapSeason?.ThirdDate;
            var sourceSampler = continuationContext is { SampleDay: int day, SampleSalt: int salt }
                ? new TacticSourceSamplerWorld(day, salt) : sampler;
            if (restored is null)
            {
                var capturePresentation = new List<CampaignEffectRequest>();
                package.AppendBaseCapturePresentationCurrent(working, current, target, targetDistrict, capturePresentation);
                working = working with { CampaignEffectRequests = (working.CampaignEffectRequests
                    ?? throw Missing("capture_presentation_owner")).Append(CampaignEffectSource.ArmyBattleDefeat,
                        working.NativeClock?.UpdateOrdinal ?? throw Missing("capture_presentation_clock"), capturePresentation) };
                working = package.QueueBaseCaptureInitialArmySpeechesCurrent(working, current,
                    target, winnerArmy, sourceDate);
                foreach (var item in current.ReadRegistryReferences(RegistryKind.Army) ?? throw Missing("manager60"))
                {
                    var entry = item ?? throw Missing("manager60 payload");
                    if (entry.Kind != RegistryKind.Army || entry.PhysicalSlot is not int armySlot) throw Missing("manager60 Army pointer");
                    if (K(current.ReadOwnedArmyExclusion233560(entry), "233560 HomeArmy exclusion")) continue;
                    var army = new ArmyUnitReference(armySlot);
                    int commander = Commander(current, army);
                    ushort home = K(current.ReadPersonNativeValidity(commander), "22A7A0 Commander validity")
                        ? K(current.ReadPersonFirstLocation(commander), "22A7A0 CommanderE2") : (ushort)0;
                    if (home != link) continue;
                    homeArmies.Add(armySlot);
                    //222F20/334660 uses manager60's current chain, with no
                    //temporary allocation. Cause4 only appends pending70;26 has
                    //not removed these manager nodes while this loop runs.
                    if (current.IsArmyUnitMapWriteSkipped(army))
                    {
                        working = WithPool(working, current, borrowed);
                        var homeFeedback = new List<CampaignEffectRequest>();
                        var destruction = new OwnedArmyDestructionStage(package);
                        var homePool = current.ReadOwnedCurrentListPool() ?? throw Missing("HomeArmy current pool");
                        var retired = winnerArmyValid
                            ? destruction.ApplyEmployedHomeCommanderCause4Current(working, current, homePool,
                                armySlot, winnerArmy, sourceDate)
                            : destruction.ApplyAbsorptionCommanderCause4Current(working, current, homePool,
                                armySlot, sourceDate ?? throw Missing("HomeArmy_null_winner_date"), homeFeedback);
                        working = retired.State ?? throw Missing(retired.UnresolvedOwner ?? "HomeArmy_259360_mode0_cause4");
                        AppendFeedback(homeFeedback);
                        homeArmyCause4.Add(retired.Receipt!);
                        if (!borrowed) current = package.CaptureOwnedArmyReturnWorld(working).Context;
                    }
                }

                //This descriptor starts as230DD0's status6/E4 Base list, is
                //cleared (not destroyed) and reused for ordinarycaptured people.
                captured = Temporary(current); current.ClearTemporaryHexList(captured);
                foreach (int person in RegisteredPeople(current))
                {
                    if (K(current.ReadPersonStatus(person), "230DD0 PersonE6") != 6) continue;
                    if (K(current.ReadPersonSecondLocation(person), "230DD0 PersonE4") != link) continue;
                    _ = current.AppendPersonReturnTemporary(captured, person);
                }
                var existingCaptiveEffects = new List<CampaignEffectRequest>();
                foreach (var item in ReadPeople(current, captured))
                    working = package.ApplyBaseCaptureExistingCaptiveCurrent(working, current,
                        current.ReadOwnedCurrentListPool() ?? throw Missing("existing_captive_current_pool"),
                        target, item.Person, sourceDate, existingCaptiveEffects, winningForce: winningForce,
                        winningDistrict: targetDistrict);
                if (existingCaptiveEffects.Count != 0)
                    working = working with { CampaignEffectRequests = (working.CampaignEffectRequests
                        ?? throw Missing("existing_captive_effect_owner")).Append(CampaignEffectSource.ArmyBattleDefeat,
                            working.NativeClock?.UpdateOrdinal ?? throw Missing("existing_captive_effect_clock"), existingCaptiveEffects) };
                current.ClearTemporaryHexList(captured);
                noncaptured = Temporary(current); special = Temporary(current);
                sourceSampler ??= package.ReadArmyDestructionSampler(working, current);
                foreach (int person in RegisteredPeople(current))
                {
                    //3346B0 first testsE2 equality, then232F10(mode0).
                    if (K(current.ReadPersonFirstLocation(person), "3346B0 PersonE2") != link) continue;
                    byte status = K(current.ReadPersonStatus(person), "3346B0 PersonE6");
                    if (status is < 1 or > 4) continue;
                    homes.Add(K(current.ReadPersonIdentity(person), "Home Person10"));
                    var personForce = current.ReadPersonForce(person) ?? throw Missing("22CF60 Person Force");
                    if (K(current.ReadForcePhysicalSlot(personForce), "22CF60 Person Force physical") == newForce) continue;
                    bool reached = K(current.ReadPersonSecondLocation(person), "Home PersonE4") == link;
                    PersonArmyCaptureReceipt? captureReceipt = null;
                    bool wasCaptured = false;
                    if (reached)
                    {
                        var captureResult = PersonArmyCaptureSource.Evaluate(working, current, person, new(winnerPerson),
                            package.CreateArmyDestructionAbilities(current), sourceDate, sourceSampler, application ?? new(false));
                        wasCaptured = captureResult.Captured ?? throw Missing(captureResult.UnresolvedOwner ?? "27E870 capture predicate");
                        captureReceipt = captureResult.Receipt;
                    }
                    string group;
                    if (wasCaptured)
                    {
                        bool ordinaryPerson = K(OfficerBehaviorRules.UsesOrdinaryPersonRules(current.ReadPersonBehavior(person)), "captured Person15E");
                        //233780(Person,5) is a status equality, not a callback.
                        bool ordinary = ordinaryPerson || status == 5;
                        group = ordinary ? "captured" : "special-captured";
                        _ = current.AppendPersonReturnTemporary(ordinary ? captured : special, person);
                    }
                    else
                    {
                        group = "noncaptured";
                        _ = current.AppendPersonReturnTemporary(noncaptured, person);
                        _ = status == 1; //The reached233780(Person,1) has no writer.
                    }
                    decisions.Add(new(person, K(current.ReadPersonIdentity(person), "decision Person10"), status, reached, captureReceipt, group));
                }

            }
            else
            {
                captured = RestorePeople(restored.People.CapturedOfficerIds);
                special = RestorePeople(restored.People.SpecialCapturedOfficerIds);
                noncaptured = RestorePeople(restored.People.NoncapturedOfficerIds);
                //The selected node has settled through the formal decision or
                //death continuation. Neither classification nor its call reruns.
                working = working with { BaseCaptureContinuation = null };
            }

            //The source list is sorted once, before the first captured call.
            var custody = new List<PersonArmyCaptureWholeReceipt>();
            working = WithPool(working, current, borrowed);
            var capturedNodes = ReadPeople(current, captured);
            if (restored is null && capturedNodes.Count != 0)
                SortStatusAndIdentity(current, captured!, capturedNodes, rulerLast: true);
            var capturedOrder = ReadPeople(current, captured!).Select(item =>
                (int)K(current.ReadPersonIdentity(item.Person), "captured Person10")).ToArray();
            var specialOrder = ReadPeople(current, special!).Select(item =>
                (int)K(current.ReadPersonIdentity(item.Person), "special Person10")).ToArray();
            var pendingNoncapturedOrder = ReadPeople(current, noncaptured!).Select(item =>
                (int)K(current.ReadPersonIdentity(item.Person), "noncaptured Person10")).ToArray();
            int firstCaptured = restored is null ? 0 : restored.People.CapturedIndex + 1;
            var orderedCaptured = ReadPeople(current, captured!);
            for (int index = firstCaptured; index < orderedCaptured.Count; index++)
            {
                var item = orderedCaptured[index];
                var branch = package.ReadRulerCaptureBranchCurrent(working, current, item.Person,
                    winningForce, sourceSampler);
                if (branch.Branch == RulerCaptureBranch.DispositionRequired)
                {
                    var decision = branch.Decision ?? throw Missing("ruler_decision_owner");
                    if (decision.Decision is null) throw Missing(decision.UnresolvedOwner ?? "ruler_decision");
                    var effects = new List<CampaignEffectRequest>();
                    if (decision.Decision == RulerCaptureDecision.PlayerDecisionRequired)
                    {
                        var prompt = package.PrepareCapturedRulerTreatmentCurrent(working, current,
                            decision.Receipt ?? throw Missing("ruler_decision_receipt"));
                        working = prompt.State;
                        return Pause(index, prompt.Request, null);
                    }
                    CapturedRulerDisposition chosen;
                    if (decision.Decision == RulerCaptureDecision.Release)
                    {
                        var released = package.ApplyRulerCaptureReleaseCurrent(working, current, world.Pool,
                            decision, sourceDate ?? throw Missing("ruler_release_date"), effects);
                        working = released.State ?? throw Missing(released.UnresolvedOwner ?? "ruler_release");
                        chosen = CapturedRulerDisposition.Release;
                    }
                    else
                    {
                        working = package.ApplyRulerCaptureExecutionCurrent(working, current, world.Pool,
                            decision, sourceDate ?? throw Missing("ruler_execution_date"), effects);
                        chosen = CapturedRulerDisposition.Execute;
                    }
                    AppendFeedback(effects);
                    if (working.PersonDeathContinuation is not null) return Pause(index, null, chosen);
                    continue;
                }
                var result = package.ApplyOrdinaryWinnerArmyCapture(working, current,
                    current.ReadOwnedCurrentListPool() ?? throw Missing("current primary pool"),
                    item.Person, winnerArmy, operationThirdDate,
                    allowLastCityRuler: branch.Branch == RulerCaptureBranch.LastCityRuler);
                working = result.State ?? throw Missing(result.UnresolvedOwner ?? "2E0FC0 captured callback");
                custody.Add(result.Receipt!);
            }
            if (K(current.ReadTemporaryHexListCount(special), "special captured count") != 0)
                throw Missing("branch.special_captured_callback");

            //The originalTaskScene callback is separate from27E870's early
            //task gate. This independent client supplies an actual absent
            //presentation owner; present source TaskScene remains separate.
            if (K((application ?? new(false)).TaskScenePresent, "1D3840 TaskScene owner"))
                throw Missing("branch.Base_capture_TaskScene_callback");
            //2851B0 then allocates its actual changed-Force filter list and
            //removes those pointers from the live noncaptured output.
            changedForce = Temporary(current);
            foreach (var item in ReadPeople(current, noncaptured))
            {
                int personForce = K(current.ReadPersonForce(item.Person)?.PhysicalSlot, "noncaptured current Force");
                if (personForce != oldForce) _ = current.AppendPersonReturnTemporary(changedForce, item.Person);
            }
            foreach (var item in ReadPeople(current, changedForce))
                current.RemoveCapturePersonTemporary(noncaptured, item.Person);
            var noncapturedNodes = ReadPeople(current, noncaptured);
            if (noncapturedNodes.Count != 0) SortStatusAndIdentity(current, noncaptured, noncapturedNodes);
            var noncapturedOrder = ReadPeople(current, noncaptured).Select(x => (int)K(current.ReadPersonIdentity(x.Person), "noncaptured Person10")).ToArray();
            bool lastCity = false;
            if (noncapturedOrder.Length != 0)
            {
                var ownedCities = ReadOwnedCityLocations(current, oldForce);
                lastCity = ownedCities.Length == 0 || ownedCities is [var only] && only == link;
            }
            var homeWrites = new List<ReturningPersonHomeReceipt>();
            var noncapturedFeedback = new List<CampaignEffectRequest>();
            if (lastCity) working = package.ApplyLastCitySpecialPersonnelReturnCurrent(working, current,
                current.ReadOwnedCurrentListPool() ?? throw Missing("special_recall_current_pool"),
                target, oldForce, sourceDate, noncapturedFeedback);
            TacticSourceSamplerWorld ReadSampler() => sourceSampler ?? package.ReadArmyDestructionSampler(working, current)
                ?? throw Missing("last_City_World450_45C");
            var noncapturedNode = current.ReadRegistryHead(new(noncaptured.Identity)) ?? throw Missing("noncaptured head");
            while (noncapturedNode.Identity.HasValue)
            {
                var entry = current.ReadRegistryNodePayload(noncapturedNode) ?? throw Missing("noncaptured payload");
                if (entry is not { Kind: RegistryKind.Person, PhysicalSlot: int person }) throw Missing("noncaptured Person pointer");
                byte status = K(current.ReadPersonStatus(person), "232460 current PersonE6");
                if (status is >= 1 and <= 4)
                {
                    working = WithPool(working, current, borrowed);
                    if (lastCity)
                    {
                        working = package.ApplyLastCityNoncapturedPersonCurrent(working, current,
                            current.ReadOwnedCurrentListPool() ?? throw Missing("last_City_current_pool"), person,
                            winningForce, sourceDate, ReadSampler, noncapturedFeedback);
                    }
                    else
                    {
                        int id = K(current.ReadPersonIdentity(person), "noncaptured Person10");
                        var rules = package.CreateCaptureReturningPersonRules();
                        var result = borrowed
                            ? rules.TryApplyHomeCurrent(working, current, link, oldForce, id, sourceDate)
                            : rules.TryApplyIdleBase(working, link, oldForce, id, sourceDate);
                        working = result.State ?? throw Missing(result.UnresolvedOwner ?? "260310 noncaptured Home");
                        homeWrites.Add(result.Receipt!);
                        //The standalone Home operation adopts its actual
                        //current, retaining the still-live caller lists.
                        if (!borrowed) current = package.CaptureOwnedArmyReturnWorld(working).Context;
                    }
                }
                noncapturedNode = current.ReadRegistryNodeNext(noncapturedNode) ?? throw Missing("noncaptured next");
            }
            if (noncapturedFeedback.Count != 0)
                working = working with { CampaignEffectRequests = (working.CampaignEffectRequests
                    ?? throw Missing("last_City_feedback_owner")).Append(CampaignEffectSource.ArmyBattleDefeat,
                        working.NativeClock?.UpdateOrdinal ?? throw Missing("last_City_feedback_clock"), noncapturedFeedback) };
            var receipt = new BaseCapturePersonPrefixReceipt(link, oldForce, newForce, homeArmies.ToArray(), homes.ToArray(),
                decisions.ToArray(), capturedOrder, specialOrder,
                noncapturedOrder, homeWrites.ToArray(), custody.ToArray(), captured, noncaptured, special, changedForce)
                { HomeArmyCause4 = homeArmyCause4.ToArray(), HasCompleteDiagnosticHistory = restored is null };
            working = WithPool(working, current, borrowed);
            published = true;
            return new(working, receipt, current, null);

            HexListHandle RestorePeople(ImmutableArray<int> identities)
            {
                var list = Temporary(current);
                try
                {
                    foreach (int id in identities)
                    {
                        int person = K(current.ReadPersonLookupReference(id)?.PhysicalSlot, "resume_Person_lookup");
                        if (!current.AppendPersonReturnTemporary(list, person))
                            throw Missing("resume_Person_node_allocation");
                    }
                    return list;
                }
                catch { Release(current, list); throw; }
            }
            void AppendFeedback(List<CampaignEffectRequest> feedback)
            {
                if (feedback.Count == 0) return;
                working = working with { CampaignEffectRequests = (working.CampaignEffectRequests
                    ?? throw Missing("ruler_feedback_owner")).Append(CampaignEffectSource.ArmyBattleDefeat,
                        working.NativeClock?.UpdateOrdinal ?? throw Missing("ruler_feedback_clock"), feedback) };
            }
            BaseCapturePersonPrefixResult Pause(int index, CapturedRulerTreatmentRequest? pending,
                CapturedRulerDisposition? chosen)
            {
                var context = continuationContext ?? throw Missing("ruler_pause_operation_context");
                if (sourceSampler is { } sample)
                    context = context with { SampleDay = sample.Day450, SampleSalt = sample.Salt45C };
                //No temporary handle crosses a save boundary. The ordered
                //identities retain source list payloads, including aliases.
                var people = new BaseCapturePeopleContinuation(homeArmies.ToImmutableArray(), homes.ToImmutableArray(),
                    capturedOrder.ToImmutableArray(), specialOrder.ToImmutableArray(),
                    pendingNoncapturedOrder.ToImmutableArray(), index);
                working = working with { BaseCaptureContinuation = new(-1, context, people)
                    { PendingCapturedRulerTreatment = pending, ChosenDisposition = chosen } };
                if (changedForce is not null) { Release(current, changedForce); changedForce = null; }
                if (special is not null) { Release(current, special); special = null; }
                if (noncaptured is not null) { Release(current, noncaptured); noncaptured = null; }
                if (captured is not null) { Release(current, captured); captured = null; }
                working = WithPool(working, current, borrowed);
                published = true;
                return new(working, null, current, null);
            }
        }
        catch (InvalidDataException error) { return new(null, null, null, error.Message); }
        finally
        {
            if (!published && current is not null)
            {
                if (changedForce is not null) Release(current, changedForce);
                if (special is not null) Release(current, special);
                if (noncaptured is not null) Release(current, noncaptured);
                if (captured is not null) Release(current, captured);
            }
        }
    }

    public static GameState Release(GameState state, SourceWorldContext current,
        BaseCapturePersonPrefixReceipt receipt) => ReleaseWithPool(state, current, receipt, false);

    internal static GameState ReleaseWithinCapture(GameState state, SourceWorldContext current,
        BaseCapturePersonPrefixReceipt receipt) => ReleaseWithPool(state, current, receipt, true);

    private static GameState ReleaseWithPool(GameState state, SourceWorldContext current,
        BaseCapturePersonPrefixReceipt receipt, bool projectOwnedRoles)
    {
        Release(current, receipt.ChangedForceTemporary); Release(current, receipt.SpecialTemporary);
        Release(current, receipt.NoncapturedTemporary); Release(current, receipt.CapturedTemporary);
        return WithPool(state, current, projectOwnedRoles);
    }

    private static List<PersonNode> ReadPeople(SourceWorldContext current, HexListHandle list)
    {
        var result = new List<PersonNode>();
        var node = current.ReadHexListHead(list) ?? throw Missing("Person temporary head");
        while (node.Identity is not null)
        {
            int physical = K(current.ReadPersonRelationListPayload(node)?.PhysicalSlot, "Person temporary payload");
            result.Add(new(node, physical));
            node = current.ReadHexListNodeNext(node) ?? throw Missing("Person temporary next");
        }
        return result;
    }
    private static void SortStatusAndIdentity(SourceWorldContext current, HexListHandle list, List<PersonNode> nodes,
        bool rulerLast = false)
    {
        //24E760 sortsstatus/ID; captured24E5F0 putsstatus1 last and otherwise
        //sortsID. Relink actual nodes, preserving
        //equal-key order and the native payload/free-node image.
        for (int i = 1; i < nodes.Count; i++)
        {
            var item = nodes[i]; int at = i;
            byte status = K(current.ReadPersonStatus(item.Person), "sort PersonE6");
            ushort id = K(current.ReadPersonIdentity(item.Person), "sort Person10");
            while (at > 0)
            {
                byte previousStatus = K(current.ReadPersonStatus(nodes[at - 1].Person), "sort prior PersonE6");
                bool before = rulerLast && (status == 1) != (previousStatus == 1)
                    ? status != 1
                    : !rulerLast && status != previousStatus ? status < previousStatus
                    : id < K(current.ReadPersonIdentity(nodes[at - 1].Person), "sort prior Person10");
                if (!before) break;
                nodes[at] = nodes[at - 1]; at--;
            }
            nodes[at] = item;
        }
        current.ReorderCurrentList(list, nodes.Select(row => row.Node).ToArray());
    }
    private static int[] ReadOwnedCityLocations(SourceWorldContext current, int force)
    {
        var result = new List<int>();
        for (int i = 0; i < 52; i++)
        {
            var city = current.ReadCityTableReference(i) ?? throw Missing("Force City pointertable");
            if (K(current.ReadBaseForceSlot(city), "Force City B0") == force)
                result.Add(K(current.ReadBaseObjectLink(city), "Force City4E"));
        }
        return result.ToArray();
    }
    private static IEnumerable<int> RegisteredPeople(SourceWorldContext current)
    {
        foreach (var item in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("manager100"))
            yield return K(item, "manager100 Person pointer");
    }
    private static int Commander(SourceWorldContext current, ArmyUnitReference army)
    {
        ushort id = K(current.ReadArmyUnitCommanderId(army), "Army Commander12");
        return K((id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Commander pointer");
    }
    private static HexListHandle Temporary(SourceWorldContext current) =>
        current.CreateTemporaryHexList() ?? throw Missing("Person temporary allocator");
    private static void Release(SourceWorldContext current, HexListHandle list)
    { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
    private static GameState WithPool(GameState state, SourceWorldContext current, bool projectOwnedRoles = false)
    {
        var pool = current.ReadOwnedCurrentListPool() ?? throw Missing("current primary pool");
        return state.WithArmyLifecycle(state.ReadArmyLifecycle()! with
            { Indexes = projectOwnedRoles ? pool.ProjectOwnedRoles() : pool.ExportOwnedState() });
    }
    private static T K<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
    private static InvalidDataException Missing(string owner) => new("current.BaseCapturePersonPrefix: " + owner);
    private sealed record PersonNode(HexListNodeReference Node, int Person);
}

public sealed partial class SourceWorldContext
{
    /// <summary>2851B0's pointer-equal removal from its actual noncaptured
    /// list. Reuses the existing0188F0/018920 normal owned unlink writer and
    /// retains the removed node payload, with the source outer lock lifetime.</summary>
    internal void RemoveCapturePersonTemporary(HexListHandle list, int person)
    {
        ConditionalRegistryEnter();
        try
        {
            int key = RegistryKey(new(list.Identity));
            var node = ReadHexListHead(list) ?? throw new InvalidDataException("Current capture Person head is unknown.");
            while (node.Identity is not null)
            {
                var next = ReadHexListNodeNext(node) ?? throw new InvalidDataException("Current capture Person next is unknown.");
                if (ReadPersonRelationListPayload(node)?.PhysicalSlot == person)
                    RemoveAddressableRegistryNode(key, new(node.Identity));
                node = next;
            }
        }
        finally { ConditionalRegistryExit(); }
    }
}
