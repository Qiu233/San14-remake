using San14.Core;

namespace San14.Data;

internal sealed record LearnTacticChoice(int TacticId, string Name, bool CanReplace)
{
    internal int SlotIndex { get; init; }
}
internal sealed record LearnCommandPerson(int Id, int Physical, string Name, IReadOnlyList<LearnTacticChoice> Tactics);
internal sealed record LearnCommandBook(int GemId, int Physical, int TacticId, string Name, int Count);
internal sealed record LearnCommandCurrentRules(SourceWorldContext Current, int ForceId, int DistrictId,
    int AvailableBooks, int BookCost, IReadOnlyList<LearnCommandPerson> People, IReadOnlyList<LearnCommandBook> Books);

public sealed partial class ArmyCampaignRuleAdapter
{
    internal LearnCommandCurrentRules CaptureLearnCommandCurrentRules(GameState state, SourceWorldContext current, int forceId)
    {
        var force = current.ReadForceTableReference(forceId) ?? throw LearnMissing("Force_pointer");
        if (!LearnNeed(current.ReadForceNativeValidity(force), "Force_validity")) throw LearnMissing("Force_inactive");
        var primary = ForcePrimaryDistrictSource.Resolve(current, force);
        if (!LearnNeed(current.ReadDistrictNativeValidity(primary), "primary_District_validity")) throw LearnMissing("primary_District_inactive");
        int district = LearnNeed(primary.PhysicalSlot, "primary_District_pointer");
        int available = state.Districts.GetValueOrDefault(district)?.OrdersRemaining ?? throw LearnMissing("primary_District14");
        var people = new List<LearnCommandPerson>();
        //6DDCC0 uses the Force's serving-person iterator(filter0), in registry order.
        foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw LearnMissing("Person_registry"))
        {
            int physical = LearnNeed(entry, "Person_registry_pointer");
            if (LearnNeed(current.ReadPersonOldForceSlot(physical), "Person_Force") != forceId ||
                LearnNeed(current.ReadPersonStatus(physical), "PersonE6") is not (>= 1 and <= 4)) continue;
            ushort flags = LearnNeed(current.ReadArmyReturnPersonFlags196(physical), "Person15E");
            if ((flags & 1) != 0 || PersonCurrentBusySource.Read(current, physical, LearnMissing) ||
                DuelAftermathSpecialPerson(current, physical) || (flags & 0x4000) != 0) continue;
            int id = LearnNeed(current.ReadPersonIdentity(physical), "Person10");
            var owner = state.Officers.GetValueOrDefault(id) ?? throw LearnMissing("Person_domain");
            var tactics = new List<LearnTacticChoice>();
            for (int index = 0; index < 10; index++)
            {
                var tactic = LearnPersonTactic(current, physical, index);
                if (!LearnNeed(current.ReadSkillTacticValidity(tactic), "Person_Tactic_validity")) continue;
                int tacticId = LearnNeed(current.ReadSkillTacticPhysicalSlot(tactic), "Person_Tactic_identity");
                tactics.Add(new(tacticId, LearnTacticName(state, tacticId), true) { SlotIndex = index });
            }
            //75D170 exposes replacement buttons only for a full ten-tactic
            //prefix; with free capacity the old entries are display markers.
            bool replace = tactics.Count == 10;
            people.Add(new(id, physical, owner.Name, tactics.Select(t => t with { CanReplace = replace }).ToArray()));
        }
        var books = new List<LearnCommandBook>();
        //6DD400 visits Gem table1..30 in that order; count is a real signed word.
        for (int slot = 1; slot <= 30; slot++)
        {
            var gem = current.ReadSkillGemReference(slot) ?? throw LearnMissing("Gem_table");
            int count = LearnNeed(TalentGemAcquisitionSource.Read(state.GemForceInventory, force, gem), "Gem_inventory");
            if (count == 0 || !LearnNeed(current.ReadSkillGemValidity(gem), "Gem_validity")) continue;
            int physical = LearnNeed(gem.PhysicalSlot, "Gem_physical");
            int raw = LearnNeed(current.ReadSkillGemTactic52(gem), "Gem_Tactic52");
            var tactic = current.ReadSkillTacticReference(raw <= 200 ? raw : 0) ?? throw LearnMissing("Gem_Tactic_lookup");
            int tacticId = LearnNeed(current.ReadSkillTacticPhysicalSlot(tactic), "Gem_Tactic_identity");
            books.Add(new(slot, physical, tacticId, _skills.Gems.Gems[physical].Name, count));
        }
        return new(current, forceId, district, available, LearnParameter(state, "command-books", 1), people.AsReadOnly(), books.AsReadOnly());
    }

    internal bool QueryLearnGemEligibility(GameState state, LearnCommandCurrentRules rules, int officerId, int gemId)
    {
        var person = rules.People.FirstOrDefault(p => p.Id == officerId) ?? throw LearnMissing("selected_Person");
        var book = rules.Books.FirstOrDefault(b => b.GemId == gemId) ?? throw LearnMissing("selected_Gem");
        var current = rules.Current;
        var tactic = new TacticReference(book.TacticId);
        if (!LearnNeed(current.ReadSkillTacticValidity(tactic), "learned_Tactic_validity")) return false;
        //749CF0 first rejects an existing tactic, before reading any formation.
        if (person.Tactics.Any(t => t.TacticId == book.TacticId)) return false;
        uint mask = 0;
        for (int part = 0; part < 4; part++)
            mask |= (uint)LearnNeed(current.ReadSkillTacticRawByte(tactic, 0x6C + part), "learned_Tactic_formations6C") << (part * 8);
        //Actual Formation table receiver order, not a fabricated army/caster.
        for (int slot = 0; slot <= 20; slot++)
        {
            var formation = current.ReadStaticTableReference(0x77428, slot) ?? throw LearnMissing("Formation_table");
            int physical = LearnNeed(formation.PhysicalSlot, "Formation_physical");
            if (physical == 0 || physical >= 21 || (mask & (1u << physical)) == 0) continue;
            if (LearnNeed(TacticRemakeCurrentFormations.ReadRawByte(state, physical, 0x7E), "Formation7E") == 2)
            {
                //042950: this admitted learner is known non-special. The Force
                //native-validity gate precedes its actual15E formation byte.
                var force = current.ReadPersonForce(person.Physical) ?? throw LearnMissing("formation_Person_Force");
                int selected = 11;
                if (LearnNeed(current.ReadForceNativeValidity(force), "formation_Force_validity"))
                {
                    int forcePhysical = LearnNeed(force.PhysicalSlot, "formation_Force_physical");
                    selected = LearnForceFormationCurrent(state, forcePhysical);
                }
                if (selected == physical) return true;
            }
            else
            {
                uint formations = LearnNeed(current.ReadPersonFormationMask164(person.Physical), "Person_formation_mask");
                if ((formations & (1u << physical)) == 0) continue;
                //232480's independent identity restriction applies only to14.
                if (physical == 14 && unchecked((ushort)(person.Id - 0x1005)) >= 100) continue;
                return true;
            }
        }
        return false;
    }

    internal IReadOnlyList<LearnTacticChoice> ReadLearnReplacementCandidatesCurrent(LearnCommandCurrentRules rules, int officerId)
        => rules.People.FirstOrDefault(p => p.Id == officerId)?.Tactics ?? throw LearnMissing("replacement_Person");

    internal bool ReadLearnReplacementRequiredCurrent(LearnCommandCurrentRules rules, int officerId)
        => ReadLearnReplacementCandidatesCurrent(rules, officerId).Count == 10;

    internal CommandResult ApplyPersonnelLearn(GameState state, ForceState force, int officerId,
        int gemId, int? replacedTacticId, CommandContext context)
    {
        try
        {
            if (state.Phase != TurnPhase.Strategy) throw LearnMissing("Strategy_phase");
            var world = CaptureOwnedArmyReturnWorld(state);
            var current = world.Context;
            var rules = CaptureLearnCommandCurrentRules(state, current, force.Id);
            if (rules.AvailableBooks < rules.BookCost) throw LearnMissing("command_books_insufficient");
            if (!QueryLearnGemEligibility(state, rules, officerId, gemId)) throw LearnMissing("Gem_not_learnable");
            var person = rules.People.First(p => p.Id == officerId);
            var book = rules.Books.First(b => b.GemId == gemId);
            if (replacedTacticId is int removed && !person.Tactics.Any(t => t.TacticId == removed && t.CanReplace))
                throw LearnMissing("replacement_Tactic_not_owned");
            if (person.Tactics.Count == 10 && replacedTacticId is null) throw LearnMissing("replacement_required");
            var working = ApplyLearnTacticSlotsCurrent(state, current, person, book, replacedTacticId);
            var inventory = working.GemForceInventory ?? throw LearnMissing("Force_inventory_owner");
            int inventoryForce = LearnNeed(current.ReadForceTableReference(force.Id)?.PhysicalSlot, "inventory_Force_receiver");
            if (!inventory.Forces.TryGetValue(inventoryForce, out var sourceCounts) || sourceCounts.Length != 31)
                throw LearnMissing("Force_inventory_vector");
            var counts = sourceCounts.ToArray();
            counts[book.Physical] = (ushort)Math.Clamp(unchecked((int)counts[book.Physical] - 1), 0, 100);
            working = working with { GemForceInventory = inventory with { Forces = new(inventory.Forces) { [inventoryForce] = counts } } };
            var effects = new List<CampaignEffectRequest>();
            working = ApplyLearnForceExperienceCurrent(working, current, person.Physical, force.Id, effects);
            //1F0610 resolves primary District again after the experience writer.
            var paymentForce = current.ReadForceTableReference(force.Id) ?? throw LearnMissing("payment_Force_pointer");
            var primary = ForcePrimaryDistrictSource.Resolve(current, paymentForce);
            int districtId = LearnNeed(primary.PhysicalSlot, "payment_primary_District");
            var district = working.Districts.GetValueOrDefault(districtId) ?? throw LearnMissing("payment_District_owner");
            int after = Math.Clamp(unchecked(LearnNeed(district.OrdersRemaining, "payment_District14") - rules.BookCost), 0, 200);
            current.SetDistrictRawByte(districtId, 0x14, (byte)after);
            var saved = working.NativeForceUnregisterCurrent ?? throw LearnMissing("payment_District_fields");
            var window = saved.DistrictWindows10.GetValueOrDefault(districtId)?.ToArray() ?? throw LearnMissing("payment_District_window");
            window[4] = (byte)after;
            working = working with
            {
                Districts = new(working.Districts) { [districtId] = district with { OrdersRemaining = after } },
                NativeForceUnregisterCurrent = saved with { DistrictWindows10 = new(saved.DistrictWindows10) { [districtId] = window } }
            };
            if (HasCommandAccountingUser(working) && BaseCaptureRewardSource.ReadPlayerRulerDistrict(current, primary, LearnMissing))
                working = ApplyCommandBookUsageCurrent(working, current, rules.BookCost);
            //741E00's system achievement calls do not alter the campaign world.
            //The source sound and formatted announcement remain real pending operands.
            working = ApplyPersonnelLearnReportCurrent(working, current, officerId, LearnTacticName(working, book.TacticId), effects);
            working = working with { CampaignEffectRequests = (working.CampaignEffectRequests ?? throw LearnMissing("pending_effect_owner"))
                .Append(CampaignEffectSource.PersonnelAdministrationCommands,
                    working.NativeMapSeason?.AdvancedCalendarSteps ?? throw LearnMissing("calendar_owner"), effects) };
            return CommandResult.Accept(working.WithArmyLifecycle(working.ReadArmyLifecycle()! with { Indexes = world.Pool.ExportOwnedState() }));
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable("personnel.learn", error.Message); }
    }

    private GameState ApplyLearnTacticSlotsCurrent(GameState state, SourceWorldContext current,
        LearnCommandPerson person, LearnCommandBook book, int? replacedTactic)
    {
        //The shared primary list allocation and per-node suppression belong to
        //this operation; never replace this with a new world capture.
        var list = current.CreateTemporaryHexList() ?? throw LearnMissing("Tactic_temporary_list");
        var values = new List<(TacticReference Tactic, HexListNodeReference Node)>();
        try
        {
            for (int index = 0; index < 10; index++)
            {
                var tactic = LearnPersonTactic(current, person.Physical, index);
                if (!LearnNeed(current.ReadSkillTacticValidity(tactic), "old_Tactic_validity")) continue;
                int id = LearnNeed(tactic.PhysicalSlot, "old_Tactic_identity");
                if (replacedTactic == id) continue;
                var node = current.AppendLearnTacticTemporary(list, tactic);
                if (node.Identity.HasValue) values.Add((tactic, node));
            }
            var learned = new TacticReference(book.TacticId);
            var learnedNode = current.AppendLearnTacticTemporary(list, learned);
            if (learnedNode.Identity.HasValue) values.Add((learned, learnedNode));
            //1D76F0: source233820 group descending, then identity ascending.
            var sorted = values.OrderByDescending(t => LearnOrdinaryTacticGroup(LearnNeed(t.Tactic.PhysicalSlot, "sort_Tactic")))
                .ThenBy(t => LearnNeed(t.Tactic.PhysicalSlot, "sort_Tactic")).ToArray();
            if (list.Identity.HasValue) current.ReorderCurrentList(list, sorted.Select(t => t.Node).ToArray());
            var owner = state.Officers[person.Id];
            var slots = owner.CurrentTacticSlots?.ToArray() ?? throw LearnMissing("Person_ten_current_Tactics");
            if (slots.Length != 10) throw LearnMissing("Person_Tactic_vector_length");
            for (int index = 0; index < Math.Min(sorted.Length, 10); index++)
            {
                byte id = checked((byte)Math.Clamp(LearnNeed(sorted[index].Tactic.PhysicalSlot, "write_Tactic"), 1, 200));
                current.WritePersonTacticSlot168(person.Physical, index, id);
                slots[index] = id;
            }
            //Original only overwrites emitted entries; it does not clear a tail
            //that remains after a failed native node allocation.
            return state with { Officers = new(state.Officers) { [person.Id] = owner with { CurrentTacticSlots = slots } } };
        }
        finally { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
    }

    private GameState ApplyLearnForceExperienceCurrent(GameState state, SourceWorldContext current,
        int person, int forceId, List<CampaignEffectRequest> effects)
    {
        var force = current.ReadForceTableReference(forceId) ?? throw LearnMissing("experience_Force_pointer");
        int basis = LearnParameter(state, "force-experience", 100);
        int multiplier = LearnArmyParameter(state, SimulationParameterKeys.ArmyDestruction.ForceExperienceTraitMultiplier, 1);
        int trait = LearnNeed(TacticRemakeCurrentTraits.CommanderEffectSum(current, person, 76), "experience_trait76");
        int traitAmount = unchecked(basis * unchecked(trait * multiplier));
        var advantage = PersonReturnPathSource.ReadForceAdvantage(state, current, force, 6);
        int effect = LearnNeed(advantage.Value, advantage.UnresolvedOwner ?? "experience_Force_effect6");
        int policyScale = LearnArmyParameter(state, SimulationParameterKeys.ArmyDestruction.ForceExperienceLandAdvantageMultiplier, 1);
        int amount = unchecked(basis + unchecked(policyScale * basis * effect) / 100 + traitAmount / 100);
        if (LearnNeed(current.ReadForceNativeValidity(force), "experience_Force_validity"))
        {
            byte flags = LearnNeed(state.NativeArmySimulation?.Forces.GetValueOrDefault(forceId)?.Flags12, "experience_Force12");
            if ((flags & 8) != 0) amount = unchecked(amount + unchecked(LearnArmyParameter(state,
                SimulationParameterKeys.ArmyDestruction.ForceExperienceFlagBonusPercent, 10) * basis) / 100);
        }
        if (!LearnNeed(current.ReadForceNativeValidity(force), "experience_writer_Force_validity") || amount < 0) return state;
        _force164Inputs.BindCurrent(state, current);
        int before = LearnNeed(current.ReadForce164(force), "experience_Force164");
        int oldLevel = LearnNeed(OwnedForce164Inputs.ReadLevel(state, before), "experience_old_level");
        int after = Math.Clamp(unchecked(before + amount), 0, 9999999);
        int newLevel = LearnNeed(OwnedForce164Inputs.ReadLevel(state, after), "experience_new_level");
        var packet = new ArmyDestructionForce164Packet(current, force, requested: amount, before: before,
            after: after, oldLevel: oldLevel, newLevel: newLevel);
        ArmyDestructionForce164.ApplyContext(packet);
        state = ArmyDestructionForce164.PublishCurrent(state, packet);
        if (TalentForceIsPlayerCurrent(state, current, force))
        {
            if (amount > 0 && oldLevel != 10)
            {
                int shown = newLevel == 10 ? unchecked(LearnNeed(OwnedForce164Inputs.ReadMaximumThreshold(state), "experience_max_threshold") - before) : amount;
                if (shown > 0) effects.Add(new(null, null) { TalentForceExperiencePresentation =
                    new(TalentForceExperiencePresentationOperation.ExperienceGain, forceId, shown) { PreviousLevel = oldLevel } });
            }
            if (oldLevel < newLevel && newLevel == 10) effects.Add(new(null, null) { TalentForceExperiencePresentation =
                new(TalentForceExperiencePresentationOperation.LevelTenReached, forceId, 0) { PreviousLevel = oldLevel } });
        }
        return state;
    }

    private static TacticReference LearnPersonTactic(SourceWorldContext current, int physical, int index)
    {
        int raw = LearnNeed(current.ReadPersonTacticSlot168(physical, index), "Person_Tactic_slot");
        return current.ReadSkillTacticReference(raw <= 200 ? raw : 0) ?? throw LearnMissing("Person_Tactic_lookup");
    }
    private static bool LearnOrdinaryTacticGroup(int physical) => physical is >= 31 and <= 130 or >= 140 and <= 200;
    private static string LearnTacticName(GameState state, int physical)
        => state.Tactics.GetValueOrDefault(physical)?.Name ?? throw LearnMissing("Tactic_name");
    private static byte LearnForceFormationCurrent(GameState state, int force)
    {
        var bank = state.NativeArmySimulation?.Parameters ?? throw LearnMissing("Force_formation_bank");
        string key = BattleRemakeCurrentForcePolicies.ForceFieldKey(force, 0x15E);
        if (bank.Float32Values.ContainsKey(key) || !bank.Int32Values.TryGetValue(key, out int value) || (uint)value > 255)
            throw LearnMissing("Force_formation15E");
        return (byte)value;
    }
    private static int LearnArmyParameter(GameState state, string key, int tcValue)
    {
        var bank = state.NativeArmySimulation?.Parameters ?? throw LearnMissing("parameter_owner");
        if (bank.Float32Values.ContainsKey(key)) throw LearnMissing("parameter_type_" + key);
        if (bank.Int32Values.TryGetValue(key, out int value)) return value;
        if (bank.Language == "TC" && bank.RuleVersion == SimulationRuleIdentity.ForLocale("TC")) return tcValue;
        throw LearnMissing("parameter_" + key);
    }
    private static int LearnParameter(GameState state, string key, int tcValue)
        => LearnArmyParameter(state, "personnel-learn:" + key, tcValue);
    private static T LearnNeed<T>(T? value, string field) where T : struct => value ?? throw LearnMissing(field);
    private static InvalidDataException LearnMissing(string field) => new("personnel.learn." + field);
}

public sealed partial class SourceWorldContext
{
    internal HexListNodeReference AppendLearnTacticTemporary(HexListHandle handle, TacticReference tactic)
    {
        if (!handle.Identity.HasValue) return new(null);
        var node = AllocateHexListNode(handle);
        if (node.Identity is int identity) WriteCurrentListPayload(identity, tactic);
        return node;
    }
}
