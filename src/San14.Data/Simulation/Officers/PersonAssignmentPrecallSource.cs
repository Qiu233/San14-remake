namespace San14.Data;

public sealed record PersonAssignmentPrecallWrite(string ReceiverKind, int PhysicalSlot,
    int NativeOffset, int Before, int After);
public sealed record PersonAssignmentPrecallReport(int NativeMessageCode, ushort PersonId,
    int? CityPhysical = null, int? AreaPhysical = null, int? TargetForcePhysical = null);
public sealed record PersonAssignmentPrecallResult(int? NativeReturn,
    IReadOnlyList<PersonAssignmentPrecallWrite> Writes,
    IReadOnlyList<PersonAssignmentPrecallReport> Reports, string? UnresolvedOwner);
public sealed record PersonAreaAssignmentRemovalResult(int? NativeReturn, int? PersonPhysical,
    int? CountBefore, int? CountAfter, int[]? ValuesBefore, int[]? ValuesAfter, string? UnresolvedOwner);
public sealed record PersonPrimaryDistrictPrecallResult(int? NativeReturn,
    IReadOnlyList<ArmyReturnGovernorPacket> GovernorUpdates, string? UnresolvedOwner);

/// <summary>The actual2E0FC0 call29C430(Person, null, null). Operates on the
/// caller's current world, including real City90/AreaB0 chains. Unknown
/// callbacks leave an operation-local prefix which the whole caller discards.
/// Native presentation is represented by explicit source message receipts.</summary>
public static class PersonAssignmentPrecallSource
{
    /// <summary>2E0FC0's captured old District call2E9140(RDX=0,R8=0).
    /// Reuses the actual owned primary/Gate/governor temporary-pool execution;
    /// other reselection branches retain the existing concrete unresolved owner.</summary>
    public static PersonPrimaryDistrictPrecallResult Apply2E9140PrimaryForCapture(
        SourceWorldContext current, DistrictReference capturedOldDistrict,
        IArmyReturnGovernorCurrentQueries queries)
    {
        try
        {
            if (!Need(current.ReadDistrictNativeValidity(capturedOldDistrict), "2E9140 old District validity")) return new(0, [], null);
            var pool = current.ReadOwnedCurrentListPool() ?? throw Missing("2E9140 current primary pool");
            var updates = ArmyReturnCommanderHomeUpdater.ApplyPrimaryOwned(capturedOldDistrict, current, queries);
            return new(1, updates, null);
        }
        catch (InvalidDataException unknown) { return new(null, [], unknown.Message); }
    }

    /// <summary>2E01E0(Area physical, RDX=0, old PersonID), including actual
    /// ID lookup, validity,2E7FD0 active-prefix removal and224D40 sorting.
    /// The independent current vector is never inferred from Area36.</summary>
    public static PersonAreaAssignmentRemovalResult Apply2E01E0AreaRemoval(
        SourceWorldContext current, int areaPhysical, ushort oldPersonId)
    {
        int? person = null; int? beforeCount = null; int[]? beforeValues = null;
        try
        {
            if (oldPersonId == 0) return new(1, null, null, null, null, null, null);
            if (oldPersonId < 6001)
                person = (current.ReadPersonLookupReference(oldPersonId) ?? throw Missing("2E01E0 old Person ID lookup")).PhysicalSlot;
            else
                person = (current.ReadTransferPersonTableReference(0) ?? throw Missing("2E01E0 old Person base pointer")).PhysicalSlot;
            if (!person.HasValue || !Need(current.ReadPersonNativeValidity(person.Value), "2E01E0 old Person validity"))
                return new(1, person, null, null, null, null, null);
            var fields = current.ReadAssignmentPersonAreaVector(person.Value) ?? throw Missing("Person1D0/1D8/1E8 current owner");
            beforeCount = fields.ActiveCount1E8; beforeValues = fields.Values.ToArray();
            int at = 0;
            if (fields.ActiveCount1E8 > 0)
                while (at < fields.ActiveCount1E8)
                {
                    if (at >= fields.Values.Length) throw Missing("Person1E8 prefix exceeds current1D0/1D8 vector");
                    if (fields.Values[at] == areaPhysical) break;
                    at++;
                }
            if (at == fields.ActiveCount1E8) return new(0, person, beforeCount, beforeCount, beforeValues, beforeValues.ToArray(), null);
            if (at >= fields.Values.Length) throw Missing("2E7FD0 current indexed vector store");
            fields.Values[at] = 0;
            int count = unchecked(fields.ActiveCount1E8 - 1);
            current.SetAssignmentPersonAreaVector(person.Value, fields with { ActiveCount1E8 = count });
            //220420 uses21FD00 for complete end-begin lengths<=32,
            //which includes the real fresh three-entry vector. Large changed
            //buffers reach its separate introsort branch rather than guessing.
            SortAreaVector(fields.Values);
            return new(1, person, beforeCount, count, beforeValues, fields.Values.ToArray(), null);
        }
        catch (InvalidDataException unknown) { return new(null, person, beforeCount, null, beforeValues, null, unknown.Message); }
    }

