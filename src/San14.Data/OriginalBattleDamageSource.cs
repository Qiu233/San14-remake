using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace San14.Data;

/// <summary>Resolved results of the attacking city's native policy21, eligible
/// personnel44 and city-query3 calls. Null at the outer boundary is an explicitly
/// absent city branch, and must never be used for an unresolved query.</summary>
public sealed record OriginalAttackingCityDamageSourceInput(int Policy21Percent,
    IReadOnlyList<int> EligiblePerson44Percent, int CityQuery3Percent);

/// <summary>One directional native common troop-damage calculation. Attack and
/// defense are final native query results, not formation source coefficients.</summary>
public sealed record OriginalTroopDamageSourceInput(int AttackerTroops, int TargetTroops,
    float AttackerAttack, float TargetDefense, bool TargetIsTransport,
    OriginalAttackingCityDamageSourceInput? AttackingCityModifiers, bool TargetHalfDamageTrait);

/// <summary>One native Object durability strike, after eligibility was resolved.
/// DefinitionCategory is the native definition byte+2C. Person103 and the two
/// signed addends are current native query/global values. A null challenge value
/// means an explicitly inactive challenge branch; an active branch needs the
/// already resolved result of native516780.</summary>
public sealed record OriginalWallDamageSourceInput(int CurrentDurability, float FinalWallAttack,
    bool AttackerPresent, byte ObjectDefinitionCategory, int ForcePolicy45Percent,
    int Person103Percent, int PositivePerson103Addend, int NegativePerson103Subtractand,
    bool PermitZeroDurability, int? ChallengeAdjustedDamage);

public sealed record OriginalBattleDamageSourceField(string Name, int Rva, int Value, string Sha256);
public sealed record OriginalBattleDamageSourceCodeWindow(string Name, int Rva, int Bytes, string Sha256);

/// <summary>All twelve current native parameter values, explicitly resolved by
/// a runtime context. No omitted value is filled from the executable image.</summary>
public sealed record OriginalBattleDamageCurrentValues(int AttackOffset,int AttackSquareDivisor,
    int TroopSumDivisor,int TroopSumOffset,int DamageDivisor,int TransportTargetMultiplier,
    int CityPolicyMultiplier,int CityPersonMultiplier,int CityQueryMultiplier,
    int HalfDamageDivisor,int DamageCap,int WallPersonMultiplier);

/// <summary>Inspected CE initial parameter globals read from the pinned source
/// executable. Current runtime writes, if any, need a separate explicit source.</summary>
public sealed class OriginalBattleDamageSourceParameters
{
    public string Language { get; }
    public SourceProvenance Source { get; }
    public string DecodedTextSha256 { get; }
    public IReadOnlyList<OriginalBattleDamageSourceField> Fields { get; }
    public IReadOnlyList<OriginalBattleDamageSourceCodeWindow> CodeWindows { get; }
    public string? CurrentRevision { get; }
    public bool IsImageInitial { get; }
    public OriginalBattleDamageCurrentValues CurrentValues { get; }
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

