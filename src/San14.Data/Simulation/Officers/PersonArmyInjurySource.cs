using San14.Core;

namespace San14.Data;

public enum PersonArmyInjuryOutcome { NativeSkip, NotInjured, Injured, CurrentInputsUnresolved }
public sealed record PersonArmyInjuryReceipt(int PersonId, int BeforeHealthStateId, int AfterHealthStateId,
    int? Sample, int? Probability, int WinnerEffect52, int OwnEffect2F, int TemporaryPersonalityCount,
    int? TemporaryDescriptor, bool PlayerDistrictNotice, int? ArmyNoticeKind, int? ArmyNoticePhysical);
public sealed record PersonArmyInjuryResult(PersonArmyInjuryOutcome Outcome,
    PersonArmyInjuryReceipt? Receipt, string? UnresolvedOwner)
{
    //27D900's healthy injury branch returns null even when its health byte changes.
    public int? NativeReturn => Outcome == PersonArmyInjuryOutcome.CurrentInputsUnresolved ? null : 0;
}

/// <summary>Ordinary type1 TC27D900 over the actual writable current context.
/// It borrows the shared arena; the enclosing259360 publishes that arena once.
/// Types2/3 share this operation and have no battle-death prefix. Unknown/death
/// paths require the caller to discard its operation-local world.
/// This is not a captivity or death callback and does not use a process.</summary>
public static class PersonArmyInjurySource
{
    private enum InjuryMode { OrdinaryBattle, ForcedCapture, Duel }

    public static PersonArmyInjuryResult Apply(GameState state, SourceWorldContext current,
        int personPhysical, TransferPersonReference winnerPerson, IEffectiveOfficerAbilities abilities,
        TacticSourceSamplerWorld? samplerWorld)
        => ApplyCore(state, current, personPhysical, winnerPerson, abilities, samplerWorld, InjuryMode.OrdinaryBattle);

    /// <summary>Actual27D900 type3. Its battle-death prefix is inapplicable;
    /// health2 can worsen to3, and an Army injury notice has kind13.</summary>
    public static PersonArmyInjuryResult ApplyDuel(GameState state, SourceWorldContext current,
        int personPhysical, TransferPersonReference winnerPerson, IEffectiveOfficerAbilities abilities,
        TacticSourceSamplerWorld? samplerWorld)
        => ApplyCore(state, current, personPhysical, winnerPerson, abilities, samplerWorld, InjuryMode.Duel);

    /// <summary>Actual27D900 type2 during forced capture. Special-person
    /// bit14 prevents this injury before tutorial checks or temporary allocation.
    /// Otherwise its nonlethal injury path shares the same health0..3 rules;
    /// an Army injury notice remains kind12, independently of the duel entry.</summary>
    public static PersonArmyInjuryResult ApplyForcedCapture(GameState state, SourceWorldContext current,
        int personPhysical, TransferPersonReference winnerPerson, IEffectiveOfficerAbilities abilities,
        TacticSourceSamplerWorld? samplerWorld)
        => ApplyCore(state, current, personPhysical, winnerPerson, abilities, samplerWorld, InjuryMode.ForcedCapture);