    internal static void SortAreaVector(int[] values)
    {
        if (values.Length > 32) throw Missing("branch.224D40_Person_area_vector_introsort_over32");
        for (int i = 1; i < values.Length; i++)
        {
            int value = values[i];
            //21FD00 compares with first before testing the reverse prefix.
            //This is observable even for a wrapped, non-transitive compare.
            if (CompareAreaValues(value, values[0]) < 0)
            {
                Array.Copy(values, 0, values, 1, i); values[0] = value; continue;
            }
            int at = i;
            while (at > 0 && CompareAreaValues(value, values[at - 1]) < 0)
            { values[at] = values[at - 1]; at--; }
            values[at] = value;
        }
    }
    private static int CompareAreaValues(int left, int right)
        => left == 0 ? right == 0 ? 0 : 1 : right == 0 ? -1 : unchecked(left - right);

    public static PersonAssignmentPrecallResult Apply29C430ForCapture(
        SourceWorldContext current, int personPhysical) =>
        Apply29C430(current, personPhysical, null, null);

    /// <summary>271320's actual changed-City call. The valid destination
    /// District/Force gates advisor and Force-association removal, while the
    /// changed destination still consumes real City90/AreaB0 appointments.</summary>
    public static PersonAssignmentPrecallResult Apply29C430ForHomeTransfer(
        SourceWorldContext current, int personPhysical,
        DistrictReference targetDistrict, BaseReference targetBase) =>
        Apply29C430(current, personPhysical, targetDistrict, targetBase);