    internal OriginalBattleDamageSourceParameters(string language, SourceProvenance source,
        string decodedTextSha256, OriginalBattleDamageSourceField[] fields,
        OriginalBattleDamageSourceCodeWindow[] codeWindows,
        OriginalBattleDamageCurrentValues? currentValues=null,string? currentRevision=null)
    {
        Language=language; Source=source; DecodedTextSha256=decodedTextSha256;
        Fields=Array.AsReadOnly(fields); CodeWindows=Array.AsReadOnly(codeWindows);
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

    public OriginalBattleDamageSourceParameters WithCurrentValues(
        OriginalBattleDamageCurrentValues values,string currentRevision)
    {
        ArgumentNullException.ThrowIfNull(values);
        if(string.IsNullOrWhiteSpace(currentRevision))
            throw new InvalidDataException("Current battle parameters need an explicit revision.");
        return new(Language,Source,DecodedTextSha256,Fields.ToArray(),CodeWindows.ToArray(),values,currentRevision);
    }
}

/// <summary>Independent C# transcription of current CE common troop damage and
/// Object durability damage. Does not execute native code, select combat targets,
/// calculate final abilities, draw random numbers or decide capture.</summary>
public static class OriginalBattleDamageSource
{
    public static OriginalBattleDamageSourceParameters LoadParameters(
        OriginalGameRepository repository, string language)
    {
        ArgumentNullException.ThrowIfNull(repository);
        Profile profile=GetProfile(language);
        string relative=$"SAN14CE_{language}.exe";
        string path=Path.GetFullPath(Path.Combine(repository.SourceDirectory,relative));
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,
            128*1024,FileOptions.SequentialScan);
        if(stream.Length is <4096 or >128*1024*1024)
            throw new InvalidDataException("Battle damage executable exceeds its supported size bound.");
        byte[] executable=new byte[(int)stream.Length]; stream.ReadExactly(executable);
        string sha=Convert.ToHexString(SHA256.HashData(executable));
        if(sha!=profile.ExecutableSha256)
            throw new InvalidDataException("Battle damage executable differs from the inspected CE source revision.");
        using var pe=new PEReader(new MemoryStream(executable,writable:false));
        var optional=pe.PEHeaders.PEHeader;
        if(optional is null || optional.Magic!=PEMagic.PE32Plus ||
            optional.ImageBase!=0x140000000UL || pe.PEHeaders.CoffHeader.Machine!=Machine.Amd64)
            throw new InvalidDataException("Battle damage source must be the inspected AMD64 PE32+ profile.");
        var fields=new List<OriginalBattleDamageSourceField>();
        foreach((string name,int scRva,int expected) in ParameterFields)
        {
            int rva=scRva+profile.ParameterRvaDelta;
            var sections=pe.PEHeaders.SectionHeaders.Where(s=>s.Name==".data" &&
                rva>=s.VirtualAddress && (long)rva+4<=(long)s.VirtualAddress+s.SizeOfRawData).ToArray();
            if(sections.Length!=1) throw new InvalidDataException("Battle parameter is not one raw .data range.");
            long offset=(long)sections[0].PointerToRawData+rva-sections[0].VirtualAddress;
            if(offset<0 || offset>executable.Length-4)
                throw new InvalidDataException("Battle parameter exceeds executable bytes.");
            ReadOnlySpan<byte> bytes=executable.AsSpan((int)offset,4);
            int value=BinaryPrimitives.ReadInt32LittleEndian(bytes);
            if(value!=expected) throw new InvalidDataException("Battle parameter differs from inspected source bytes.");
            fields.Add(new(name,rva,value,Convert.ToHexString(SHA256.HashData(bytes))));
        }
        var source=new SourceProvenance(relative,executable.Length,sha,profile.ExecutableSha256)
        { ResourceUri=GameResourceId.File(relative).ToString(),PhysicalPath=path,IsOverride=false };
        return new(language,source,profile.DecodedTextSha256,fields.ToArray(),profile.Windows);
    }

    public static int CalculateTroopDamage(OriginalBattleDamageSourceParameters parameters,
        OriginalTroopDamageSourceInput input)
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
    public static int CalculateAppliedTroopLoss(OriginalBattleDamageSourceParameters parameters,
        OriginalTroopDamageSourceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Math.Min(input.TargetTroops,CalculateTroopDamage(parameters,input));
    }

