using San14.Core;

namespace San14.Data;

/// <summary>Resolved results of the attacking city's native policy21, eligible
/// personnel44 and city-query3 calls. Null at the outer boundary is an explicitly
/// absent city branch, and must never be used for an unresolved query.</summary>
public sealed record AttackingCityDamageSourceInput(int Policy21Percent,
    IReadOnlyList<int> EligiblePerson44Percent, int CityQuery3Percent);

/// <summary>One directional native common troop-damage calculation. Attack and
/// defense are final native query results, not formation source coefficients.</summary>
public sealed record TroopDamageSourceInput(int AttackerTroops, int TargetTroops,
    float AttackerAttack, float TargetDefense, bool TargetIsTransport,
    AttackingCityDamageSourceInput? AttackingCityModifiers, bool TargetHalfDamageTrait);

/// <summary>One native Object durability strike, after eligibility was resolved.
/// DefinitionCategory is the native definition byte+2C. Person103 and the two
/// signed addends are current native query/global values. A null challenge value
/// means an explicitly inactive challenge branch; an active branch needs the
/// already resolved result of native516780.</summary>
public sealed record WallDamageSourceInput(int CurrentDurability, float FinalWallAttack,
    bool AttackerPresent, byte ObjectDefinitionCategory, int ForcePolicy45Percent,
    int Person103Percent, int PositivePerson103Addend, int NegativePerson103Subtractand,
    bool PermitZeroDurability, int? ChallengeAdjustedDamage);

public sealed record BattleDamageSourceField(string Key, int Value);

/// <summary>All twelve current native parameter values, explicitly resolved by
/// a runtime context. No omitted value is filled from the rule defaults.</summary>
public sealed record BattleDamageCurrentValues(int AttackOffset,int AttackSquareDivisor,
    int TroopSumDivisor,int TroopSumOffset,int DamageDivisor,int TransportTargetMultiplier,
    int CityPolicyMultiplier,int CityPersonMultiplier,int CityQueryMultiplier,
    int HalfDamageDivisor,int DamageCap,int WallPersonMultiplier);

/// <summary>Project-owned CE-compatible initial damage parameters and
/// rule defaults. Current runtime writes need a separate explicit owner.</summary>
public sealed class BattleDamageSourceParameters
{
    public string Language { get; }
    public SourceProvenance Source { get; }
    public IReadOnlyList<BattleDamageSourceField> Fields { get; }
    public string? CurrentRevision { get; }
    public bool IsImageInitial { get; }
    public BattleDamageCurrentValues CurrentValues { get; }
    public int AttackOffset { get; }
    public int AttackSquareDivisor { get; }
    public int TroopSumDivisor { get; }
    public int TroopSumOffset { get; }
    public int DamageDivisor { get; }
    public int TransportTargetMultiplier { get; }
    public int CityPolicyMultiplier { get; }
    public int CityPersonMultiplier { get; }
    public int CityQueryMultiplier { get; }
    public int HalfDamageDivisor { get; }
    public int DamageCap { get; }
    public int WallPersonMultiplier { get; }

    internal BattleDamageSourceParameters(string language, SourceProvenance source,
        BattleDamageSourceField[] fields,
        BattleDamageCurrentValues? currentValues=null,string? currentRevision=null)
    {
        Language=language; Source=source;
        Fields=Array.AsReadOnly(fields);
        IsImageInitial=currentValues is null;
        CurrentRevision=currentRevision;
        var values=currentValues??new(fields[0].Value,fields[1].Value,fields[2].Value,
            fields[3].Value,fields[4].Value,fields[5].Value,fields[6].Value,fields[7].Value,
            fields[8].Value,fields[9].Value,fields[10].Value,fields[11].Value);
        CurrentValues=values;
        AttackOffset=values.AttackOffset; AttackSquareDivisor=values.AttackSquareDivisor;
        TroopSumDivisor=values.TroopSumDivisor; TroopSumOffset=values.TroopSumOffset;
        DamageDivisor=values.DamageDivisor; TransportTargetMultiplier=values.TransportTargetMultiplier;
        CityPolicyMultiplier=values.CityPolicyMultiplier; CityPersonMultiplier=values.CityPersonMultiplier;
        CityQueryMultiplier=values.CityQueryMultiplier; HalfDamageDivisor=values.HalfDamageDivisor;
        DamageCap=values.DamageCap; WallPersonMultiplier=values.WallPersonMultiplier;
    }