    private static PersonAssignmentPrecallResult Apply29C430(
        SourceWorldContext current, int personPhysical,
        DistrictReference? targetDistrict, BaseReference? targetBase)
    {
        var writes = new List<PersonAssignmentPrecallWrite>();
        var reports = new List<PersonAssignmentPrecallReport>();
        try
        {
            var force = current.ReadPersonForce(personPhysical) ?? throw Missing("Person22CB40 Force");
            if (!Need(current.ReadForceNativeValidity(force), "Person Force validity")) return Result(0);
            int forcePhysical = Need(current.ReadForcePhysicalSlot(force), "Person Force physical");
            bool keepsForce = targetDistrict is not null && Need(current.ReadDistrictNativeValidity(targetDistrict), "29C430 target District validity") &&
                Need(current.ReadDistrictForceId(Need(targetDistrict.PhysicalSlot, "29C430 target District pointer")), "29C430 target District Force10") ==
                    Need(current.ReadPersonOldForceSlot(personPhysical), "29C430 Person22CF60 Force");
            bool changed = false;
            ushort advisor = Need(current.ReadAssignmentForceAdvisor52(forcePhysical), "Force52 current advisor");
            if (advisor == Id() && !keepsForce)
            {
                changed = true; current.WriteAssignmentForceAdvisor52(forcePhysical, 0);
                writes.Add(new("Force", forcePhysical, 0x52, advisor, 0));
                if (IsPlayer(force)) reports.Add(new(0x31FC, Id()));
                bool? callbacks = current.AssignmentAdvisorResetCallbacks?.Invoke(force);
                if (!callbacks.HasValue) throw Missing("branch.29C430_27BE40_27AA40_2FF040_advisor_reset");
                if (IsPlayer(force)) current.RawTransferServiceFlags16A8 = Need(current.RawTransferServiceFlags16A8, "Service16A8") | 0x80000;
            }

            bool retainsOldCity = targetBase is not null && Need(current.ReadBaseNativeValidity(targetBase), "29C430 target Base validity") &&
                Need(current.ReadBaseObjectLink(targetBase), "29C430 target Base4E") == Need(current.ReadPersonFirstLocation(personPhysical), "29C430 PersonE2");
            var cityNode = retainsOldCity ? new RegistryListNodeReference(null) : Head(RegistryKind.City);
            while (cityNode.Identity.HasValue)
            {
                var city = Payload(cityNode, RegistryKind.City);
                int cityPhysical = Need(city.PhysicalSlot, "City90 payload pointer");
                ushort ca = Need(current.ReadAssignmentCityCA(cityPhysical), "CityCA current appointment");
                if (ca == Id())
                {
                    changed = true; current.SetAssignmentCityCA(cityPhysical, 0);
                    writes.Add(new("City", cityPhysical, 0xCA, ca, 0));
                    if (IsPlayer(force)) reports.Add(new(0x31FD, Id(), cityPhysical));
                }
                ushort cc = Need(current.ReadAssignmentCityCC(cityPhysical), "CityCC current appointment");
                if (cc == Id())
                {
                    changed = true; current.SetAssignmentCityCC(cityPhysical, 0);
                    writes.Add(new("City", cityPhysical, 0xCC, cc, 0));
                    if (IsPlayer(force)) reports.Add(new(0x31FE, Id(), cityPhysical));
                }

                //223350's CCityAreaIterator scans actual managerB0 and only
                //filters physical City1..51 and the Area's current raw35.
                var areaNode = Head(RegistryKind.Area);
                while (areaNode.Identity.HasValue)
                {
                    var area = Payload(areaNode, RegistryKind.Area);
                    int areaPhysical = Need(area.PhysicalSlot, "AreaB0 payload pointer");
                    bool accepted = unchecked((uint)(cityPhysical - 1)) <= 50 &&
                        Need(current.ReadAreaCitySlot(new(areaPhysical)), "CityAreaIterator current Area35") == cityPhysical;
                    if (accepted)
                    {
                        ushort old = Need(current.ReadAssignmentArea36(areaPhysical), "Area36 current officer");
                        if (old == Id())
                        {
                            changed = true;
                            if (old != 0)
                            {
                                current.SetAssignmentArea36(areaPhysical, 0);
                                writes.Add(new("Area", areaPhysical, 0x36, old, 0));
                                //29C9FE passes OLD officer asR8D, despite the
                                //typed caller omitting that third parameter.
                                bool? removed = current.AssignmentAreaOfficerRemoval?.Invoke(areaPhysical, old);
                                if (!removed.HasValue) throw Missing("branch.29C430_2E01E0_old_officer_2E7FD0");
                            }
                            byte focus = Need(current.ReadAssignmentArea72(areaPhysical), "Area72 current focus");
                            current.SetAssignmentArea72(areaPhysical, 0); writes.Add(new("Area", areaPhysical, 0x72, focus, 0));
                            byte bonus = Need(current.ReadAssignmentArea6D(areaPhysical), "Area6D current bonus");
                            current.SetAssignmentArea6D(areaPhysical, 0); writes.Add(new("Area", areaPhysical, 0x6D, bonus, 0));
                            if (IsPlayer(force)) reports.Add(new(0x31FF, Id(), cityPhysical, areaPhysical));
                        }
                    }
                    //Keep live Next reads after source callbacks.
                    areaNode = current.ReadRegistryNodeNext(areaNode) ?? throw Missing("AreaB0 Next");
                }
                cityNode = current.ReadRegistryNodeNext(cityNode) ?? throw Missing("City90 Next");
            }
            //The null-target capture call passes2783B0 R8=0/R9=1;
            //a valid same-Force target District skips that actual call.
            if (!keepsForce)
            {
                var association = Apply2783B0ForCapture(current, force, personPhysical);
                writes.AddRange(association.Writes); reports.AddRange(association.Reports);
                if (association.NativeReturn is null) throw new InvalidDataException(association.UnresolvedOwner!);
                if (association.NativeReturn != 0) changed = true;
            }
            return Result(changed ? 1 : 0);

            ushort Id() => Need(current.ReadPersonIdentity(personPhysical), "current Person ID10");
            bool IsPlayer(ForceReference receiver) => IsCurrentPlayer(current, receiver);
            RegistryListNodeReference Head(RegistryKind kind)
            {
                var descriptor = current.ReadRegistryDescriptor(kind) ?? throw Missing($"manager {kind} descriptor");
                return current.ReadRegistryHead(descriptor) ?? throw Missing($"manager {kind} head");
            }
            RegistryObjectReference Payload(RegistryListNodeReference node, RegistryKind kind)
            {
                var payload = current.ReadRegistryNodePayload(node) ?? throw Missing($"manager {kind} node payload");
                return payload.Kind == kind ? payload : throw Missing($"manager {kind} payload type");
            }
        }
        catch (InvalidDataException unknown) { return new(null, writes, reports, unknown.Message); }
        PersonAssignmentPrecallResult Result(int value) => new(value, writes, reports, null);
    }