    public static int CalculateWallDamage(OriginalBattleDamageSourceParameters parameters,
        OriginalWallDamageSourceInput input)
    {
        ArgumentNullException.ThrowIfNull(parameters); ArgumentNullException.ThrowIfNull(input);
        ValidateCurrentParameters(parameters);
        if(input.CurrentDurability is <0 or >ushort.MaxValue ||
            !float.IsFinite(input.FinalWallAttack) || input.FinalWallAttack<0 ||
            (input.AttackerPresent && !input.PermitZeroDurability && input.CurrentDurability==0))
            throw new InvalidDataException("Native wall damage needs a live permitted durability target and finite wall attack.");
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

    private static int PercentProduct(int damage,int multiplier,int value)
        =>unchecked(damage*multiplier*value)/100;

    private static void ValidateCurrentParameters(OriginalBattleDamageSourceParameters parameters)
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

    private static readonly (string Name,int ScRva,int Expected)[] ParameterFields=
    [
        ("attackOffset",0x1A4A948,1000),("attackSquareDivisor",0x1A4A954,2000),
        ("troopSumDivisor",0x1A4A960,300),("troopSumOffset",0x1A4A96C,300),
        ("damageDivisor",0x1A4A978,1500),("transportTargetMultiplier",0x1A4A984,10),
        ("cityPolicyMultiplier",0x1A4A9CC,1),("cityPersonMultiplier",0x1A4A9D8,1),
        ("cityQueryMultiplier",0x1A4ABC8,1),("halfDamageDivisor",0x1A4C3B8,2),
        ("damageCap",0x1A4C514,9999),("wallPersonMultiplier",0x1A4C4F8,1)
    ];

    private sealed record Profile(string ExecutableSha256,string DecodedTextSha256,
        int ParameterRvaDelta,OriginalBattleDamageSourceCodeWindow[] Windows);
    private static Profile GetProfile(string language)=>language switch
    {
        "SC"=>new("CEEE6A94EB939B40C329B8EC5AECD938E9D3A51A6663E8E7D27A1498627AFB88",
            "D7F3831C369A76148D87370D91EF0A8A43487D5E13A383886C68A6D50591E8F6",0,
            [new("commonTroopDamage",0x169FC0,1452,"8705F47489F90FBCD1EB0F06D9A08C34A34238A1AB33C9F3989721BA9C33CF4E"),
             new("wallDamageApplication",0x29EAD0,1430,"E42A42CAECD4BC7B2E5B01DAA4FCD4C72827158D68098D2E3D06C6BB44CF79DF"),
             new("wallDamageCaller",0x174B87,132,"78FFAE7FF1E9B6F9B5606A1E166D820CF40461403577CC5C69E47664414E20EF"),
             new("armyTargetTroopCap",0x178D89,11,"0C2271092419D91EB11C7F76753BBE44B6DF7D3B15306124C7B465BCC206C4E6"),
             new("baseTargetTroopCap",0x1793A0,10,"4922D2C29989906BF3D9B613F4090D3C21E902EE89E305FE3731CA252AE43214")]),
        "TC"=>new("0751B4FED7A5783371B9541674C0587A6AFD3140E60E706E32EAF35A9432F464",
            "FFBD504FC3DEA6A218EF4E8530A65761296C17191F7D675AB3C49DF64D950766",-0x1030,
            [new("commonTroopDamage",0x16A260,1452,"D6D1903660530BE082011B3D97A1E9478F89BBC037789A48DBE090DC0ABA2477"),
             new("wallDamageApplication",0x29EC80,1430,"1FB5294FF5FAC9F8B48F1E257D57FB909EF0890E10CDB38114FD6F8E28F57F07"),
             new("wallDamageCaller",0x174E27,132,"9BFEE28D6C0451E9DD792A8DEAC37451B9712AD4E1A534E86A383091EC843A19"),
             new("armyTargetTroopCap",0x179029,11,"0C2271092419D91EB11C7F76753BBE44B6DF7D3B15306124C7B465BCC206C4E6"),
             new("baseTargetTroopCap",0x179640,10,"4922D2C29989906BF3D9B613F4090D3C21E902EE89E305FE3731CA252AE43214")]),
        _=>throw new InvalidDataException("Battle damage source supports the inspected CE SC/TC revisions only.")
    };
}