    public BattleDamageSourceParameters WithCurrentValues(
        BattleDamageCurrentValues values,string currentRevision)
    {
        ArgumentNullException.ThrowIfNull(values);
        if(string.IsNullOrWhiteSpace(currentRevision))
            throw new InvalidDataException("Current battle parameters need an explicit revision.");
        return new(Language,Source,Fields.ToArray(),values,currentRevision);
    }
}
/// <summary>Independent C# transcription of current CE common troop damage and
/// Object durability damage. Does not execute native code, select combat targets,
/// calculate final abilities, draw random numbers or decide capture.</summary>
public static class BattleDamageSource
{
    public static BattleDamageSourceParameters LoadParameters(
        GameResourceRepository repository, string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _ = SimulationRuleIdentity.ForLocale(language);
        var fields = ParameterFields.Select(field => new BattleDamageSourceField(
            field.Key, field.Value)).ToArray();
        return new(language, SimulationRuleSources.Provenance(
            "san14-remake://rules/battle-damage/v1", language), fields);
    }

    public static int CalculateTroopDamage(BattleDamageSourceParameters parameters,
        TroopDamageSourceInput input)
    {
        ArgumentNullException.ThrowIfNull(parameters); ArgumentNullException.ThrowIfNull(input);
        ValidateCurrentParameters(parameters);
        if(input.AttackerTroops<0 || input.TargetTroops<0 ||
            !float.IsFinite(input.AttackerAttack) || !float.IsFinite(input.TargetDefense))
            throw new InvalidDataException("Native common damage needs nonnegative troop counts and finite final attributes.");
        // Preserve separate scalar single-precision operations in native order.
        float difference=(float)parameters.AttackOffset+input.AttackerAttack;
        difference=difference-input.TargetDefense;
        difference=MathF.Max(1f,difference);
        float squared=difference*difference;
        squared=squared/(float)parameters.AttackSquareDivisor;
        squared=MathF.Max(1f,squared);
        float amount=(float)unchecked(input.AttackerTroops+input.TargetTroops);
        amount=amount/(float)parameters.TroopSumDivisor;
        amount=amount+(float)parameters.TroopSumOffset;
        amount=amount*squared;
        amount=amount/(float)parameters.DamageDivisor;
        amount=(input.TargetIsTransport?(float)parameters.TransportTargetMultiplier:1f)*amount;
        int damage=Math.Min(TruncateNative(amount),input.TargetTroops);
        if(input.AttackingCityModifiers is { } city)
        {
            ArgumentNullException.ThrowIfNull(city.EligiblePerson44Percent);
            int policy=PercentProduct(damage,parameters.CityPolicyMultiplier,city.Policy21Percent);
            int positive=1,negative=-1;
            foreach(int value in city.EligiblePerson44Percent)
            {
                int effect=PercentProduct(damage,parameters.CityPersonMultiplier,value);
                if(value>0)positive=Math.Max(positive,effect);
                //TC16A6D4..6EA uses the same signed max in the nonpositive
                //raw-percent group. Its initial selector value remains-1.
                else negative=Math.Max(negative,effect);
            }
            int selected=unchecked(positive+negative)>0?positive:negative;
            int cityQuery=PercentProduct(damage,parameters.CityQueryMultiplier,city.CityQuery3Percent);
            damage=Math.Max(0,unchecked(damage+selected+cityQuery+policy));
        }
        if(damage>0 && input.TargetHalfDamageTrait)
            damage=unchecked(damage-damage/parameters.HalfDamageDivisor);
        return Math.Min(damage,parameters.DamageCap);
    }

    /// <summary>The native178C20 application then limits its computed amount to
    /// current Army+16 or Base+3C. Wounded troops are a separate later channel.</summary>
    public static int CalculateAppliedTroopLoss(BattleDamageSourceParameters parameters,
        TroopDamageSourceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Math.Min(input.TargetTroops,CalculateTroopDamage(parameters,input));
    }

    public static int CalculateWallDamage(BattleDamageSourceParameters parameters,
        WallDamageSourceInput input)
    {
        ArgumentNullException.ThrowIfNull(parameters); ArgumentNullException.ThrowIfNull(input);
        ValidateCurrentParameters(parameters);
        if(input.CurrentDurability is <0 or >ushort.MaxValue ||
            !float.IsFinite(input.FinalWallAttack) || input.FinalWallAttack<0 ||
            (input.AttackerPresent && !input.PermitZeroDurability && input.CurrentDurability==0))
            throw new InvalidDataException("Native wall damage needs a live permitted durability target and finite wall attack.");
        int damage=CalculateWallDamageBeforeCap(parameters,input);
        int permitted=input.CurrentDurability-(input.AttackerPresent && !input.PermitZeroDurability?1:0);
        damage=Math.Min(damage,permitted);
        if(input.ChallengeAdjustedDamage is { } adjusted)
        {
            if(adjusted<0 || adjusted>permitted)
                throw new InvalidDataException("Resolved native challenge damage exceeds the applicable target durability.");
            damage=adjusted;
        }
        if(damage<0) throw new InvalidDataException("Resolved native wall modifiers produced unsupported negative applied damage.");
        return damage;
    }

