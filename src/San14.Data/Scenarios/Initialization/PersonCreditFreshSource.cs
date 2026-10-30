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
        SourceWorldContext current, IReadOnlyList<int>? resetIdentities = null)
    {
        // The local result comes from the actual3361D0 reset consumer. It is
        // neither a saved readiness marker nor a reconstruction while loading.
        var reset = resetIdentities?.ToHashSet();
        bool FreshCredit(OfficerState person) => reset?.Contains(person.Id) == true
            ? person.MeritState is { Merit: 0, PendingMerit: 0, RankStageId: 0,
                FirstSelectionCount: 0, SecondSelectionCount: 0, ResetCode: 0 }
            : person.MeritState is { Merit: null, PendingMerit: null,
                FirstSelectionCount: null, SecondSelectionCount: null, ResetCode: null };
        if (state.NativeArmySimulation?.Parameters is not { } parameters ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            source.Metadata.FormatMarker != 0x66 || source.Snapshot.Locale != parameters.Language ||
            state.ContentHashes.GetValueOrDefault(source.ScenarioSource.ResourceUri) != source.ScenarioSource.Sha256 ||
            state.ContentHashes.GetValueOrDefault(source.FixedDataSource.ResourceUri) != source.FixedDataSource.Sha256 ||
            source.Snapshot.CreditRankParameters is not { FormatMarker: 0x66, Ranks.Count: 7 } ranks ||
            state.Officers.Values.Any(person => !FreshCredit(person)))
            throw new InvalidDataException("Fresh credit cannot replace a saved or incomplete CE Person owner.");

        //The formal fresh caller has completed3361D0 then335E90 on this same
        //current table. Legacy direct callers can still prove an inactive
        //female prefix; an unknown current setting never assumes that prefix.
        int? selector = BattleRemakeCurrentSettings.ReadSelectorE58(state);
        int? meritChoice = selector == 1 ? BattleRemakeCurrentSettings.ReadInt32(state, 0x120) : null;
        int? surrenderChoice = selector == 1 ? BattleRemakeCurrentSettings.ReadInt32(state, 0x128) : null;
        bool prefixKnown = meritChoice is 0 or 1 && surrenderChoice is 0 or 1 &&
            (resetIdentities is not null || BattleRemakeCurrentSettings.ReadByte(state, 0x78) == 0);
        var officers = new Dictionary<int, OfficerState>(state.Officers);
        foreach (OfficerState person in state.Officers.Values)
        {
            if (current.StaticSource?.ResourceUri != source.ScenarioSource.ResourceUri ||
                current.StaticSource.Sha256 != source.ScenarioSource.Sha256 ||
                current.ResolveOfficerSource(person.Id) is not int physical)
                throw new InvalidDataException("Fresh credit has no complete current Person identity.");

            OfficerMeritState credit = new(null, null, person.MeritState!.RankStageId, null, null, null);
            CreditState? after = null;
            CharacterPreviewRecord row = source.Snapshot.Characters[physical];
            byte? status = OfficerServiceStatusSourceAdapter.Encode(person.ServiceStatus);
            if (prefixKnown && row.CharacterId == person.Id && status is byte currentStatus &&
                current.ReadPersonStatus(physical) == currentStatus &&
                current.ReadPhysicalPerson(physical) is { } captured &&
                captured.RawFlags196 == OfficerBehaviorSourceAdapter.EncodeFlags(person.Behavior) &&
                captured.RawFlag1FD == OfficerBehaviorSourceAdapter.EncodeMarker(person.Behavior) &&
                Eligibility(person, physical, current) is bool eligible)
            {
                CreditState? before = null;
                if (reset?.Contains(person.Id) == true)
                {
                    //226480 reset the reached person's actual credit fields.
                    //3361D0 then changed status to9;3255D0 consumes that status,
                    //not the old archive status or its old appendix values.
                    before = new(0, 0, 0, 0, 0, 0, captured.RawFlag1FD);
                }
                else if (row.CreditReference is { } appendix && AppendixMatches(appendix) &&
                    OfficerMeritSourceAdapter.EncodeStage(person.MeritState) == appendix.RawStageCode)
                {
                    //The governor repair may change another person's status.
                    //The untouched raw credit appendix still supplies its
                    //pre-credit values, while the tail is the current owner.
                    before = CreditInitialization.ReadSource(appendix, 0) with { RawTailCode = captured.RawFlag1FD };
                }
                if (before is not null)
                {
                    after = CreditInitialization.Apply(before, eligible, currentStatus,
                        new(meritChoice == 0, surrenderChoice == 0), ranks);
                    credit = OfficerMeritSourceAdapter.Decode(after.Merit, after.RawValue1, after.Stage,
                        after.FirstSelectionCount, after.SecondSelectionCount, after.RawResetCode);
                }
            }
            officers[person.Id] = person with
            {
                MeritState = credit,
                Behavior = after is null ? person.Behavior : person.Behavior!.WithSpecialValidityMarker(after.RawTailCode)
            };
        }
        return state with { Officers = officers };
    }

    private static bool? Eligibility(OfficerState person, int personPhysical,
        SourceWorldContext current)
    {
        if (current.ReadPersonNativeValidity(personPhysical) is not bool valid ||
            OfficerServiceStatusSourceAdapter.Encode(person.ServiceStatus) is not byte status || OfficerBehaviorSourceAdapter.EncodeFlags(person.Behavior) is not ushort flags) return null;
        if (!valid || status == 1) return false;
        bool band = new[] { 4111, 4131, 4151, 4171, 4191 }
            .Any(start => unchecked((uint)(person.Id - start)) <= 9);
        var force = current.ReadPersonForce(personPhysical);
        if (force?.PhysicalSlot is not int physical || physical is < 0 or > 51 ||
            person.ForceId != (physical == 0 ? null : (int?)physical)) return null;
        if (band)
        {
            if (current.ReadPersonFirstLocation(personPhysical) is not ushort location) return null;
            if (location is >= 1 and <= 61 && physical is not (>= 46 and <= 50)) return false;
        }
        if (physical is >= 46 and <= 50 || OfficerBehaviorRules.UsesOrdinaryPersonRules(person.Behavior) == false) return false;
        return true;
    }

    private static bool AppendixMatches(S14CharacterCreditReference value) => value.RawBytes is { Length: 9 } raw &&
        value.RawFlagsWord is not null && value.RawSelectionCodes is { Count: 3 } choices &&
        raw[0] == value.RawStageCode && BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(1, 2)) == value.RawValue0 &&
        BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(3, 2)) == value.RawValue1 &&
        raw.AsSpan(5, 3).SequenceEqual(choices.ToArray()) && raw[8] == value.RawTailCode;
}
