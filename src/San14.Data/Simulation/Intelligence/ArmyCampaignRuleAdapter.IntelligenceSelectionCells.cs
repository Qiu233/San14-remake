using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private static int ReadIntelligenceSelectionTravelDaysCurrent(GameState state, SourceWorldContext current,
        int forceId, int mode, int target, int home)
    {
        var targetBase = mode == 1 ? new DiplomacyCommandSource(state, current).Capital(target)
            : current.ReadCityTableReference(target < 52 ? target : 0)
                ?? throw IntelligenceMissing("selection.travel_target_city");
        if (!IntelligenceNeed(current.ReadBaseNativeValidity(targetBase), "selection.travel_target_validity"))
            throw IntelligenceMissing("selection.travel_target_invalid");
        var startBase = BaseLookup.Find(home, current);
        HexReference Hex(BaseReference location)
        {
            int raw = current.ReadBaseCurrentHexIndex(location);
            return current.ReadHexTableReference(raw is >= 0 and < 48400 ? raw : 0)
                ?? throw IntelligenceMissing("selection.travel_hex");
        }
        var force = current.ReadForceTableReference(forceId) ?? throw IntelligenceMissing("selection.travel_force");
        var list = current.CreateTemporaryHexList() ?? throw IntelligenceMissing("selection.travel_list");
        try
        {
            var result = PersonReturnPathSource.Fill(state, current, Hex(startBase), Hex(targetBase), force, list);
            return result.Days ?? throw IntelligenceMissing(result.UnresolvedOwner ?? "selection.travel_days");
        }
        finally { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
    }

    private static int IntelligenceAssociationSlot(SourceWorldContext current, ForceReference own, int id, bool person)
    {
        for (int slot = 0; slot < 3; slot++)
            if ((person ? IntelligenceNeed(current.ReadForceAssociationPersonId(own, slot), "selection.association_person")
                : IntelligenceNeed(current.ReadForceAssociationTargetSlot(own, slot), "selection.association_target")) == id)
                return slot;
        return -1;
    }

    private static ForceReference IntelligenceSelectionOwn(GameState state, SourceWorldContext current, int selected)
    {
        var force = current.ReadForceTableReference(selected is >= 0 and <= 51 ? selected : 0)
            ?? throw IntelligenceMissing("selection.own_force_pointer");
        if (IntelligenceNeed(current.ReadForceNativeValidity(force), "selection.own_force_validity")) return force;
        return OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw IntelligenceMissing("selection.player_force_pointer");
    }

    private (string Text, AreaMarkerColor? MarkerColour) IntelligenceSelectionForceName(GameState state, SourceWorldContext current, int id)
    {
        string body;
        if (id is >= 46 and <= 50)
            body = UnitDetailBaseName(state, current, BaseKind.City, id + 1);
        else
        {
            int raw = IntelligenceNeed(current.ReadForceRecord(id)?.RawRuler10, "selection.force_ruler");
            int physical = HireAssociationPerson(current, raw);
            if (!IntelligenceNeed(current.ReadPersonNativeValidity(physical), "selection.force_ruler_validity"))
                return ("", null);
            body = IntelligenceSelectionPersonName(state, current, physical);
        }
        var force = current.ReadForceTableReference(id) ?? throw IntelligenceMissing("selection.force_name_pointer");
        byte colour = IntelligenceNeed(current.ReadForceReportColor(force), "selection.force_colour");
        return (ReadHistoryDataText(1, 0x571) + body, MapSpritePalettes.ForceFill(colour));
    }

    private string IntelligenceSelectionForceSortText(GameState state, SourceWorldContext current, int id)
    {
        var name = IntelligenceSelectionForceName(state, current, id);
        if (name.MarkerColour is not { } colour) return "";
        string marker = ReadHistoryDataText(1, 0x571);
        string body = name.Text[marker.Length..];
        return "^08~" + unchecked((int)colour.CpuPacked).ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "~" + marker + "^00~0~" + body;
    }

    private static string IntelligenceSelectionPersonName(GameState state, SourceWorldContext current, int physical)
    {
        int id = IntelligenceNeed(current.ReadPersonIdentity(physical), "selection.person_identity");
        var person = state.Officers.GetValueOrDefault(id) ?? throw IntelligenceMissing("selection.person_name_owner");
        return (person.CurrentSurname ?? throw IntelligenceMissing("selection.person_surname"))
            + (person.CurrentGivenName ?? throw IntelligenceMissing("selection.person_given_name"));
    }

    // The filtered forecast is the same three-slot producer as the capital
    // expense forecast. Its first output is gain; fees and remaining budget
    // are consumed in source slot order, only after a positive gain.
    private int ReadIntelligenceSelectionAssociationGainCurrent(GameState state, SourceWorldContext current,
        ForceReference own, int target)
    {
        var capital = new DiplomacyCommandSource(state, current).Capital(
            IntelligenceNeed(own.PhysicalSlot, "selection.forecast_force"));
        int gold = IntelligenceNeed(current.ReadBaseResource(capital, BaseResourceKind.Gold), "selection.forecast_gold");
        var district = ForcePrimaryDistrictSource.Resolve(current, own);
        int predicted = ReadDiplomacyDistrictBudget(current, IntelligenceNeed(district.PhysicalSlot, "selection.forecast_district"));
        int gainTotal = 0;
        for (int slot = 0; slot < 3; slot++)
        {
            if (ReadHireAssociationBudgetCapacity(state, current, ForcePrimaryDistrictSource.Resolve(current, own)) <= predicted) break;
            int otherId = IntelligenceNeed(current.ReadForceAssociationTargetSlot(own, slot), "selection.forecast_target");
            var other = current.ReadForceTableReference(otherId <= 51 ? otherId : 0)
                ?? throw IntelligenceMissing("selection.forecast_target_pointer");
            int person = HireAssociationPerson(current,
                IntelligenceNeed(current.ReadForceAssociationPersonId(own, slot), "selection.forecast_person"));
            if (!IntelligenceNeed(current.ReadForceNativeValidity(other), "selection.forecast_target_validity") ||
                !IntelligenceNeed(current.ReadPersonNativeValidity(person), "selection.forecast_person_validity") ||
                target is >= 1 and <= 51 && target != other.PhysicalSlot) continue;
            int fee = ReadHireAssociationFee(state, current, own, person);
            if (gold < fee) break;
            int gain = ReadHireAssociationGain(state, current, own, other, person);
            if (gain <= 0) continue;
            predicted = unchecked(predicted + gain); gold = unchecked(gold - fee);
            gainTotal = unchecked(gainTotal + gain);
        }
        return gainTotal;
    }

    private sealed partial class OfficerInformationCurrent
    {
        internal IntelligencePersonSelection IntelligencePages(int mode,
            IReadOnlyList<IntelligenceTargetChoice> choices, Func<int, int> travelDays, string guide)
        {
            commandBodyPalette = mode != 0; movementTravelDays = travelDays;
            var groups = mode == 0 ? IntelligenceSelectionColumnsSource.HomePerson(Data)
                : mode == 1 ? IntelligenceSelectionColumnsSource.AwayPerson(Data)
                : IntelligenceSelectionColumnsSource.InspectPerson(Data);
            var cache = new Dictionary<(int Person, int Column), PreparedCell>();
            PreparedCell Read(int id, int column)
            {
                if (column is not (>= 199 and <= 203)) return Cell(id, column);
                if (cache.TryGetValue((id, column), out var saved)) return saved;
                try { saved = IntelligenceCell(ReadPerson(id), column); }
                catch (InvalidDataException error) { saved = new(new(null, null, error.Message), null, error.Message); }
                return cache[(id, column)] = saved;
            }
            var ids = choices.Select(row => row.Id).ToArray();
            var pages = new OfficerInformationPageSet(Data(1, 0x58F), groups, ids,
                (id, column) => Read(id, column).Display with { SortUnresolvedOwner = Read(id, column).SortUnresolvedOwner },
                (id, column) => Read(id, column).SortUnresolvedOwner,
                (left, right, column, ascending) => (Read(left, column).Key ?? throw Missing("intelligence.sort_key"))
                    .Before(Read(right, column).Key ?? throw Missing("intelligence.sort_key"), ascending));
            return new(pages, ids.ToDictionary(id => id, MovementAlteredHealth), choices.ToDictionary(row => row.Id), guide);
        }

        private PreparedCell IntelligenceCell(Person person, int column)
        {
            bool Trait(int id)
            {
                for (int slot = 0; slot < 9; slot++)
                    if (Need(TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current, person.PhysicalSlot, slot),
                        "intelligence.trait_slot") == id) return true;
                return false;
            }
            ForceReference Own() => OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                ?? throw Missing("intelligence.player_force");
            int Slot() => IntelligenceAssociationSlot(current, Own(), person.Id, true);
            int Target()
            {
                int slot = Slot();
                int raw = slot >= 0 ? Need(current.ReadForceAssociationTargetSlot(Own(), slot), "intelligence.target") : 0;
                return raw <= 51 ? raw : 0;
            }
            bool Assigned() => InformationNativeValid(person) &&
                Need(current.ReadForceNativeValidity(Own()), "intelligence.player_validity") && Slot() >= 0;
            (string Text, AreaMarkerColor? MarkerColour) Name() => rules.IntelligenceSelectionForceName(state, current, Target());
            var result = column switch
            {
                199 or 200 or 201 => MakeCell(() => Trait(column == 199 ? 211 : column == 200 ? 212 : 88) ? Data(1, 0x338) : "",
                    () => new NumberKey(Trait(column == 199 ? 211 : column == 200 ? 212 : 88) ? 1 : 0)),
                202 => MakeCell(() => Assigned() ? Data(1, 0x338) : "", () => new NumberKey(Assigned() ? 1 : 0)),
                203 => MakeCell(() => Assigned() ? Name().Text : "",
                    () => new NameKey(rules.IntelligenceSelectionForceSortText(state, current, Target()))),
                _ => throw Missing("intelligence.column")
            };
            if (column == 203)
                try { return result with { Display = result.Display with { MarkerColour = Assigned() ? Name().MarkerColour : null } }; }
                catch (InvalidDataException error) { return result with { Display = result.Display with
                    { UnresolvedOwner = Join(result.Display.UnresolvedOwner, error.Message) } }; }
            return result;
        }
    }

    private sealed partial class ForceInformationCurrent
    {
        internal ForceInformationPageSet IntelligencePages(IReadOnlyList<int> ids)
        {
            var cache = new Dictionary<(int Force, int Column), PreparedCell>();
            PreparedCell Read(int id, int column)
            {
                if (column is not (35 or 36 or 39)) return Cell(id, column);
                if (cache.TryGetValue((id, column), out var saved)) return saved;
                try { saved = IntelligenceCell(id, column); }
                catch (InvalidDataException error) { saved = new(new(null, null, error.Message), null, error.Message); }
                return cache[(id, column)] = saved;
            }
            return new(Data(1, 0x58D), IntelligenceSelectionColumnsSource.AwayForce(Data), ids,
                (id, column) => Read(id, column).Display with { SortUnresolvedOwner = Read(id, column).SortUnresolvedOwner },
                (id, column) => Read(id, column).SortUnresolvedOwner,
                (left, right, column, ascending) => Before(Read(left, column).Key ?? throw Missing("intelligence.sort_key"),
                    Read(right, column).Key ?? throw Missing("intelligence.sort_key"), column, ascending));
        }

        private PreparedCell IntelligenceCell(int id, int column)
        {
            var own = IntelligenceSelectionOwn(state, Current, playerForceId);
            bool ValidPair() => Need(Current.ReadForceNativeValidity(own), "intelligence.own_validity") && Valid(id);
            int Assigned() => IntelligenceAssociationSlot(Current, own, id, false);
            int Actor()
            {
                for (int slot = 0; slot < 3; slot++)
                {
                    if (Need(Current.ReadForceAssociationTargetSlot(own, slot), "intelligence.target_slot") != id) continue;
                    int physical = Person(Need(Current.ReadForceAssociationPersonId(own, slot), "intelligence.person_slot"));
                    if (Need(Current.ReadPersonNativeValidity(physical), "intelligence.person_validity")) return physical;
                }
                return -1;
            }
            int Adviser() => Person(Need(Current.ReadAssignmentForceAdvisor52(id), "intelligence.adviser"));
            bool AdviserValid() => Need(Current.ReadPersonNativeValidity(Adviser()), "intelligence.adviser_validity");
            int AdviserIntelligence() => Need(rules.SearchAbilities(Current).ResolveDimension(state,
                Need(Current.ReadPersonIdentity(Adviser()), "intelligence.adviser_id"), 2,
                (force, effect) => rules._baseCapacityInputs.ReadPanelModifier(state, force, effect)), "intelligence.adviser_ability");
            int Gain() => rules.ReadIntelligenceSelectionAssociationGainCurrent(state, Current, own, id);
            return column switch
            {
                35 => MakeCell(() => ValidPair() && Actor() is >= 0 and var actor
                        ? IntelligenceSelectionPersonName(state, Current, actor) : "",
                    () => ValidPair() && Actor() is >= 0 and var actor
                        ? new NameKey(IntelligenceSelectionPersonName(state, Current, actor)) : new NameKey("", true)),
                36 => MakeCell(() => AdviserValid() ? Number(AdviserIntelligence()) : "",
                    () => new NumberKey(Valid(id) && AdviserValid() ? AdviserIntelligence() : 0)),
                39 => MakeCell(() => ValidPair() && Assigned() >= 0 ? Number(Math.Max(0, Gain())) : "",
                    () => ValidPair() ? new NumberKey(Gain()) : new NumberKey(int.MaxValue, true)),
                _ => throw Missing("intelligence.column")
            };
        }
    }

    private sealed partial class CityInformationCurrent
    {
        internal CityInformationPageSet IntelligencePages(IReadOnlyList<int> ids)
        {
            var cache = new Dictionary<int, PreparedCell>();
            PreparedCell Read(int id, int column)
            {
                if (column != 34) return Cell(id, column);
                if (cache.TryGetValue(id, out var saved)) return saved;
                try
                {
                    int person = Governor(ReadCity(id));
                    bool Valid() => Need(Current.ReadPersonNativeValidity(person), "intelligence.governor_validity");
                    byte Status() => Need(Current.ReadPersonStatus(person), "intelligence.governor_status");
                    int Loyalty() => Need(ArmyDestructionLoyalty.ReadCurrentRawE8(state, Current, person, rules._governorInputs),
                        "intelligence.governor_loyalty");
                    saved = MakeCell(() => !Valid() ? "" : Status() == 1 ? Data(1, 0x353)
                            : Status() is >= 1 and <= 4 ? Number(Math.Min(Loyalty(), 100)) : "",
                        () => Valid() ? new NumberKey(Loyalty()) : new NumberKey(int.MaxValue, true));
                }
                catch (InvalidDataException error) { saved = new(new(null, null, error.Message), null, error.Message); }
                return cache[id] = saved;
            }
            return new(Data(1, 0x58D), IntelligenceSelectionColumnsSource.InspectCity(Data), ids,
                (id, column) => Read(id, column).Display with { SortUnresolvedOwner = Read(id, column).SortUnresolvedOwner },
                (id, column) => Read(id, column).SortUnresolvedOwner,
                (left, right, column, ascending) => (Read(left, column).Key ?? throw Missing("intelligence.sort_key"))
                    .Before(Read(right, column).Key ?? throw Missing("intelligence.sort_key"), ascending));
        }
    }
}