    private static PersonArmyInjuryResult ApplyCore(GameState state, SourceWorldContext current,
        int personPhysical, TransferPersonReference winnerPerson, IEffectiveOfficerAbilities abilities,
        TacticSourceSamplerWorld? samplerWorld, InjuryMode mode)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(winnerPerson); ArgumentNullException.ThrowIfNull(abilities);
        int count = 0; int? descriptor = null;
        if (personPhysical is < 1 or > 1850 || current.ReadPersonIdentity(personPhysical) is not ushort id ||
            !state.Officers.TryGetValue(id, out var person) || current.ResolveOfficerSource(id) != personPhysical ||
            current.StaticSource is not { } source || state.ContentHashes.GetValueOrDefault(source.ResourceUri) != source.Sha256 ||
            OfficerHealthSourceAdapter.ReadByte(current, personPhysical) is not byte healthIdentity)
            return Unknown("current.Person_identity_and_health161");
        var health = new OfficerHealth(healthIdentity);
        if (BattleRemakeCurrentSettings.ReadSelectorE58(state) is not int selector ||
            BattleRemakeCurrentSettings.ReadInt32(state, selector != 0 ? 0xD0 : 0x2B4) is not int prohibited)
            return Unknown("current.Environment_3EBE70");
        if (prohibited != 0) return Skip(id, health);
        if (mode == InjuryMode.ForcedCapture)
        {
            //The existing behavior owner is the independent native15E bit14.
            //Unrelated flag bits are not consumed by this short-circuit gate.
            if (OfficerBehaviorRules.UsesOrdinaryPersonRules(current.ReadPersonBehavior(personPhysical)) is not bool ordinary)
                return Unknown("current.type2_Person15E_bit14");
            if (!ordinary) return Skip(id, health);
        }
        if (state.NativeArmySimulation?.Parameters.Int32Values is not { } integers ||
            integers.GetValueOrDefault(TacticRemakeCurrentBattleControl.VersionKey) != 1 ||
            !integers.TryGetValue(TacticRemakeCurrentBattleControl.HeaderKey, out int tutorial))
            return Unknown("current.Tutorial_header0");
        if (tutorial != 0) return Skip(id, health);
        var pool = current.ReadOwnedCurrentListPool();
        if (pool is null || !pool.OwnsArmyReturnProfile()) return Unknown("current.owned_primary_shared_pool");