    /// <summary>2783B0's actualcapture-internal arguments R8=0/R9=1. First
    /// matching196 slot is final even when target is invalid; not transfer's
    /// mandatory native-report adapter and not a search for a later valid pair.</summary>
    public static PersonAssignmentPrecallResult Apply2783B0ForCapture(
        SourceWorldContext current, ForceReference force, int personPhysical) =>
        Apply2783B0(current, force, personPhysical, true);

    internal static PersonAssignmentPrecallResult Apply2783B0ForDraft(
        SourceWorldContext current, ForceReference force, int personPhysical) =>
        Apply2783B0(current, force, personPhysical, false);

    private static PersonAssignmentPrecallResult Apply2783B0(
        SourceWorldContext current, ForceReference force, int personPhysical, bool publish)
    {
        var writes = new List<PersonAssignmentPrecallWrite>();
        var reports = new List<PersonAssignmentPrecallReport>();
        try
        {
            if (!Need(current.ReadForceNativeValidity(force), "2783B0 Force validity") ||
                !Need(current.ReadPersonNativeValidity(personPhysical), "2783B0 Person validity")) return Result(0);
            ushort id = Need(current.ReadPersonIdentity(personPhysical), "2783B0 Person ID10");
            if (id == 0) return Result(0); //229930's explicit id0 sentinel.
            int found = -1;
            for (int i = 0; i < 3; i++)
                if (Need(current.ReadForceAssociationPersonId(force, i), $"Force196[{i}]") == id) { found = i; break; }
            if (found == -1) return Result(0);
            byte target = Need(current.ReadForceAssociationTargetSlot(force, found), $"Force19C[{found}]");
            var targetForce = current.ReadForceTableReference(target <= 51 ? target : 0) ?? throw Missing("2783B0 target Force pointer");
            if (!Need(current.ReadForceNativeValidity(targetForce), "2783B0 target Force validity")) return Result(0);
            int physical = Need(current.ReadForcePhysicalSlot(force), "2783B0 Force physical");
            current.WriteForceAssociationTargetSlot(force, found, 0);
            writes.Add(new("Force", physical, 0x19C + found, target, 0));
            current.WriteForceAssociationPersonId(force, found, 0);
            writes.Add(new("Force", physical, 0x196 + found * 2, id, 0));
            if (publish && IsCurrentPlayer(current, force))
                reports.Add(new(0x3452, Need(current.ReadPersonIdentity(personPhysical), "2783B0 report Person ID10"),
                    TargetForcePhysical: Need(current.ReadForcePhysicalSlot(targetForce), "2783B0 report target Force physical")));
            return Result(1);
        }
        catch (InvalidDataException unknown) { return new(null, writes, reports, unknown.Message); }
        PersonAssignmentPrecallResult Result(int value) => new(value, writes, reports, null);
    }
    private static bool IsCurrentPlayer(SourceWorldContext current, ForceReference force)
    {
        var player = current.ReadCurrentPlayerForce() ?? throw Missing("2338E0 current player Force pointer");
        int physical = Need(current.ReadForcePhysicalSlot(player), "2338E0 player Force physical");
        return physical != 0 && physical == Need(current.ReadForcePhysicalSlot(force), "2338E0 receiver Force physical");
    }
    private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("current.AssignmentPrecall: " + field);
}
