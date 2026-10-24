using San14.Core;

namespace San14.Data;

public sealed record BanishOfficerCandidate(int OfficerId, string Name, bool IsCaptive,
    bool? Enabled, string? DisabledReason, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    public AreaAdministrationQuery<IReadOnlyList<BanishOfficerCandidate>> QueryBanishCandidates(GameState state, int forceId)
    {
        try { return QueryBanishCandidatesCurrent(state, CaptureOwnedArmyReturnWorld(state).Context, forceId); }
        catch (InvalidDataException error) { return AreaAdministrationQuery<IReadOnlyList<BanishOfficerCandidate>>.Unknown(error.Message); }
    }

    //6D8950 preserves the Force iterator first, followed by the registered
    //captives whose actual holding City/Gate belongs to this Force.
    internal AreaAdministrationQuery<IReadOnlyList<BanishOfficerCandidate>> QueryBanishCandidatesCurrent(
        GameState state, SourceWorldContext current, int forceId)
    {
        try
        {
            var force = current.ReadForceTableReference(forceId is >= 0 and <= 51 ? forceId : 0)
                ?? throw BanishMissing("Force_pointer");
            if (!BanishNeed(current.ReadForceNativeValidity(force), "Force_validity"))
                return AreaAdministrationQuery<IReadOnlyList<BanishOfficerCandidate>>.Known([]);
            int actualForce = BanishNeed(current.ReadForcePhysicalSlot(force), "Force_identity");
            int[] people = (current.ReadRegisteredPersonPhysicalSlots() ?? throw BanishMissing("Person_manager100"))
                .Select(value => BanishNeed(value, "Person_registry_pointer")).ToArray();
            var rows = new List<BanishOfficerCandidate>();
            foreach (int person in people)
            {
                if (BanishNeed(current.ReadPersonOldForceSlot(person), "iterator_Force") != actualForce ||
                    BanishNeed(current.ReadPersonStatus(person), "iterator_status") is < 1 or > 4) continue;
                if (!BanishNeed(current.ReadPersonNativeValidity(person), "Person_validity") ||
                    BanishNeed(current.ReadPersonStatus(person), "ruler_status") == 1 || DuelAftermathSpecialPerson(current, person)) continue;
                var at = HoldingBase(person);
                if (BanishNeed(current.ReadBaseNativeValidity(at), "holding_Base_validity") &&
                    BanishNeed(current.ReadBaseForceSlot(at), "holding_Base_Force") == actualForce &&
                    (BanishNeed(current.ReadArmyReturnPersonFlags196(person), "Person15E") & 1) == 0) Add(person, false);
            }
            foreach (int person in people)
            {
                if (BanishNeed(current.ReadPersonStatus(person), "captive_status") != 6) continue;
                var at = HoldingBase(person);
                if (BanishNeed(current.ReadBaseNativeValidity(at), "captive_Base_validity") &&
                    BanishNeed(current.ReadBaseForceSlot(at), "captive_Base_Force") == actualForce) Add(person, true);
            }
            return AreaAdministrationQuery<IReadOnlyList<BanishOfficerCandidate>>.Known(rows.AsReadOnly());

            BaseReference HoldingBase(int person)
            {
                ushort second = BanishNeed(current.ReadPersonSecondLocation(person), "holder_E4");
                if (second <= 51) return current.ReadCityTableReference(second) ?? throw BanishMissing("holder_City");
                if (second is >= 52 and <= 61) return current.ReadGateTableReference(second - 51) ?? throw BanishMissing("holder_Gate");
                return new(BaseKind.City, null);
            }
            void Add(int physical, bool captive)
            {
                int id = BanishNeed(current.ReadPersonIdentity(physical), "candidate_identity");
                var person = state.Officers.GetValueOrDefault(id) ?? throw BanishMissing("candidate_domain_person");
                if (current.ResolveOfficerSource(id) != physical) throw BanishMissing("candidate_identity_binding");
                rows.Add(new(id, person.Name, captive, true, null, null));
            }
        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<IReadOnlyList<BanishOfficerCandidate>>.Unknown(error.Message); }
    }

    internal CommandResult ApplyPersonnelBanish(GameState state, ForceState force,
        IReadOnlyList<int> officerIds, CommandContext context)
    {
        if (officerIds.Count == 0 || officerIds.Distinct().Count() != officerIds.Count)
            return CommandResult.Reject("Select at least one distinct officer.", "personnel-banish.selection");
        try
        {
            var world = CaptureOwnedArmyReturnWorld(state);
            var current = world.Context;
            var candidates = QueryBanishCandidatesCurrent(state, current, force.Id);
            if (candidates.Value is null) throw BanishMissing(candidates.UnresolvedInput ?? "candidates");
            var allowed = candidates.Value.ToDictionary(row => row.OfficerId);
            if (officerIds.Any(id => !allowed.TryGetValue(id, out var row) || row.Enabled != true))
                return CommandResult.Reject("An officer is no longer available for banishment or release.", "personnel-banish.qualification");
            var selected = OrderBanishSelectionCurrent(state, current, officerIds);
            var working = state;
            var effects = new List<CampaignEffectRequest>();
            var date = state.NativeMapSeason?.ThirdDate ?? throw BanishMissing("operation_date");
            TacticSourceSamplerWorld Sampler() => ReadArmyDestructionSampler(working, current) ?? throw BanishMissing("current_sampler");
            int Day() => Sampler().Day450;
            foreach (int id in selected)
            {
                int person = current.ResolveOfficerSource(id) ?? throw BanishMissing("selected_person");
                if (!BanishNeed(current.ReadPersonNativeValidity(person), "selected_validity")) continue;
                var origin = current.ReadPersonForce(person) ?? throw BanishMissing("selected_original_Force");
                int originSlot = BanishNeed(current.ReadForcePhysicalSlot(origin), "selected_Force_identity");
                int ruler = BanishNeed(current.ReadForceRecord(originSlot)?.RawRuler10, "selected_Force_ruler");
                var before = current.ReadOfficerAftermath(person) ?? throw BanishMissing("selected_resentment_owner");
                current.SetOfficerAftermath(person, before with { Resentment = new(Math.Clamp(ruler, 0, 6000), 24),
                    SourceKnowledge = (before.SourceKnowledge ?? new(id, null, null, null)) with { UnmappedResentmentTarget = null } });
                working = current.PublishOfficerAftermath(working, id);
                working = ApplyBanishRelationsCurrent(working, current, person, origin);
                byte status = BanishNeed(current.ReadPersonStatus(person), "selected_status");
                if (status is 2 or 3 or 4)
                {
                    var oldHome = status == 3 ? BaseLookup.Find(BanishNeed(current.ReadPersonFirstLocation(person), "governor_E2"), current) : null;
                    working = ApplyCaptiveFreedomCurrent(working, current, world.Pool, person, date, Day, Sampler, effects);
                    if (oldHome is not null && BanishNeed(current.ReadBaseNativeValidity(oldHome), "governor_tail_Base_validity"))
                        ApplyPersonFreedomGovernorCurrent(working, current, oldHome);
                }
                else if (status == 6)
                {
                    working = ApplyCaptiveReleaseCurrent(working, current, person, date, Day, effects);
                    var releasing = current.ReadForceTableReference(force.Id) ?? throw BanishMissing("release_Force");
                    int slot = BanishNeed(current.ReadForcePhysicalSlot(releasing), "release_Force_identity");
                    if (BanishNeed(current.ReadForceNativeValidity(releasing), "release_Force_validity") && slot != 0 &&
                        slot is not (>= 46 and <= 51) &&
                        (BanishNeed(current.ReadArmyReturnPersonFlags196(person), "release_Person15E") & 0x4000) == 0)
                    {
                        int gain = BanishNeed(ArmyDestructionParameterSeed.ReadCurrent(working,
                            SimulationParameterKeys.ArmyDestruction.RulerReleasePopularityGain), "release_popularity_parameter");
                        working = RulerCaptureReleaseSource.ApplyCurrentPopularityAdjustment(working, current, slot, gain);
                    }
                }
            }
            working = OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
            working = OwnedArmyLifecycleDetachedInputs.PublishCurrent(working, current);
            working = ApplyPersonnelBanishReportsCurrent(working, current, force.Id, selected, allowed[selected[0]].IsCaptive, effects, state);
            var pending = working.CampaignEffectRequests ?? throw BanishMissing("pending_owner");
            working = working with { CampaignEffectRequests = pending.Append(CampaignEffectSource.PersonnelAdministrationCommands,
                working.NativeMapSeason?.AdvancedCalendarSteps ?? throw BanishMissing("calendar_owner"), effects) };
            return CommandResult.Accept(PersonReturnCommands.PublishCurrent(working, current));
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, error.Message); }
    }

    //6CC220's stable insertion passes precede both confirmation and1DEEA0.
    //Resolve only reached comparisons, including the real2B2CF0 capacity.
    internal static int[] OrderBanishSelectionCurrent(GameState state, SourceWorldContext current, IReadOnlyList<int> selected)
    {
        var ordered = selected.ToList();
        Pass((left, right) => Status(left) == 6 && Status(right) != 6);
        Pass((left, right) => Flags(left) > Flags(right));
        Pass((left, right) => left < right);
        Pass((left, right) => Capacity(left) > Capacity(right));
        Pass((left, right) => Status(left) == 3 && Status(right) != 3);
        Pass((left, right) => Status(left) == 2 && Status(right) != 2);
        return ordered.ToArray();

        int Physical(int id) => current.ResolveOfficerSource(id) ?? throw BanishMissing("selection_Person");
        byte Status(int id) => BanishNeed(current.ReadPersonStatus(Physical(id)), "selection_status");
        int Flags(int id) => BanishNeed(current.ReadArmyReturnPersonFlags196(Physical(id)), "selection_flags") & 0x4000;
        int Capacity(int id)
        {
            var result = OfficerCommandCapacity.Resolve(state, current, Physical(id));
            return result.Capacity ?? throw BanishMissing(result.UnresolvedInput ?? "selection_capacity2B2CF0");
        }
        void Pass(Func<int, int, bool> before)
        {
            for (int i = 1; i < ordered.Count; i++)
            {
                int person = ordered[i];
                if (!before(person, ordered[i - 1])) continue;
                int insertion = i - 1;
                while (insertion > 0 && before(person, ordered[insertion - 1])) insertion--;
                ordered.RemoveAt(i); ordered.Insert(insertion, person);
            }
        }
    }

    //27F820 mode1 is the source's banishment loyalty wave. Its outside-Force
    //resentment branch exists only in mode0 and is not executed here.
    private GameState ApplyBanishRelationsCurrent(GameState state, SourceWorldContext current, int expelled, ForceReference force)
    {
        if (!BanishNeed(current.ReadPersonNativeValidity(expelled), "wave_subject_validity") ||
            !BanishNeed(current.ReadForceNativeValidity(force), "wave_Force_validity")) return state;
        int subjectId = BanishNeed(current.ReadPersonIdentity(expelled), "wave_subject_identity");
        var working = state;
        foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw BanishMissing("wave_Person_manager"))
        {
            int person = BanishNeed(entry, "wave_Person_pointer");
            int id = BanishNeed(current.ReadPersonIdentity(person), "wave_Person_identity");
            if (id == subjectId) continue;
            ushort R(int physical, int field) => BanishNeed(OwnedPersonRelationInputs.ReadCurrentRelationIdentity(working, physical, field), "wave_relation_" + field.ToString("X"));
            bool ValidId(int value) => value is >= 1 and <= 6000;
            int kind;
            ushort value = R(person, 0xBC);
            if (ValidId(value) && value == R(expelled, 0xBC)) kind = 1;
            else if (ValidId(R(person, 0xBA)) && R(expelled, 0xBA) == id) kind = 2;
            else if (ValidId(R(expelled, 0xB4)) && R(expelled, 0xB4) == id ||
                ValidId(R(expelled, 0xB6)) && R(expelled, 0xB6) == id ||
                ValidId(R(person, 0xB4)) && R(person, 0xB4) == subjectId ||
                ValidId(R(person, 0xB6)) && R(person, 0xB6) == subjectId) kind = 3;
            else if (OfficeRelatedCurrent(working, current, person, expelled) &&
                !OfficeRelationList(working, current, person, expelled, 0xD0)) kind = 4;
            else if (OfficeRelationList(working, current, person, expelled, 0xC0)) kind = 5;
            else continue;
            if (BanishNeed(current.ReadPersonOldForceSlot(person), "wave_other_Force") !=
                BanishNeed(current.ReadForcePhysicalSlot(force), "wave_Force_identity")) continue;
            byte status = BanishNeed(current.ReadPersonStatus(person), "wave_status");
            if (status is < 1 or > 4 || status == 1) continue;
            byte loyalty = BanishNeed(ArmyDestructionLoyalty.ReadCurrentRawE8(working, current, person, _governorInputs), "wave_E8");
            int amount = kind switch { 1 => 20, 2 => 15, 3 => 10, 4 or 5 => 5, _ => 0 };
            byte after = (byte)Math.Clamp(loyalty - amount, 0, 255);
            if ((BanishNeed(current.ReadArmyReturnPersonFlags196(person), "wave_Person15E") & 0x2000) != 0) after = 255;
            current.SetPersonDestructionLoyaltyE8(person, after);
            working = ArmyDestructionLoyalty.PublishCurrentRawE8(working, current, person, after);
        }
        return working;
    }

    private static T BanishNeed<T>(T? value, string field) where T : struct => value ?? throw BanishMissing(field);
    private static InvalidDataException BanishMissing(string field) => new("personnel-banish." + field);
}