        //2307D0 clears the actual descriptor, appends in native selector order,
        //and scans only nodes that really allocated. Its common destructor keeps
        //free-chain order and retained payloads in the primary saved allocator.
        HexListHandle temporary = current.CreatePersonInjuryTemporary();
        descriptor = temporary.Identity;
        try
        {
            current.ClearPersonInjuryTemporary(temporary, false);
            for (int index = 0; index < 9; index++)
            {
                var trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(current, personPhysical, index);
                if (trait is null || current.ReadSkillPersonalityValidity(trait) is not bool valid)
                    return Unknown("current.230730_Person_personality");
                if (valid) current.AppendPersonInjuryTemporary(temporary, trait);
            }
            if (current.ReadTemporaryHexListCount(temporary) is not int actualCount)
                return Unknown("current.2307D0_temporary_count");
            count = actualCount;
            var node = current.ReadSkillTemporaryHead(temporary);
            if (node is null) return Unknown("current.2307D0_temporary_head");
            while (node.Identity.HasValue)
            {
                var trait = current.ReadSkillTemporaryPayload(node);
                if (trait is null || FirstEffect(current, trait, 0x35) is not int immunity)
                    return Unknown("current.Personality_effect35");
                if (immunity > 0)
                {
                    if (Radius(state, current, trait) is not int radius) return Unknown("current.Personality_22C4E0");
                    if (radius == 0) return Skip(id, health);
                }
                node = current.ReadHexListNodeNext(node);
                if (node is null) return Unknown("current.2307D0_temporary_next");
            }

            var ownArmy = PersonArmy(current, personPhysical);
            if (ownArmy is null || current.ReadArmyUnitNativeValidity(ownArmy) is not bool ownValid)
                return Unknown("current.Person_22A200_Army");
            if (ownValid)
            {
                if (CachedArmyHasEffect(current, ownArmy, 0x35) is not bool immune)
                    return Unknown("current.Army_cached36_effect35");
                if (immune) return Skip(id, health);
            }
            //The original middle section can invoke the full295EE0 death and
            //succession callback for severe injuries. This module owns no such
            //callback; it must not report those receivers as a successful skip.
            if (mode != InjuryMode.OrdinaryBattle)
            {
                //Only type1 enters the earlier death branch. Types2/3 really
                //return unchanged at health3 AFTER the same trait temporary.
                if (health.StateId == 3) return Skip(id, health);
                if (health.StateId > 3) return Unknown(mode == InjuryMode.Duel
                    ? "current.type3_health_identity" : "current.type2_health_identity");
            }
            else if (!OfficerHealthRules.SupportsOrdinaryBattleInjury(health))
                return Unknown("branch.health2_or3_death_and_severe_injury");
            if (samplerWorld is null) return Unknown("current.operation_World450_45C");
            if (SpecialForce(current, personPhysical) is not bool special)
                return Unknown("current.Person_232A50_special_force");
            bool healthy = !OfficerHealthRules.IsInjured(health);
            string baselineKey = special
                ? healthy ? SimulationParameterKeys.ArmyDestruction.SpecialForceHealthyInjuryBase
                    : health.StateId == 2 ? SimulationParameterKeys.ArmyDestruction.SpecialForceSevereInjuryBase
                    : SimulationParameterKeys.ArmyDestruction.SpecialForceInjuredInjuryBase
                : healthy ? SimulationParameterKeys.ArmyDestruction.OrdinaryForceHealthyInjuryBase
                    : health.StateId == 2 ? SimulationParameterKeys.ArmyDestruction.OrdinaryForceSevereInjuryBase
                    : SimulationParameterKeys.ArmyDestruction.OrdinaryForceInjuredInjuryBase;
            string divisorKey = special
                ? healthy ? SimulationParameterKeys.ArmyDestruction.SpecialForceHealthyAbilityDivisor
                    : health.StateId == 2 ? SimulationParameterKeys.ArmyDestruction.SpecialForceSevereAbilityDivisor
                    : SimulationParameterKeys.ArmyDestruction.SpecialForceInjuredAbilityDivisor
                : healthy ? SimulationParameterKeys.ArmyDestruction.OrdinaryForceHealthyAbilityDivisor
                    : health.StateId == 2 ? SimulationParameterKeys.ArmyDestruction.OrdinaryForceSevereAbilityDivisor
                    : SimulationParameterKeys.ArmyDestruction.OrdinaryForceInjuredAbilityDivisor;
            //A preceding source callback in this same uncommitted operation
            //may already have changed health. The effective-ability adapter
            //consumes the saved-field view, so give it the actual current byte.
            var abilityState = person.Health == health ? state : state with { Officers = new(state.Officers)
                { [id] = person with { Health = health } } };
            if (ArmyDestructionParameterSeed.ReadCurrent(state, baselineKey) is not int baseline ||
                ArmyDestructionParameterSeed.ReadCurrent(state, divisorKey) is not int divisor ||
                divisor == 0 || abilities.Resolve(abilityState, id) is not { } effective)
                return Unknown("current.healthy_injury_parameters_and_22FE40_abilities");
            int probability = unchecked(baseline - Math.Max(effective.Intelligence, effective.Strength) / divisor);
            if (BattleRemakeCurrentSettings.ReadSelectorE58(state) is not int difficultySelector ||
                BattleRemakeCurrentSettings.ReadInt32(state, difficultySelector == 0 ? 0x134 : 0x28) is not int difficulty)
                return Unknown("current.Environment_difficulty_3EB240");
            if (difficulty > 2)
            {
                var force = current.ReadPersonForce(personPhysical);
                if (force is null || current.ReadForceNativeValidity(force) is not bool forceValid)
                    return Unknown("current.Person_Force_validity");
                if (forceValid)
                {
                    var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
                    if (player is null || player.PhysicalSlot is not int playerPhysical)
                        return Unknown("current.World_player_Force3A");
                    if (playerPhysical != 0 && playerPhysical == force.PhysicalSlot)
                    {
                        if (ArmyDestructionParameterSeed.ReadCurrent(state, SimulationParameterKeys.ArmyDestruction.HighDifficultyPlayerInjuryPercent) is not int percent)
                            return Unknown("current.player_injury_multiplier");
                        probability = unchecked(probability * percent) / 100;
                    }
                }
            }
            int winnerEffect = 0, ownEffect = 0;
            if (winnerPerson.PhysicalSlot is int winnerPhysical)
            {
                if (current.ReadPersonNativeValidity(winnerPhysical) is not bool winnerValid)
                    return Unknown("current.winner_Person_validity");
                if (winnerValid)
                {
                    var winnerArmy = PersonArmy(current, winnerPhysical);
                    if (winnerArmy is null || current.ReadArmyUnitNativeValidity(winnerArmy) is not bool winnerArmyValid)
                        return Unknown("current.winner_22A200_Army");
                    if (winnerArmyValid)
                    {
                        if (ArmyEffect(current, winnerArmy, 0x52) is not int value)
                            return Unknown("current.winner_Army_effect52");
                        winnerEffect = value;
                    }
                }
            }
            if (ownValid)
            {
                if (ArmyEffect(current, ownArmy, 0x2F) is not int value)
                    return Unknown("current.defeated_Army_effect2F");
                ownEffect = value;
            }
            probability = unchecked(probability - ownEffect + unchecked(winnerEffect * 3));
            int sample = TacticEffectSourceSamplerMath.Sample(100, 1, id, 0, 0, 0, samplerWorld);
            bool hit = sample < probability; //3FCA30: no clamp in the healthy branch.
            OfficerHealth after = hit ? mode != InjuryMode.OrdinaryBattle
                ? OfficerHealthRules.WorsenNonlethalBattleInjury(health)
                : OfficerHealthRules.WorsenOrdinaryBattleInjury(health) : health;
            bool playerNotice = false;
            if (hit)
            {
                if (PlayerDistrict(state, current, personPhysical) is not bool notice)
                    return Unknown("current.233A90_player_District_notice");
                playerNotice = notice;
                current.SetOfficerHealth(id, after);
            }
            return new(hit ? PersonArmyInjuryOutcome.Injured : PersonArmyInjuryOutcome.NotInjured,
                new(id, health.StateId, after.StateId, sample, probability, winnerEffect, ownEffect, count, descriptor,
                    playerNotice, hit && ownValid ? mode == InjuryMode.Duel ? 13 : 12 : null,
                    hit && ownValid ? ownArmy.PhysicalSlot : null), null);
        }
        finally { current.ClearPersonInjuryTemporary(temporary, true); }

