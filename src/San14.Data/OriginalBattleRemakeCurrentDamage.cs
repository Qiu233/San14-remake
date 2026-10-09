using San14.Core;

namespace San14.Data;

/// <summary>Actual stage2/3/4 float slots, each direction has stride0x14.
/// These are query results, not UI base statistics or formation coefficients.</summary>
public sealed record OriginalBattleRemakeCurrentDamageAttributes(float AttackerCommon00,
    float AttackerAttack04, float AttackerDefense08, float TargetCommon14,
    float TargetAttack18, float TargetDefense1C)
{
    public void Validate()
    {
        if (new[] { AttackerCommon00, AttackerAttack04, AttackerDefense08,
            TargetCommon14, TargetAttack18, TargetDefense1C }.Any(value => !float.IsFinite(value)))
            throw new InvalidDataException("Current ordinary damage attributes contain an unresolved/nonfinite native float.");
    }
}

/// <summary>Current Army-only normal-damage queries. PermitCurve resolves the
/// validity/special-world-player guard inside169FC0. A known false is an actual
/// early zero return; unknown queries must return null, never PermitCurve=false.
/// Null city modifiers means a proved absent city branch, not a missing query.</summary>
public sealed record OriginalBattleRemakeCurrentNormalAttackInput(int AttackerId, int TargetId,
    int AttackerTroops, int TargetTroops, bool PermitCurve,
    OriginalBattleRemakeCurrentDamageAttributes? Attributes, bool TargetIsTransport,
    OriginalAttackingCityDamageSourceInput? AttackingCityModifiers, bool? TargetHalfDamageTrait,
    ulong RandomState, string Revision);

public interface IOriginalBattleRemakeCurrentDamageQueries
{
    OriginalBattleRemakeCurrentNormalAttackInput? ResolveNormalArmy(GameState state,
        UnitState caster, UnitState target);
}

/// <summary>The source's computed int32 before16A6D0 low-word storage and
/// before178C20 applied-loss cap. Ordinary damage arithmetic draws no randomness.</summary>
public sealed record OriginalBattleRemakeCurrentDamageReceipt(int NormalDamage, ulong RandomState,
    OriginalBattleRemakeCurrentDamageAttributes? Attributes, string Revision)
{
    public ushort NativeFrameWord20 => unchecked((ushort)NormalDamage);
}

/// <summary>169A40 stage5→16A570→169FC0 receipt seam. Current twelve globals
/// come only from the owned session dictionary; no query reseeds image values.
/// Target descriptor A and16FBC0 comparison remain the caller's separate stage.</summary>
public sealed class OriginalBattleRemakeCurrentDamage
{
    private readonly OriginalBattleDamageSourceParameters source;
    private readonly IOriginalBattleRemakeCurrentDamageQueries queries;
    public string Revision { get; }
    public OriginalBattleRemakeCurrentDamage(OriginalBattleDamageSourceParameters imageSource,
        IOriginalBattleRemakeCurrentDamageQueries currentQueries, string revision)
    {
        source = imageSource ?? throw new ArgumentNullException(nameof(imageSource));
        queries = currentQueries ?? throw new ArgumentNullException(nameof(currentQueries));
        if (!imageSource.IsImageInitial || string.IsNullOrWhiteSpace(revision) || imageSource.Fields.Count != 12 ||
            imageSource.Source.RuleVersion != OriginalBattleUnitSourceCodeSources.ForLanguage(imageSource.Language).RuleVersion)
            throw new InvalidDataException("Current ordinary damage needs the paired immutable source profile and a revision.");
        Revision = revision;
    }

