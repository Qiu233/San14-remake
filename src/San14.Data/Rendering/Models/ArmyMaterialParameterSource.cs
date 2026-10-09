using System.Buffers.Binary;
using System.Collections.ObjectModel;
using San14.Data.Formats;

namespace San14.Data;

public enum ArmyMaterialParameterPhase { RegularStage1, RenderTargetStage2 }

/// <summary>The initial type12 record produced by D9C5D0. A later parameter
/// producer can replace its contents; this packet does not certify that the
/// material's automaton or complete ready stage has run.</summary>
public sealed record ArmyMaterialParameterRecord12(
    ReadOnlyMemory<byte> MaterialRecord64, uint SourceMaterialObjectId,
    ArmyMaterialParameterPhase Phase, string SourceEvidence)
{
    public uint Flags1C => BinaryPrimitives.ReadUInt32LittleEndian(MaterialRecord64.Span.Slice(0x1C,4));
    public byte AlphaReference3E => MaterialRecord64.Span[0x3E];
    public float SpecularW2C => BinaryPrimitives.ReadSingleLittleEndian(MaterialRecord64.Span.Slice(0x2C,4));
}

/// <summary>One accepted user property routed by D9C840. NameHash is the
/// original suffix hash, after the native caller removes its six-byte prefix.
/// ParameterNames are the actual native shader parameter-name directory;
/// translated shader reflection is not a substitute.</summary>
public sealed record ArmyMaterialShaderParameterRow(
    IReadOnlyList<string>? ActualParameterNames);
/// <summary>An actual native directory query. A source-proven query result
/// can be independent of unrelated conditional directory lengths.</summary>
public sealed record ArmyMaterialShaderParameterLookup(
    Func<string,int> ReadActualParameterOrdinal);
public sealed record ArmyMaterialScalarParameter5(
    uint SourcePropertyHash, string NativeSuffixName, ReadOnlyMemory<byte> Value,
    int ElementCount);
public sealed record ArmyMaterialParameterAggregate15(
    IReadOnlyList<ArmyMaterialScalarParameter5> ScalarRecords,
    ReadOnlyMemory<int> ShaderParameterOrdinals,
    ReadOnlyMemory<uint> SuffixNameHashes, int ActualShaderRowCount)
{
    public const int NativeScratchShaderRowStride = 0x80;
    public int ReadOrdinal(int row,int parameter) => ShaderParameterOrdinals.Span[row*ScalarRecords.Count+parameter];
}

/// <summary>Project material constructor rules. The two ready phases supply
/// different specular W values; material declarations remain resource data.</summary>
public sealed class LoadedArmyMaterialParameterNativeSource
{
    internal LoadedArmyMaterialParameterNativeSource(string locale,float stage1,float stage2)
    { Locale=locale;
      Stage1SpecularW=stage1;Stage2SpecularW=stage2; }
    public string Locale { get; }
    public string RuleVersion => RenderRuleIdentity.Version;
    public float Stage1SpecularW { get; }
    public float Stage2SpecularW { get; }
    public string SourceEvidence => $"{RuleVersion}/army-material/{Locale};"+
        "phase1/2-parameters;material-record12;scalar-aggregate15;retain-enqueue";
    public static LoadedArmyMaterialParameterNativeSource Load(GameResourceRepository repository,string locale)
        => ArmyMaterialParameterRules.Create(locale);
    public LoadedArmyMaterialParameterSource ReadMaterial(LoadedArmyMaterialReadySource actualMaterial)
        => new(this,actualMaterial,ArmyFlagMaterialVariations.ReadUserParameters(actualMaterial.Material));
}

