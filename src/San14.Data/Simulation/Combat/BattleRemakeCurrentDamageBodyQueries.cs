using San14.Core;

namespace San14.Data;

/// <summary>Reached 169FC0 Army-versus-Army body queries over the current owned
/// registry and commander personality tables. Army kind27 makes the special
/// player exclusion false and bypasses the attacking-Base modifiers. The late
/// effect56 test uses the first matching commander personality and requires
/// its condition B4 to be FFFF; activation1BC and Army trait64 are not read.</summary>
public sealed class BattleRemakeCurrentDamageBodyQueries : IBattleRemakeCurrentDamageBodyQueries
{
    private readonly TacticRemakeCurrentContext current;
    private readonly SourceWorldContext definitions;
    public string Revision { get; }

    public BattleRemakeCurrentDamageBodyQueries(TacticRemakeCurrentContext currentContext,
        SourceWorldContext currentDefinitions, string revision)
    {
        current = currentContext ?? throw new ArgumentNullException(nameof(currentContext));
        definitions = currentDefinitions ?? throw new ArgumentNullException(nameof(currentDefinitions));
        if (string.IsNullOrWhiteSpace(revision)) throw new InvalidDataException("Current normal-damage body revision is missing.");
        Revision = revision;
    }

    public bool? ReadCurvePermission(GameState state, UnitState caster, UnitState target)
    {
        bool? a = ReadNativeValidity(state, caster);
        if (a is false) return false; //169FC0's first validity guard.
        if (a is null) return null;
        bool? b = ReadNativeValidity(state, target);
        if (b is not true) return b;
        //Target kind27 stores the Army receiver at16A1A5 and the zero Base
        //flag at16A205. The later r13 !=0 && r15 !=0 exclusion is false,
        //independently of the current-player lookup and its Force ID.
        return true;
    }

    private bool? ReadNativeValidity(GameState state, UnitState army)
    {
        bool? valid = current.ArmyValid(state, army);
        if (valid is not false) return valid;
        //ArmyValid also protects this remake's coherent commander deployment.
        //Failure of those extra ownership guards does not prove the native
        //233EA0 receiver invalid. Its reached leaf reads word12 and byte10.
        return army.OfficerId == 0 || army.NativeTacticRuntime?.Status10 == 0 ? false : null;
    }

    public BattleRemakeCurrentCityDamageBranch? ReadAttackingCityBranch(GameState state, UnitState caster)
    {
        if (current.ArmyValid(state, caster) != true) return null;
        //Attacker kind27 retains the actual Army pointer and leaves rsi=0;
        //16A336 checks that Base receiver and jumps directly to16A4E3.
        return new(false, null);
    }

    public bool? ReadTargetHalfDamageTrait(GameState state, UnitState target)
    {
        if (current.ArmyValid(state, target) != true ||
            state.NativeArmySimulation?.Parameters is null ||
            definitions.StaticSource is not { } source ||
            state.ContentHashes.GetValueOrDefault(source.ResourceUri) != source.Sha256 ||
            state.Officers.GetValueOrDefault(target.OfficerId) is not { } officer ||
            definitions.ResolveOfficerSource(target.OfficerId) is not int physical ||
            OfficerServiceStatusSourceAdapter.Encode(officer.ServiceStatus) is not byte status)
            return null;
        bool? valid = NativeValidity.PersonWithBehavior(physical, checked((ushort)officer.Id), officer.Behavior, status);
        if (valid is null) return null;
        if (!valid.Value) return false; //2AB540 returns a null personality receiver.
        return ReadFirstMatchingPermanentEffect(definitions, physical, 56);
    }

    /// <summary>Pure 230990 -> 2AB540 -> caller B4 predicate. Caller has proved
    /// the current Person valid. The first matching slot ends the search even
    /// when its condition is nonpermanent; a later permanent match is ignored.</summary>
    public static bool? ReadFirstMatchingPermanentEffect(SourceWorldContext currentDefinitions,
        int personPhysical, int effectCode)
    {
        ArgumentNullException.ThrowIfNull(currentDefinitions);
        if (personPhysical is < 1 or > 1850 || effectCode is < 0 or > 255)
            throw new InvalidDataException("Current commander effect lookup identity/code is outside the native finite contract.");
        if (currentDefinitions.ReadPersonIdentity(personPhysical) is not ushort identity) return null;
        bool custom = unchecked((uint)(identity - 5101)) <= 99;
        for (int index = 0; index < 9; index++)
        {
            //230990's custom branch reaches its index>=5 return before
            //reading the raw Person slot. Ordinary people first read BF.
            ushort requested = 0;
            if (!(custom && index >= 5))
            {
                if (currentDefinitions.ReadPersonTraitSlot150(personPhysical, index) is not ushort raw) return null;
                requested = raw;
            }
            bool filtered = false;
            if (!custom)
            {
                PersonalityReference? candidate = currentDefinitions.ReadPersonalityTableReference(requested <= 355 ? requested : 0);
                if (candidate is null || currentDefinitions.ReadSkillPersonalityValidity(candidate) is not bool candidateValid) return null;
                if (candidateValid)
                {
                    byte? personalityFlags = currentDefinitions.ReadSkillPersonalityFlags(candidate);
                    if (personalityFlags is null) return null;
                    filtered = (personalityFlags & 0x10) == 0;
                }
            }
            PersonalityReference? selected = currentDefinitions.ReadPersonalityTableReference(
                !filtered && index < 5 && requested <= 355 ? requested : 0);
            if (selected is null || currentDefinitions.ReadSkillPersonalityValidity(selected) is not bool valid) return null;
            if (!valid) continue;
            byte? first = currentDefinitions.ReadPersonalityByte(selected, 0xB6);
            if (first is null) return null;
            if (first != effectCode)
            {
                byte? second = currentDefinitions.ReadPersonalityByte(selected, 0xBA);
                if (second is null) return null;
                if (second != effectCode) continue;
            }
            ushort? condition = currentDefinitions.ReadPersonalityWord(selected, 0xB4);
            return condition is null ? null : condition == ushort.MaxValue;
        }
        return false;
    }
}
