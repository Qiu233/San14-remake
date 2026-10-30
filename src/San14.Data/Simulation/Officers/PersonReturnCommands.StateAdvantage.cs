using San14.Core;

namespace San14.Data;

public static partial class PersonReturnCommands
{
    /// <summary>266D60 refreshes the first associated command only when
    /// this serving force-person is its last valid member. Preserve registry
    /// and member order, including duplicates and mixed-force groups.</summary>
    internal static void RefreshStateAdvantageJourneys(GameState state, SourceWorldContext current,
        ForceReference force)
    {
        if (!Get(current.ReadForceNativeValidity(force), "State journey Force validity")) return;
        int forceId = Get(current.ReadForcePhysicalSlot(force), "State journey Force identity");
        foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("State journey Person registry"))
        {
            int person = Get(entry, "State journey registered Person");
            if (Get(current.ReadPersonOldForceSlot(person), "State journey Person Force code22CF60") != forceId ||
                Get(current.ReadPersonStatus(person), "State journey PersonE6") is not (>= 1 and <= 4)) continue;
            var linked = current.ReadPersonLinkedEntity(person) ?? throw Missing("State journey first association");
            if (!Get(current.ReadLinkedEntityNativeValidity(linked), "State journey association validity")) continue;
            var members = current.ReadLinkedEntityMemberIds(linked) ?? throw Missing("State journey member chain");
            int? last = null;
            foreach (uint? member in members)
            {
                int physical = Member(Get(member, "State journey member ID"));
                if (Get(current.ReadPersonNativeValidity(physical), "State journey member validity")) last = physical;
            }
            if (last != person) continue;
            int id = Get(linked.Identity, "State journey command identity");
            Refresh(id, members);
        }