    public OriginalBattleRemakeCurrentDamageReceipt? Resolve(GameState state, UnitState caster, UnitState target)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(caster); ArgumentNullException.ThrowIfNull(target);
        if (!Bound(state, caster, target)) return null;
        OriginalBattleRemakeCurrentNormalAttackInput? input = queries.ResolveNormalArmy(state, caster, target);
        if (input is null) return null;
        if (input.AttackerId != caster.Id || input.TargetId != target.Id || input.AttackerTroops != caster.Troops ||
            input.TargetTroops != target.Troops || input.RandomState != state.RandomState ||
            string.IsNullOrWhiteSpace(input.Revision) || input.TargetIsTransport != (target.NativeTacticRuntime!.Status10 == 2))
            throw new InvalidDataException("Current ordinary damage queries belong to another owner, troop snapshot, type or random state.");
        string revision = $"{Revision}:{state.NativeArmySimulation!.Parameters.Revision}:{input.Revision}";
        if (!input.PermitCurve)
        {
            if (input.Attributes is not null || input.AttackingCityModifiers is not null || input.TargetHalfDamageTrait is not false)
                throw new InvalidDataException("A native early-return damage guard supplied later-stage inputs.");
            return new(0, state.RandomState, null, revision);
        }
        if (input.Attributes is null) return null;
        input.Attributes.Validate();
        OriginalBattleDamageSourceParameters? current = CurrentParameters(state);
        if (current is null) return null;
        var strike = new OriginalTroopDamageSourceInput(caster.Troops, target.Troops,
            input.Attributes.AttackerAttack04, input.Attributes.TargetDefense1C,
            input.TargetIsTransport, input.AttackingCityModifiers, false);
        int amount;
        if (input.TargetHalfDamageTrait is bool half)
            amount = OriginalBattleDamageSource.CalculateTroopDamage(current, strike with { TargetHalfDamageTrait = half });
        else
        {
            //16A4E3 skips the pure commander query when the pre-half amount is
            //nonpositive. Remove only the later16A536 cap from a private pure
            //arithmetic view; no current parameter owner is edited or reseeded.
            var uncapped = current.WithCurrentValues(current.CurrentValues with { DamageCap = int.MaxValue },
                $"{revision}:pre-half-16A4E3");
            int gross = OriginalBattleDamageSource.CalculateTroopDamage(uncapped, strike);
            if (gross > 0) return null;
            amount = Math.Min(gross, current.DamageCap);
        }
        return new(amount, state.RandomState, input.Attributes with { }, revision);
    }

    public OriginalBattleDamageSourceParameters? CurrentParameters(GameState state)
    {
        if (state.NativeArmySimulation?.Parameters is not { } parameters || parameters.Language != source.Language ||
            parameters.RuleVersion != source.Source.RuleVersion || string.IsNullOrWhiteSpace(parameters.Revision)) return null;
        var current = new int[12];
        for (int index = 0; index < current.Length; index++)
        {
            int semantic = source.Fields[index].Rva + (source.Language == "TC" ? 0x1030 : 0);
            if (!parameters.Int32Values.TryGetValue(OriginalTacticRemakeParameterSeed.Key(semantic), out current[index])) return null;
        }
        return source.WithCurrentValues(new(current[0], current[1], current[2], current[3], current[4], current[5],
            current[6], current[7], current[8], current[9], current[10], current[11]), parameters.Revision);
    }

    private bool Bound(GameState state, UnitState caster, UnitState target) =>
        state.NativeClock is not null && state.NativeArmySimulation?.Parameters is { } parameters &&
        parameters.Language == source.Language && parameters.RuleVersion == source.Source.RuleVersion &&
        state.Units.GetValueOrDefault(caster.Id) == caster && state.Units.GetValueOrDefault(target.Id) == target &&
        caster.NativeArmyOrigin is { } a && target.NativeArmyOrigin is { } b &&
        a.Language == source.Language && b.Language == source.Language && a.RuleVersion == source.Source.RuleVersion &&
        b.RuleVersion == source.Source.RuleVersion && a.NativeSlotIndex is >= 1 and <= 500 && b.NativeSlotIndex is >= 1 and <= 500 &&
        caster.NativeTacticRuntime is not null && target.NativeTacticRuntime is not null &&
        caster.Troops is >= 0 and <= ushort.MaxValue && target.Troops is >= 0 and <= ushort.MaxValue;
}
