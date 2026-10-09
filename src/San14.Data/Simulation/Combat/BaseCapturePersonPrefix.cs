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
    {
        SourceWorldContext? current = null;
        HexListHandle? captured = null, noncaptured = null, special = null, changedForce = null;
        bool published = false;
        try
        {
            var world = package.CaptureOwnedArmyReturnWorld(input); current = world.Context;
            int link = K(current.ReadBaseObjectLink(target), "Base4E");
            if (link is < 1 or > 61 || !K(current.ReadBaseNativeValidity(target), "Base validity")) throw Missing("branch.invalid_Base");
            int oldForce = K(current.ReadBaseForceSlot(target), "Base B0");
            if (oldForce is < 1 or > 45) throw Missing("branch.nonordinary_old_Force");
            var oldForceReference = current.ReadForceTableReference(oldForce) ?? throw Missing("old Force pointer");
            if (!K(current.ReadForceNativeValidity(oldForceReference), "old Force validity")) throw Missing("branch.invalid_old_Force");
            if (!K(current.ReadArmyUnitNativeValidity(winnerArmy), "winner Army validity")) throw Missing("branch.invalid_winner_Army");
            var winningForce = current.ReadArmyUnitForceReference(winnerArmy);
            int newForce = K(current.ReadForcePhysicalSlot(winningForce), "winner Force physical");
            if (newForce is < 1 or > 45 || newForce == oldForce || !K(current.ReadForceNativeValidity(winningForce), "winner Force validity"))
                throw Missing("branch.nonordinary_winner_Force");
            int winnerPerson = Commander(current, winnerArmy);
            if (!K(current.ReadPersonNativeValidity(winnerPerson), "winner Commander validity")) throw Missing("branch.invalid_winner_Commander");

            var homeArmies = new List<int>(); var homeArmyCause4 = new List<ArmyCause4Receipt>();
            var working = input;
            NativeMapSeasonDate? sourceDate = operationThirdDate ?? input.NativeMapSeason?.ThirdDate;
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
                    working = WithPool(working, current);
                    var retired = new OwnedArmyDestructionStage(package).ApplyEmployedHomeCommanderCause4Current(
                        working, current, current.ReadOwnedCurrentListPool() ?? throw Missing("HomeArmy current pool"),
                        armySlot, winnerArmy, sourceDate);
                    working = retired.State ?? throw Missing(retired.UnresolvedOwner ?? "HomeArmy_259360_mode0_cause4");
                    homeArmyCause4.Add(retired.Receipt!);
                    current = package.CaptureOwnedArmyReturnWorld(working).Context;
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
            if (K(current.ReadTemporaryHexListCount(captured), "230DD0 output count") != 0)
                throw Missing("branch.Base_status6_existing_captives");
            current.ClearTemporaryHexList(captured);
            noncaptured = Temporary(current); special = Temporary(current);
            var decisions = new List<BaseCapturePersonDecision>();
            var homes = new List<int>();
            var sourceSampler = sampler ?? package.ReadArmyDestructionSampler(working, current);
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
                    ushort flags = K(current.ReadArmyReturnPersonFlags196(person), "captured Person15E");
                    //233780(Person,5) is a status equality, not a callback.
                    bool ordinary = (flags & 0x4000) == 0 || status == 5;
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

            //The existing whole2E0FC0 source is genuinely reusable for its
            //ordinarystatus3/4 scope. Special callbacks stay unknown;
            //capturetrue is never relabeled asnoncaptured.
            var custody = new List<PersonArmyCaptureWholeReceipt>();
            working = WithPool(working, current);
            var capturedNodes = ReadPeople(current, captured);
            if (capturedNodes.Count != 0) SortStatusAndIdentity(current, captured, capturedNodes, rulerLast: true);
            foreach (var item in ReadPeople(current, captured))
            {
                var result = package.ApplyOrdinaryWinnerArmyCapture(working, current,
                    current.ReadOwnedCurrentListPool() ?? throw Missing("current primary pool"), item.Person, winnerArmy);
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
            if (noncapturedOrder.Length != 0)
            {
                var ownedCities = ReadOwnedCityLocations(current, oldForce);
                if (ownedCities.Length == 0 || ownedCities is [var only] && only == link)
                    throw Missing("branch.last_City_noncaptured_2EACB0_265250");
            }
            var homeWrites = new List<ReturningPersonHomeReceipt>();
            foreach (int id in noncapturedOrder)
            {
                int person = current.ReadPersonLookupReference(id)?.PhysicalSlot ?? throw Missing("noncaptured Person pointer");
                byte status = K(current.ReadPersonStatus(person), "232460 current PersonE6");
                if (status is < 1 or > 4) continue;
                working = WithPool(working, current);
                var result = package.CreateCaptureReturningPersonRules().TryApplyIdleBase(working, link, oldForce, id, sourceDate);
                working = result.State ?? throw Missing(result.UnresolvedOwner ?? "260310 noncaptured Home");
                homeWrites.Add(result.Receipt!);
                //The Home operation adopted actual current and includes all
                //still-live caller temporaries. Continue from that owner.
                current = package.CaptureOwnedArmyReturnWorld(working).Context;
            }
            var receipt = new BaseCapturePersonPrefixReceipt(link, oldForce, newForce, homeArmies.ToArray(), homes.ToArray(),
                decisions.ToArray(), capturedNodes.Select(x => (int)K(current.ReadPersonIdentity(x.Person), "captured Person10")).ToArray(),
                ReadPeople(current, special).Select(x => (int)K(current.ReadPersonIdentity(x.Person), "special Person10")).ToArray(),
                noncapturedOrder, homeWrites.ToArray(), custody.ToArray(), captured, noncaptured, special, changedForce)
                { HomeArmyCause4 = homeArmyCause4.ToArray() };
            working = WithPool(working, current);
            published = true;
            return new(working, receipt, current, null);
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
        BaseCapturePersonPrefixReceipt receipt)
    {
        Release(current, receipt.ChangedForceTemporary); Release(current, receipt.SpecialTemporary);
        Release(current, receipt.NoncapturedTemporary); Release(current, receipt.CapturedTemporary);
        return WithPool(state, current);
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
    private static GameState WithPool(GameState state, SourceWorldContext current) => state with
    { NativeArmyLifecycle = state.NativeArmyLifecycle! with { Indexes = (current.ReadOwnedCurrentListPool() ?? throw Missing("current primary pool")).ExportOwnedState() } };
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