        int Member(uint id) => Get((id < 6001 ? current.ReadPersonLookupReference((int)id) :
            current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "State journey member pointer");

        void Refresh(int id, IReadOnlyList<uint?> members)
        {
            var owner = CurrentCommandOwner(state, current);
            var today = state.NativeMapSeason?.ThirdDate ?? throw Missing("State journey World34 date");
            int now = DateOrdinal(today);
            if (members.Count == 0) throw Missing("State journey first member");
            int first = Member(Get(members[0], "State journey first member ID"));
            ForceReference commandForce = Get(current.ReadPersonNativeValidity(first), "State journey first member validity")
                ? current.ReadPersonForce(first) ?? throw Missing("State journey command Force")
                : current.ReadForceTableReference(0) ?? throw Missing("State journey null Force");

            if (owner.Caravans?.GetValueOrDefault(id) is { } caravan)
            {
                int remaining = Remaining(caravan.StartDate, caravan.DueDate, false);
                int advantage = Get(PersonReturnPathSource.ReadForceAdvantage(state, current, commandForce, 5).Value,
                    "State caravan current advantage");
                int reduction = ArmyCampaignRuleAdapter.ForeignTradeParameter(state, "return-state-advantage-reduction");
                int days;
                if (advantage == 0) days = unchecked(remaining + reduction);
                else
                {
                    if (remaining < unchecked(reduction + 1)) return;
                    days = Math.Max(reduction, unchecked(remaining - reduction));
                }
                var due = Adjust(caravan.DueDate, remaining, days);
                current.SetOwnedPersonCommandManager(owner with { Caravans = new(owner.Caravans) { [id] = caravan with { DueDate = due } } });
                return;
            }

            //B8 asks C8 then D8; D8 is the current interpolated position,
            //not the stored departure Hex. Its shared path query uses mode8.
            var end = Target(owner, id);
            var start = Position(owner, id, today);
            var output = current.CreateNearbyTemporary();
            int newDays;
            try
            {
                var route = PersonReturnPathSource.Fill(state, current, start, end, commandForce, output);
                newDays = Get(route.Days, route.UnresolvedOwner ?? "State journey2318F0");
            }
            finally { current.ClearNearbyTemporary(output, true); }
            owner = CurrentCommandOwner(state, current); //D8 can initialize a Return deadline.
            if (owner.Returns.ContainsKey(id))
            {
                var initialized = EnsureArrivalDate(state, current, id, today);
                owner = initialized.Owner ?? throw Missing(initialized.UnresolvedOwner ?? "State Return deadline");
                var task = owner.Returns[id];
                current.SetOwnedPersonCommandManager(owner with { Returns = new(owner.Returns)
                    { [id] = task with { ArriveOn = Adjust(task.ArriveOn, Remaining(task.StartedOn, task.ArriveOn, true), newDays) } } });
            }
            else if (owner.Disappearances?.GetValueOrDefault(id) is { } absence)
                current.SetOwnedPersonCommandManager(owner with { Disappearances = new(owner.Disappearances)
                    { [id] = absence with { DueOn = Adjust(absence.DueOn, Remaining(absence.StartedOn, absence.DueOn, false), newDays) } } });
            else if (owner.Talents?.GetValueOrDefault(id) is { } talent)
                current.SetOwnedPersonCommandManager(owner with { Talents = new(owner.Talents)
                    { [id] = talent with { DueDate = Adjust(talent.DueDate, Remaining(talent.StartDate, talent.DueDate, false), newDays) } } });
            else if (owner.Diplomacies?.GetValueOrDefault(id) is { } diplomacy)
                current.SetOwnedPersonCommandManager(owner with { Diplomacies = new(owner.Diplomacies)
                    { [id] = diplomacy with { DueDate = Adjust(diplomacy.DueDate, Remaining(diplomacy.StartDate, diplomacy.DueDate, false), newDays) } } });
            else if (owner.Strategies?.GetValueOrDefault(id) is { } strategic)
                current.SetOwnedPersonCommandManager(owner with { Strategies = new(owner.Strategies)
                    { [id] = strategic with { DueDate = Adjust(strategic.DueDate, Remaining(strategic.StartDate, strategic.DueDate, false), newDays) } } });
            else if (owner.Independences?.GetValueOrDefault(id) is { } independence)
                current.SetOwnedPersonCommandManager(owner with { Independences = new(owner.Independences)
                    { [id] = independence with { DueDate = Adjust(independence.DueDate, Remaining(independence.StartDate, independence.DueDate, false), newDays) } } });
            else if (owner.Intelligences?.GetValueOrDefault(id) is { } intelligence)
                current.SetOwnedPersonCommandManager(owner with { Intelligences = new(owner.Intelligences)
                    { [id] = intelligence with { DueDate = Adjust(intelligence.DueDate, Remaining(intelligence.StartDate, intelligence.DueDate, false), newDays) } } });
            else throw Missing("State journey owned command receiver");

            int Remaining(NativeMapSeasonDate began, NativeMapSeasonDate due, bool clampElapsed)
            {
                int total = unchecked(DateOrdinal(due) - DateOrdinal(began));
                int elapsed = unchecked(now - DateOrdinal(began));
                if (clampElapsed) elapsed = Math.Max(0, elapsed);
                return Math.Max(0, unchecked(total - elapsed));
            }
            NativeMapSeasonDate Adjust(NativeMapSeasonDate due, int previous, int days)
            {
                if (days == previous) return due;
                int adjusted = unchecked(DateOrdinal(due) + days - previous);
                return DateFromOrdinal(days < previous ? Math.Max(now, adjusted) : adjusted);
            }
        }

        HexReference Target(PersonnelCommandTasks owner, int id)
        {
            if (owner.Returns.ContainsKey(id)) return ReadCommandTargetHex(state, current, id);
            int at = owner.Disappearances?.GetValueOrDefault(id)?.StationHex ??
                owner.Talents?.GetValueOrDefault(id)?.EndHexId ?? owner.Diplomacies?.GetValueOrDefault(id)?.EndHexId ??
                owner.Strategies?.GetValueOrDefault(id)?.EndHexId ?? owner.Independences?.GetValueOrDefault(id)?.EndHexId ??
                owner.Intelligences?.GetValueOrDefault(id)?.EndHexId ?? throw Missing("State journey C8 receiver");
            return current.ReadHexTableReference(at is >= 0 and < 48400 ? at : 0) ?? throw Missing("State journey target Hex");
        }

        HexReference Position(PersonnelCommandTasks owner, int id, NativeMapSeasonDate date)
        {
            if (owner.Returns.ContainsKey(id)) return ResolveJourneyPosition(state, current, id, date);
            if (owner.Disappearances?.GetValueOrDefault(id) is { } absence) return ReadDisappearancePosition(current, absence);
            if (owner.Talents?.GetValueOrDefault(id) is { } talent) return TalentTravelSource.ResolvePosition(state, current, talent, date);
            if (owner.Diplomacies?.GetValueOrDefault(id) is { } diplomacy)
                return TalentTravelSource.ResolvePosition(state, current, diplomacy.StartHexId, diplomacy.EndHexId,
                    diplomacy.SourceDistrictId, diplomacy.StartDate, diplomacy.DueDate, date);
            if (owner.Strategies?.GetValueOrDefault(id) is { } strategic)
                return TalentTravelSource.ResolvePosition(state, current, strategic.StartHexId, strategic.EndHexId,
                    strategic.SourceDistrictId, strategic.StartDate, strategic.DueDate, date);
            if (owner.Independences?.GetValueOrDefault(id) is { } independence)
                return TalentTravelSource.ResolvePosition(state, current, independence.StartHexId, independence.EndHexId,
                    independence.SourceDistrictId, independence.StartDate, independence.DueDate, date);
            if (owner.Intelligences?.GetValueOrDefault(id) is { } intelligence)
                return TalentTravelSource.ResolvePosition(state, current, intelligence.StartHexId, intelligence.EndHexId,
                    intelligence.SourceDistrictId, intelligence.StartDate, intelligence.DueDate, date);
            throw Missing("State journey D8 receiver");
        }
    }
}