    //29EC80 rewards the actor using this value before reading/capping HP14.
    //Keep that reached quantity separate from the later applied wall loss.
    internal static int CalculateWallDamageBeforeCap(BattleDamageSourceParameters parameters,
        WallDamageSourceInput input)
    {
        ArgumentNullException.ThrowIfNull(parameters); ArgumentNullException.ThrowIfNull(input);
        ValidateCurrentParameters(parameters);
        if(!float.IsFinite(input.FinalWallAttack) || input.FinalWallAttack<0)
            throw new InvalidDataException("Native wall attack must be finite and nonnegative.");
        int damage=TruncateNative(input.FinalWallAttack),policy=0;
        if(input.AttackerPresent)
        {
            if(input.ObjectDefinitionCategory is >=2 and <=4 && input.ForcePolicy45Percent>0)
                policy=Math.Max(1,PercentProduct(damage,1,input.ForcePolicy45Percent));
            if(input.ObjectDefinitionCategory==3 && input.Person103Percent!=0)
            {
                int effect=PercentProduct(damage,parameters.WallPersonMultiplier,input.Person103Percent);
                if(input.Person103Percent>0)
                    damage=unchecked(damage+Math.Max(1,effect)+input.PositivePerson103Addend+policy);
                else damage=Math.Max(1,unchecked(damage+Math.Min(-1,effect)-input.NegativePerson103Subtractand+policy));
            }
            else damage=unchecked(damage+policy);
        }
        return damage;
    }

    private static int PercentProduct(int damage,int multiplier,int value)
        =>unchecked(damage*multiplier*value)/100;

    private static void ValidateCurrentParameters(BattleDamageSourceParameters parameters)
    {
        if(parameters.AttackSquareDivisor<=0 || parameters.TroopSumDivisor<=0 ||
            parameters.DamageDivisor<=0 || parameters.HalfDamageDivisor<=0 ||
            parameters.DamageCap<0 || parameters.TransportTargetMultiplier<0)
            throw new InvalidDataException("Current native damage parameters are outside the supported arithmetic branch.");
    }

    private static int TruncateNative(float value)
    {
        // CVTTSS2SI returns INT_MIN on an invalid/out-of-range conversion. These
        // contexts need resolution instead of allowing an overflow to heal troops.
        if(!float.IsFinite(value) || value<int.MinValue || (double)value>=2147483648d)
            throw new InvalidDataException("Native scalar damage conversion is outside the supported finite int32 range.");
        return (int)value;
    }

    private const int AttackOffsetDefault = 1000;
    private const int AttackSquareDivisorDefault = 2000;
    private const int TroopSumDivisorDefault = 300;
    private const int TroopSumOffsetDefault = 300;
    private const int DamageDivisorDefault = 1500;
    private const int TransportTargetMultiplierDefault = 10;
    private const int CityPolicyMultiplierDefault = 1;
    private const int CityPersonMultiplierDefault = 1;
    private const int CityQueryMultiplierDefault = 1;
    private const int HalfDamageDivisorDefault = 2;
    private const int DamageCapDefault = 9999;
    private const int WallPersonMultiplierDefault = 1;

    private static readonly (string Key, int Value)[] ParameterFields =
    [
        (SimulationParameterKeys.BattleDamage.AttackOffset, AttackOffsetDefault),
        (SimulationParameterKeys.BattleDamage.AttackSquareDivisor, AttackSquareDivisorDefault),
        (SimulationParameterKeys.BattleDamage.TroopSumDivisor, TroopSumDivisorDefault),
        (SimulationParameterKeys.BattleDamage.TroopSumOffset, TroopSumOffsetDefault),
        (SimulationParameterKeys.BattleDamage.DamageDivisor, DamageDivisorDefault),
        (SimulationParameterKeys.BattleDamage.TransportTargetMultiplier, TransportTargetMultiplierDefault),
        (SimulationParameterKeys.BattleDamage.CityPolicyMultiplier, CityPolicyMultiplierDefault),
        (SimulationParameterKeys.BattleDamage.CityPersonMultiplier, CityPersonMultiplierDefault),
        (SimulationParameterKeys.BattleDamage.CityQueryMultiplier, CityQueryMultiplierDefault),
        (SimulationParameterKeys.BattleDamage.HalfDamageDivisor, HalfDamageDivisorDefault),
        (SimulationParameterKeys.BattleDamage.DamageCap, DamageCapDefault),
        (SimulationParameterKeys.BattleDamage.WallPersonMultiplier, WallPersonMultiplierDefault),
    ];
}
