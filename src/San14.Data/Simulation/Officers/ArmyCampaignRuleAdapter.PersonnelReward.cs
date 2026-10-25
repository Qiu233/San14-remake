using San14.Core;

namespace San14.Data;

internal sealed record PersonnelRewardPersonPreview(int OfficerId, int PhysicalSlot,
    int LoyaltyBefore, int LoyaltyAfter, int Gain);
internal sealed record PersonnelRewardPreview(int DistrictId, int GoldBefore, int GoldAfter,
    int GoldCost, int BooksBefore, int BooksAfter, int BookCost,
    IReadOnlyList<PersonnelRewardPersonPreview> People);

public sealed partial class ArmyCampaignRuleAdapter
{
    private const string PersonnelRewardDivisorKey = "simulation.personnel.reward-loyalty-divisor";
    private const string PersonnelRewardBookCostKey = "simulation.personnel.reward-command-book-cost";

    //TC1F2A50's two immutable file-backed rules. Older saved campaigns can
    //lack these newly named keys; a saved integer still takes precedence.
    private static readonly IReadOnlyDictionary<string, int> PersonnelRewardDefaults =
        new Dictionary<string, int> { [PersonnelRewardDivisorKey] = 15, [PersonnelRewardBookCostKey] = 1 };

    internal static GameState InstallFreshPersonnelRewardParameters(GameState state)
    {
        var simulation = state.NativeArmySimulation ?? throw PersonnelRewardMissing("parameters");
        if (simulation.Parameters.Language != "TC") return state;
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        foreach (var pair in PersonnelRewardDefaults) values.TryAdd(pair.Key, pair.Value);
        return state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values } } };
    }

    //The page has already captured its candidate list. Quoting a selection
    //does not rebuild that list, mutate current, or consume the App stream.
    internal PersonnelRewardPreview ReadPersonnelRewardPreviewCurrent(GameState state,
        SourceWorldContext current, int districtPhysical, IReadOnlyList<int> officerIds)
    {
        var source = ReadPersonnelRewardSourceCurrent(state, current, districtPhysical);
        int divisor = ReadPersonnelRewardParameter(state, PersonnelRewardDivisorKey);
        if (divisor == 0) throw PersonnelRewardMissing("zero_loyalty_divisor");
        var people = new List<PersonnelRewardPersonPreview>(officerIds.Count);
        TacticSourceSamplerWorld? sampler = null;
        foreach (int id in officerIds)
        {
            int physical = current.ResolveOfficerSource(id) ?? throw PersonnelRewardMissing("selected_Person_pointer");
            if (!PersonnelRewardNeed(current.ReadPersonNativeValidity(physical), "selected_Person_validity"))
                throw PersonnelRewardMissing("selected_Person_invalid");
            int first = PersonnelRewardNeed(OwnedPersonRelationInputs.ReadCurrentAffinity(state, source.Ruler), "ruler_BE");
            int second = PersonnelRewardNeed(OwnedPersonRelationInputs.ReadCurrentAffinity(state, physical), "selected_BE");
            int distance = Math.Abs(first - second);
            distance = Math.Min(Math.Min(distance, 150 - distance), 75); //2A6260.
            sampler ??= ReadArmyDestructionSampler(state, current) ?? throw PersonnelRewardMissing("sampler_World450_45C");
            int sample = TacticEffectSourceSamplerMath.Sample(2, 1,
                PersonnelRewardNeed(current.ReadPersonIdentity(source.Ruler), "ruler_ID"), id, 0, 0, sampler);
            int gain = Math.Min(9, unchecked((75 - distance) / divisor + 3 + sample));
            byte before = PersonnelRewardNeed(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, physical,
                _governorInputs), "selected_E8");
            ushort flags = PersonnelRewardNeed(current.ReadArmyReturnPersonFlags196(physical), "selected_15E");
            byte after = (flags & 0x2000) != 0 ? (byte)255 : unchecked((byte)Math.Min(100, unchecked(before + gain)));
            people.Add(new(id, physical, before, after, gain));
        }
        int goldCost = checked(officerIds.Count * 100);
        int gold = BaseCurrencyAdditionSource.ReadCurrentGold(source.Home, current);
        //027300 repeats currency routing for the capacity, read and setter.
        //Keep those actual City capacities rather than assuming a subtraction
        //can never encounter an already over-capacity stock.
        int cap = BasePostLoad.ReadResourceCapacities(BaseCurrencyAdditionSource.CurrencyCity(source.Home, current), current).Gold;
        int requested = Math.Max(0, Math.Min(unchecked(gold - goldCost), cap));
        int setterCap = BasePostLoad.ReadResourceCapacities(BaseCurrencyAdditionSource.CurrencyCity(source.Home, current), current).Gold;
        int afterGold = Math.Max(0, Math.Min(requested, setterCap));
        var district = state.Districts.GetValueOrDefault(source.DistrictId) ?? throw PersonnelRewardMissing("District_owner");
        int books = district.OrdersRemaining ?? throw PersonnelRewardMissing("District14");
        int cost = ReadPersonnelRewardParameter(state, PersonnelRewardBookCostKey);
        if (cost < 0) throw PersonnelRewardMissing("negative_command_book_cost");
        return new(source.DistrictId, gold, afterGold, goldCost, books,
            Math.Clamp(unchecked(books - cost), 0, 200), cost, people.AsReadOnly());
    }

    internal CommandResult ApplyPersonnelReward(GameState state, ForceState force, int districtId,
        IReadOnlyList<int> officerIds, CommandContext context)
    {
        if (officerIds.Count == 0 || officerIds.Distinct().Count() != officerIds.Count)
            return CommandResult.Reject("Select distinct officers to reward.", "personnel.reward.selection");
        try
        {
            var world = CaptureOwnedArmyReturnWorld(state);
            var current = world.Context;
            var source = ReadPersonnelRewardSourceCurrent(state, current, districtId);
            if (source.ForceId != force.Id)
                return CommandResult.Reject("The reward treasury belongs to another force.", "personnel.reward.control");
            var candidates = ReadRewardCandidatesCurrent(state, current, districtId).ToDictionary(row => row.OfficerId);
            if (officerIds.Any(id => !candidates.ContainsKey(id)))
                return CommandResult.Reject("An officer is no longer eligible for a reward.", "personnel.reward.qualification");
            var quote = ReadPersonnelRewardPreviewCurrent(state, current, districtId, officerIds);
            if (quote.GoldBefore < quote.GoldCost)
                return CommandResult.Reject("The capital has insufficient gold.", "personnel.reward.gold");
            if (quote.BooksBefore - CommandBookAvailability.Reserved(state, quote.DistrictId) < quote.BookCost)
                return CommandResult.Reject("Insufficient command books.", "personnel.reward.command_books");
            int representative = ReadPersonnelRewardRepresentativeCurrent(state, current, officerIds);
            var working = state;
            foreach (var person in quote.People)
            {
                ushort flags = PersonnelRewardNeed(current.ReadArmyReturnPersonFlags196(person.PhysicalSlot), "write_Person15E");
                current.WriteArmyReturnPersonFlags196(person.PhysicalSlot, (ushort)(flags | 2));
                current.SetPersonDestructionLoyaltyE8(person.PhysicalSlot, (byte)person.LoyaltyAfter);
                working = ArmyDestructionLoyalty.PublishCurrentRawE8(working, current,
                    person.PhysicalSlot, (byte)person.LoyaltyAfter);
            }
            working = BaseCurrencyAdditionSource.Add(working, current, source.Home, -quote.GoldCost,
                city => BasePostLoad.ReadResourceCapacities(city, current).Gold).State;
            current.SetDistrictRawByte(quote.DistrictId, 0x14, (byte)quote.BooksAfter);
            var district = working.Districts[quote.DistrictId];
            var saved = working.NativeForceUnregisterCurrent ?? throw PersonnelRewardMissing("District_saved_fields");
            var window = saved.DistrictWindows10.GetValueOrDefault(quote.DistrictId)?.ToArray()
                ?? throw PersonnelRewardMissing("District_saved_window");
            window[4] = (byte)quote.BooksAfter;
            working = working with
            {
                Districts = new(working.Districts) { [quote.DistrictId] = district with { OrdersRemaining = quote.BooksAfter } },
                NativeForceUnregisterCurrent = saved with
                    { DistrictWindows10 = new(saved.DistrictWindows10) { [quote.DistrictId] = window } }
            };
            //1F2CAF reaches the separate statistics writer only for the
            //selected user and the player's actual charged District.
            if (HasCommandAccountingUser(working))
            {
                var charged = current.ReadDistrictTableReference(quote.DistrictId)
                    ?? throw PersonnelRewardMissing("App_charged_District");
                if (BaseCaptureRewardSource.ReadPlayerRulerDistrict(current, charged, PersonnelRewardMissing))
                    working = ApplyCommandBookUsageCurrent(working, current, quote.BookCost);
            }
            working = OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
            var effects = new List<CampaignEffectRequest>();
            working = ApplyPersonnelRewardReportsCurrent(working, current, force.Id,
                representative, officerIds.Count, effects, state);
            working = working with { CampaignEffectRequests = (working.CampaignEffectRequests
                ?? throw PersonnelRewardMissing("pending_owner")).Append(CampaignEffectSource.PersonnelAdministrationCommands,
                working.NativeMapSeason?.AdvancedCalendarSteps ?? throw PersonnelRewardMissing("calendar_owner"), effects) };
            return CommandResult.Accept(working.WithArmyLifecycle(working.ReadArmyLifecycle()! with
                { Indexes = world.Pool.ExportOwnedState() }));
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, error.Message); }
    }

    private (BaseReference Home, int DistrictId, int ForceId, int Ruler) ReadPersonnelRewardSourceCurrent(
        GameState state, SourceWorldContext current, int districtId)
    {
        ushort leaderId = PersonnelRewardNeed(current.ReadDistrictLeader(districtId), "source_District12");
        int leader = PersonnelRewardNeed((leaderId <= 6000 ? current.ReadPersonLookupReference(leaderId)
            : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "source_District_leader_pointer");
        var home = BaseLookup.Find(PersonnelRewardNeed(current.ReadPersonFirstLocation(leader), "source_leader_E2"), current);
        if (!PersonnelRewardNeed(current.ReadBaseNativeValidity(home), "source_Base_validity"))
            throw PersonnelRewardMissing("source_Base_invalid");
        var district = current.ReadBaseDistrictReference(home) ?? throw PersonnelRewardMissing("source_Base_virtual80");
        int actual = PersonnelRewardNeed(district.PhysicalSlot, "source_District_pointer");
        if (!PersonnelRewardNeed(current.ReadDistrictNativeValidity(district), "source_District_validity"))
            throw PersonnelRewardMissing("source_District_invalid");
        byte rawForce = PersonnelRewardNeed(current.ReadDistrictForceSlot(district), "source_District10");
        var force = current.ReadForceTableReference(rawForce < 52 ? rawForce : 0) ?? throw PersonnelRewardMissing("source_Force_pointer");
        if (!PersonnelRewardNeed(current.ReadForceNativeValidity(force), "source_Force_validity"))
            throw PersonnelRewardMissing("source_Force_invalid");
        int forceId = PersonnelRewardNeed(current.ReadForcePhysicalSlot(force), "source_Force_identity");
        ushort rulerId = current.ReadForceRecord(forceId)?.RawRuler10 ?? throw PersonnelRewardMissing("source_ruler_ID");
        int ruler = PersonnelRewardNeed((rulerId <= 6000 ? current.ReadPersonLookupReference(rulerId)
            : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "source_ruler_pointer");
        if (!PersonnelRewardNeed(current.ReadPersonNativeValidity(ruler), "source_ruler_validity"))
            throw PersonnelRewardMissing("source_ruler_invalid");
        return (home, actual, forceId, ruler);
    }

    private int ReadPersonnelRewardRepresentativeCurrent(GameState state, SourceWorldContext current, IReadOnlyList<int> ids)
    {
        int selected = ids[0];
        int Physical(int id) => current.ResolveOfficerSource(id) ?? throw PersonnelRewardMissing("speaker_pointer");
        byte Status(int id) => PersonnelRewardNeed(current.ReadPersonStatus(Physical(id)), "speaker_status");
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        int Charm(int id) => PersonnelRewardNeed(abilities.ResolveDimension(state, id, 4,
            (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect)), "speaker_Charm");
        foreach (int id in ids.Skip(1))
        {
            int status = Status(id), prior = Status(selected);
            if (status > prior) continue;
            if (status < prior) { selected = id; continue; }
            int charm = Charm(id), priorCharm = Charm(selected);
            if (charm > priorCharm || charm == priorCharm && id < selected) selected = id;
        }
        return selected;
    }

    private static int ReadPersonnelRewardParameter(GameState state, string name)
    {
        var bank = state.NativeArmySimulation?.Parameters ?? throw PersonnelRewardMissing("parameters");
        if (bank.Language != "TC" || bank.RuleVersion != SimulationRuleIdentity.ForLocale("TC") ||
            bank.Float32Values.ContainsKey(name)) throw PersonnelRewardMissing(name);
        return bank.Int32Values.TryGetValue(name, out int value) ? value
            : PersonnelRewardDefaults.TryGetValue(name, out int initial) ? initial : throw PersonnelRewardMissing(name);
    }
    private static T PersonnelRewardNeed<T>(T? value, string owner) where T : struct => value ?? throw PersonnelRewardMissing(owner);
    private static InvalidDataException PersonnelRewardMissing(string owner) => new("personnel.reward." + owner);
}