public sealed class LoadedArmyMaterialParameterSource
{
    private readonly LoadedArmyMaterialParameterNativeSource native;
    private readonly ArmyFlagMaterialUserParameters user;
    internal LoadedArmyMaterialParameterSource(LoadedArmyMaterialParameterNativeSource actualNative,
        LoadedArmyMaterialReadySource actualMaterial,ArmyFlagMaterialUserParameters actualUser)
    {
        native=actualNative;Material=actualMaterial;user=actualUser;
        var context=actualMaterial.Material.Properties.Single(p=>p.NameHash==0xC17A66DF);
        if(context.TypeCode!=5||context.ElementCount!=1||context.RawValue.Length!=4)
            throw new InvalidDataException("Material CPU18 context dependency is not its actual DWORD lookup field.");
        RegisteredContextResourceObjectId=BinaryPrimitives.ReadUInt32LittleEndian(context.RawValue);
        var filename=actualMaterial.Material.Properties.Single(p=>p.NameHash==0x8FD1DA96);
        if(filename.TypeCode!=1||filename.RawValue.Length!=filename.ElementCount)
            throw new InvalidDataException("Material filename context source is not its actual byte-string field.");
        FilenameContextSourceBytes=filename.RawValue;
    }
    public LoadedArmyMaterialReadySource Material { get; }
    public int? DeclaredMaterialId => user.DeclaredMaterialId;
    public uint? DeclaredAutomatonContextIdentifier => user.AutomatonContextObjectId;
    /// <summary>D9B7B0's separate CPU18 registered-Data lookup input. Its
    /// declaration is not current directory presence or dependency readiness.</summary>
    public uint RegisteredContextResourceObjectId { get; }
    public ReadOnlyMemory<byte> FilenameContextSourceBytes { get; }
    public bool HasFilenameContextSource => !FilenameContextSourceBytes.IsEmpty&&FilenameContextSourceBytes.Span[0]!=0;
    public ArmyMaterialParameterRecord12 ConstructInitialRecord12(ArmyMaterialParameterPhase actualPhase)
    {
        float specular=actualPhase switch {
            ArmyMaterialParameterPhase.RegularStage1=>native.Stage1SpecularW,
            ArmyMaterialParameterPhase.RenderTargetStage2=>native.Stage2SpecularW,
            _=>throw new ArgumentOutOfRangeException(nameof(actualPhase)) };
        byte[] record=new byte[64];
        foreach(int offset in new[]{0,4,8,12,16,20,24,32,36,40,48,52,56})
            BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(offset,4),0x3F800000);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(0x1C,4),0x100);
        BinaryPrimitives.WriteSingleLittleEndian(record.AsSpan(0x2C,4),specular);
        if(user.DeclaredOpacityThreshold is float opacity) {
            // Native COMISS/MAXSS then MULSS/CVTTSS2SI, for the admitted finite
            // scalar source. The product does not round the byte threshold.
            float clamped=opacity>=1?1:MathF.Max(0,opacity);
            float scaled=clamped*255;
            record[0x3E]=unchecked((byte)(long)scaled);
        }
        return new(record,Material.Material.ObjectId,actualPhase,native.SourceEvidence);
    }

    /// <summary>The current supported A8 user subset: D9C840 skips the
    /// automaton identifier and opacity threshold, and routes nMtrID as a
    /// type5 scalar. The native producer has other type branches; callers
    /// importing additional user properties must supply their real converted
    /// records rather than silently dropping them.</summary>
    public IReadOnlyList<ArmyMaterialScalarParameter5> ReadImportedScalarParameters5()
    {
        if(user.MaterialIdProperty is not {} property)return Array.Empty<ArmyMaterialScalarParameter5>();
        return Array.AsReadOnly(new[]{new ArmyMaterialScalarParameter5(
            property.NameHash,"nMtrID",property.RawValue,property.ElementCount)});
    }

    /// <summary>D9C840's scalar-record mapping to every actual full-table row.
    /// This consumes native parameter-name directory ordinals, including rows
    /// without a parameter group. It does not infer row ordinals from PS RDEF.</summary>
    public ArmyMaterialParameterAggregate15 BuildActualScalarAggregate15(
        IReadOnlyList<ArmyMaterialScalarParameter5> actualRecords,
        IReadOnlyList<ArmyMaterialShaderParameterRow> actualShaderRows)
        => BuildActualScalarAggregate15(actualRecords,actualShaderRows.Select(row=>
            new ArmyMaterialShaderParameterLookup(name=> {
                if(row.ActualParameterNames is {} names)
                    for(int i=0;i<names.Count;i++)if(names[i]==name)return i;
                return -1;
            })).ToArray());

    public ArmyMaterialParameterAggregate15 BuildActualScalarAggregate15(
        IReadOnlyList<ArmyMaterialScalarParameter5> actualRecords,
        IReadOnlyList<ArmyMaterialShaderParameterLookup> actualShaderRows)
    {
        if(actualRecords.Count>ArmyMaterialParameterAggregate15.NativeScratchShaderRowStride)
            throw new InvalidDataException("Actual material parameter count exceeds the native shader-row stride.");
        // D9CF10..D9CF45 compacts the128-stride scratch rows before BF7B00.
        // Only the actual row*parameter count belongs to the type5 child;
        // unused native allocation bytes are not exposed as guessed defaults.
        int[] ordinals=new int[checked(actualShaderRows.Count*actualRecords.Count)];
        uint[] hashes=new uint[actualRecords.Count];
        for(int parameter=0;parameter<actualRecords.Count;parameter++) {
            var record=actualRecords[parameter];
            if(record.Value.Length!=checked(record.ElementCount*4))
                throw new InvalidDataException("Type5 material parameter does not contain its actual complete scalar array.");
            hashes[parameter]=ResourceNameHash.Compute(record.NativeSuffixName);
            for(int row=0;row<actualShaderRows.Count;row++) {
                int ordinal=actualShaderRows[row].ReadActualParameterOrdinal(record.NativeSuffixName);
                ordinals[row*actualRecords.Count+parameter]=ordinal;
            }
        }
        return new(new ReadOnlyCollection<ArmyMaterialScalarParameter5>(actualRecords.ToArray()),
            ordinals,hashes,actualShaderRows.Count);
    }
}