        PersonArmyInjuryResult Unknown(string owner) => new(PersonArmyInjuryOutcome.CurrentInputsUnresolved, null, owner);
        PersonArmyInjuryResult Skip(int identity, OfficerHealth before) => new(PersonArmyInjuryOutcome.NativeSkip,
            new(identity, before.StateId, before.StateId, null, null, 0, 0, count, descriptor, false, null, null), null);
    }

    private static ArmyUnitReference? PersonArmy(SourceWorldContext current, int physical)
    {
        if (current.ReadPersonSecondLocation(physical) is not ushort second || current.ReadPersonIdentity(physical) is not ushort id)
            return null;
        int slot = second is >= 62 and <= 561 ? second - 61 : 0;
        var army = current.ReadArmyUnitTableReference(slot);
        if (army is null || current.ReadArmyUnitCommanderId(army) is not ushort commander) return null;
        return commander == id ? army : current.ReadArmyUnitTableReference(0);
    }
    private static bool? SpecialForce(SourceWorldContext current, int physical)
    {
        if (current.ReadPersonFirstLocation(physical) is not ushort home) return null;
        if (home is < 1 or > 61) return false;
        return current.ReadPersonForce(physical)?.PhysicalSlot is int force ? force is >= 46 and <= 50 : null;
    }
    private static int? ArmyEffect(SourceWorldContext current, ArmyUnitReference army, int effect)
    {
        if (current.ReadArmyUnitCommanderId(army) is not ushort commander) return null;
        var person = current.ReadPersonLookupReference(commander <= 6000 ? commander : 0);
        return person?.PhysicalSlot is int physical && TacticRemakeCurrentTraits.ArmyContributions(current, physical,
            current.ReadArmyReturnTraitFields(army)?.Traits64) is { } values ? values[effect] : null;
    }
    private static bool? CachedArmyHasEffect(SourceWorldContext current, ArmyUnitReference army, byte effect)
    {
        if (current.ReadArmyReturnTraitFields(army)?.Traits64 is not { Count: 36 } traits) return null;
        foreach (ushort requested in traits)
        {
            //2C8DA0 explicitly ignores index0 and overflow before validity.
            if (requested is < 1 or > 355) continue;
            var reference = current.ReadPersonalityTableReference(requested);
            if (reference is null || current.ReadSkillPersonalityValidity(reference) is not bool valid) return null;
            if (!valid) continue;
            if (current.ReadPersonalityByte(reference, 0xB6) is not byte first) return null;
            if (first == effect) return true;
            if (current.ReadPersonalityByte(reference, 0xBA) is not byte second) return null;
            if (second == effect) return true;
        }
        return false;
    }
    private static int? FirstEffect(SourceWorldContext current, PersonalityReference trait, byte effect)
    {
        for (int pair = 0; pair < 2; pair++)
        {
            if (current.ReadPersonalityByte(trait, pair == 0 ? 0xB6 : 0xBA) is not byte code) return null;
            if (code != effect) continue;
            return current.ReadPersonalityWord(trait, pair == 0 ? 0xB8 : 0xBC) is ushort raw
                ? Math.Clamp((int)unchecked((short)raw), -100, 2000) : null;
        }
        return 0;
    }
    private static int? Radius(GameState state, SourceWorldContext current, PersonalityReference trait)
    {
        if (current.ReadPersonalityByte(trait, 0xA4) is not byte kind) return null;
        if (kind is 2 or 3 or 0x17 or 0x18) return 1;
        if (kind is not (9 or 10 or 0x15 or 0x16 or 0x1F or 0x20 or 0x27 or 0x28)) return 0;
        if (trait.PhysicalSlot is >= 256 and <= 355)
        {
            int value = 0;
            for (int i = 0; i < 4; i++)
            {
                if (current.ReadPersonalityByte(trait, 0xA8 + i) is not byte part) return null;
                value |= part << (i * 8);
            }
            if (value >= 1) return value;
        }
        return ArmyDestructionParameterSeed.ReadCurrent(state, SimulationParameterKeys.ArmyDestruction.PersonalityDefaultRadius);
    }
    private static bool? PlayerDistrict(GameState state, SourceWorldContext current, int personPhysical)
    {
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
        if (player is null || current.ReadForceNativeValidity(player) is not bool valid) return null;
        if (!valid) return false;
        if (current.ReadPersonDistrictSlot(personPhysical) is not byte raw) return null;
        var district = current.ReadDistrictTableReference(raw <= 51 ? raw : 0);
        if (district?.PhysicalSlot is not int physical || current.ReadDistrictForceId(physical) is not byte force) return null;
        if (force != player.PhysicalSlot) return false;
        if (current.ReadDistrictLeader(physical) is not ushort leader) return null;
        var person = current.ReadPersonLookupReference(leader <= 6000 ? leader : 0);
        if (person?.PhysicalSlot is not int leaderPhysical || current.ReadPersonNativeValidity(leaderPhysical) is not bool leaderValid) return null;
        if (!leaderValid) return false;
        return current.ReadPersonStatus(leaderPhysical) is byte status ? status == 1 : null;
    }
}

public sealed partial class SourceWorldContext
{
    internal HexListHandle CreatePersonInjuryTemporary()
    {
        ConditionalRegistryEnter();
        try { return CreateTemporaryHexList() ?? throw new InvalidDataException("Current personality temporary allocator is unknown."); }
        finally { ConditionalRegistryExit(); }
    }
    internal void AppendPersonInjuryTemporary(HexListHandle list, PersonalityReference trait)
    {
        ConditionalRegistryEnter();
        try { _ = AppendSkillTemporaryPersonality(list, trait); }
        finally { ConditionalRegistryExit(); }
    }
    internal void ClearPersonInjuryTemporary(HexListHandle list, bool release)
    {
        ConditionalRegistryEnter();
        try { ClearTemporaryHexList(list); if (release) ReleaseTemporaryHexList(list); }
        finally { ConditionalRegistryExit(); }
    }
}
