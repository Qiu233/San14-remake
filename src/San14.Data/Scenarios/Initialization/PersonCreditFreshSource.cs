using System.Buffers.Binary;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Actual static credit appendix followed by the complete current
/// SC325810/TC3255D0 initialization step. This is a fresh owned allocation, not
/// a declaration that every original first-player event has completed.</summary>
public static class PersonCreditFreshSource
{
    public const string ProfileId = "san14:ce-owned-person-credit-fresh-20261003";

    public static GameState InstallFresh(GameState state, LoadedScenario source,
        SourceWorldContext current)
    {
        if (state.NativeArmySimulation?.Parameters is not { } parameters ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            source.Metadata.FormatMarker != 0x66 || source.Snapshot.Locale != parameters.Language ||
            state.ContentHashes.GetValueOrDefault(source.ScenarioSource.ResourceUri) != source.ScenarioSource.Sha256 ||
            state.ContentHashes.GetValueOrDefault(source.FixedDataSource.ResourceUri) != source.FixedDataSource.Sha256 ||
            source.Snapshot.CreditRankParameters is not { FormatMarker: 0x66, Ranks.Count: 7 } ranks ||
            state.Officers.Values.Any(person => person.NativeCreditRuntime is not null ||
                person.NativeGrowthRuntime is not { PendingMerit1F8: null }))
            throw new InvalidDataException("Fresh credit cannot replace a saved or incomplete CE Person owner.");

        //Current ordinary settings are explicit saved writers. The earlier
        //female reset/movement branches must not be assumed inactive from a
        //difficulty or from the source setting's usual coincidence.
        int? selector = BattleRemakeCurrentSettings.ReadSelectorE58(state);
        int? meritChoice = selector == 1 ? BattleRemakeCurrentSettings.ReadInt32(state, 0x120) : null;
        int? surrenderChoice = selector == 1 ? BattleRemakeCurrentSettings.ReadInt32(state, 0x128) : null;
        bool prefixKnown = meritChoice is 0 or 1 && surrenderChoice is 0 or 1 &&
            BattleRemakeCurrentSettings.ReadByte(state, 0x78) == 0 &&
            BattleRemakeCurrentSettings.ReadByte(state, 0x79) == 0 &&
            BattleRemakeCurrentSettings.ReadInt32(state, 0xDC) is int personal && personal != 1;
        var officers = new Dictionary<int, OfficerState>(state.Officers);
        foreach (OfficerState person in state.Officers.Values)
        {
            if (person.NativeAbilityRuntime is not { } ability || person.NativeGrowthRuntime is not { } growth ||
                ability.Revision != TacticRemakeCurrentAbilities.ProfileId ||
                ability.Language != parameters.Language || ability.RuleVersion != parameters.RuleVersion ||
                ability.SourceScenarioUri != source.ScenarioSource.ResourceUri || ability.SourceScenarioSha256 != source.ScenarioSource.Sha256 ||
                ability.PhysicalSlot is < 1 or > 1850 ||
                current.ReadPersonLookupReference(person.Id)?.PhysicalSlot != ability.PhysicalSlot ||
                current.ReadPersonIdentity(ability.PhysicalSlot) != person.Id)
                throw new InvalidDataException("Fresh credit has no complete current Person identity.");

            NativePersonCreditRuntime credit = new(ProfileId, null, null, null, null);
            CreditState? after = null;
            CharacterPreviewRecord row = source.Snapshot.Characters[ability.PhysicalSlot];
            if (prefixKnown && row.CharacterId == person.Id && row.CreditReference is { } appendix &&
                AppendixMatches(appendix) && ability.Status11E == row.StatusCode && ability.Flags196 == appendix.RawFlagsWord &&
                ability.Secondary1F4 == appendix.RawStageCode && ability.Extra1FD == appendix.RawTailCode &&
                current.ReadPersonStatus(ability.PhysicalSlot) == ability.Status11E &&
                current.ReadPhysicalPerson(ability.PhysicalSlot) is { } captured &&
                captured.RawFlags196 == ability.Flags196 && captured.RawFlag1FD == ability.Extra1FD &&
                Eligibility(person, ability, current) is bool eligible)
            {
                //226720/226480 clears dword1FC on fresh allocation. The nine
                //serialized appendix bytes do not contain this reset byte.
                var before = CreditInitialization.ReadSource(appendix, 0);
                after = CreditInitialization.Apply(before, eligible, row.StatusCode,
                    new(meritChoice == 0, surrenderChoice == 0), ranks);
                credit = new(ProfileId, after.Merit, after.FirstSelectionCount, after.SecondSelectionCount, after.RawResetCode);
            }
            officers[person.Id] = person with
            {
                NativeCreditRuntime = credit,
                NativeGrowthRuntime = growth with { PendingMerit1F8 = after?.RawValue1 },
                NativeAbilityRuntime = after is null ? ability : ability with
                    { Secondary1F4 = after.Stage, Extra1FD = after.RawTailCode }
            };
        }
        GameState result = state with { Officers = officers };
        StateValidator.Validate(result); return result;
    }

    private static bool? Eligibility(OfficerState person, NativePersonAbilityRuntime ability,
        SourceWorldContext current)
    {
        if (current.ReadPersonNativeValidity(ability.PhysicalSlot) is not bool valid ||
            ability.Status11E is not byte status || ability.Flags196 is not ushort flags) return null;
        if (!valid || status == 1) return false;
        bool band = new[] { 4111, 4131, 4151, 4171, 4191 }
            .Any(start => unchecked((uint)(person.Id - start)) <= 9);
        var force = current.ReadPersonForce(ability.PhysicalSlot);
        if (force?.PhysicalSlot is not int physical || physical is < 0 or > 51 ||
            person.ForceId != (physical == 0 ? null : (int?)physical)) return null;
        if (band)
        {
            if (current.ReadPersonFirstLocation(ability.PhysicalSlot) is not ushort location) return null;
            if (location is >= 1 and <= 61 && physical is not (>= 46 and <= 50)) return false;
        }
        if (physical is >= 46 and <= 50 || (flags & 0x4000) != 0) return false;
        return true;
    }

    private static bool AppendixMatches(S14CharacterCreditReference value) => value.RawBytes is { Length: 9 } raw &&
        value.RawFlagsWord is not null && value.RawSelectionCodes is { Count: 3 } choices &&
        raw[0] == value.RawStageCode && BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(1, 2)) == value.RawValue0 &&
        BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(3, 2)) == value.RawValue1 &&
        raw.AsSpan(5, 3).SequenceEqual(choices.ToArray()) && raw[8] == value.RawTailCode;
}
