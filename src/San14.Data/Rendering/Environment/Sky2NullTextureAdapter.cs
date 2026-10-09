using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public enum SkyTextureStateScope { FreshSourceConstruction, CurrentOrEditedState, AppliedSourceSeasonOnFreshShader }

/// <summary>The selected fresh source has no texture assignment after the
/// original118CFA0 constructor initialized its Sky2 texture pointers to null.
/// A zero Camera key alone does not establish a current null GPU binding.</summary>
public sealed class Sky2SourceNull2D
{
    public SkySamplingReference CameraReference { get; }
    public uint PixelRegister => SkySamplingResources.PixelRegister(CameraReference.Role);
    public SkyTextureStateScope Scope{get;}
    private readonly MapSeasonCameraPackage? appliedSeasonPackage;
    public string SourceEvidence { get; }
    private Sky2SourceNull2D(SkySamplingReference reference,
        MapSeasonCameraPackage? package=null)
    {CameraReference=reference;appliedSeasonPackage=package;Scope=package is null?
        SkyTextureStateScope.FreshSourceConstruction:SkyTextureStateScope.AppliedSourceSeasonOnFreshShader;
      SourceEvidence=package is null
        ? SkySamplingResources.ExplainFreshNull(reference)+";Sky2Ctor118CFA0:null1678/1688/1698"
        : SkySeasonPackageTextures.ExplainNullAssignment(SkySeasonPackageTextures.Read(package))+
            ";scope=AppliedSourceSeasonOnFreshSky2Shader;118CFA0-null-then-source-null-wrapper-skips-setter;no-intervening-nonnull-assignment"+
            ";source-null-contract-sha256=DC60552E6C96977D7897BA5C7FF92E3B654D755D802605081F5A2160CCF6EB6B";
    }

    public static Sky2SourceNull2D FromFreshConstruction(SkySamplingReference reference,
        SkyTextureStateScope explicitScope)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (explicitScope != SkyTextureStateScope.FreshSourceConstruction)
            throw new InvalidDataException("A current or edited Sky2 texture state requires its actual applied resource; source zero is insufficient.");
        var canonical = SkySamplingResources.ReadCameraReference(reference.Definitions,
            reference.Camera.ObjectId, reference.Role);
        if (!canonical.IsSourceReferenceZero || canonical.Role == SkySamplingRole.Star)
            throw new InvalidDataException("Sky2 null2D requires a verified fresh Earth/Moon/Milky source zero and no subsequent provider assignment.");
        return new(canonical);
    }

    /// <summary>A separate lifecycle: actual seasonal package pointer-copy on
    /// a newly created Sky2 shader. Source-null wrappers skip its setters, so
    /// constructor-null remains. Existing arbitrary shader state cannot enter
    /// this scope. The owning assembler freezes this source family and rejects
    /// later non-null assignments to the adapted slot.</summary>
    public static Sky2SourceNull2D FromAppliedSeasonOnFreshShader(MapSeasonCameraPackage package,
        SkySamplingRole role,SkyTextureStateScope explicitScope)
    {
        if(explicitScope!=SkyTextureStateScope.AppliedSourceSeasonOnFreshShader||
            role is not (SkySamplingRole.MilkyWay or SkySamplingRole.Moon))
            throw new InvalidDataException("Applied season null proof requires its separate fresh-shader source lifecycle and a declared zero Milky/Moon role.");
        var sampling=SkySeasonPackageTextures.Read(package);
        var reference=role==SkySamplingRole.MilkyWay?sampling.MilkyWay:sampling.Moon;
        _=SkySeasonPackageTextures.ExplainNullAssignment(sampling);
        return new(reference,sampling.Package);
    }

    public void Validate()
    {
        if(appliedSeasonPackage is not null)
        {
            if(!CameraReference.IsSourceReferenceZero ||
                CameraReference.Role is not (SkySamplingRole.MilkyWay or SkySamplingRole.Moon) ||
                Scope!=SkyTextureStateScope.AppliedSourceSeasonOnFreshShader)
                throw new InvalidDataException("Loaded season null descriptor has an invalid role or descriptor scope.");
            return;
        }
        // FromFreshConstruction already imported the complete source. A newly
        // loaded/changed asset must construct its own proof; packet validation
        // checks this proof's role and current presence, not the file again.
        if (!CameraReference.IsSourceReferenceZero || CameraReference.Role == SkySamplingRole.Star ||
            Scope != SkyTextureStateScope.FreshSourceConstruction)
            throw new InvalidDataException("The loaded fresh null texture has an invalid role or descriptor scope.");
    }
}

public sealed record Sky2NullTextureSpirv(byte[] Bytes, string Sha256,
    string UnspecializedTranslationSha256, IReadOnlyList<uint> NullPixelRegisters,
    IReadOnlyList<string> SourceEvidence, int ReplacedSamples, int RetainedInstructions)
{
    public int SourcePhysicalPixelProgramIndex { get; internal init; }
    public string SourcePixelSha256 { get; internal init; } = "";
    public IReadOnlyList<Sky2SourceNull2D> SourceNullProofs { get; internal init; } = [];
}

/// <summary>Pinned Sky2 PS3/PS4 and finite Moon PS5/PS11/PS6/PS14/PS15 adapter for the
/// original D3D11 null2D result. Default Earth/Milky or finite Moon Milky/Moon
/// samples become float4(+0) only with their exact typed source-null proof.
/// Non-null programs and all other instructions remain unchanged. No bitmap
/// or GPU texture is allocated. This does not handle unknown current bindings.</summary>
public static partial class Sky2NullTextureAdapter
{
    public const string DefaultPixelSourceSha = "890FB3C9182BAE2D52C20BA993471769F8BD8DBFB959997474827ACAFCDAB11A";
    public const string DefaultPixelTranslationSha = "C19982966B36102793D05CFA629EFB136C284176477795D4E052890743504C3B";
    public const string ContributionPixelSourceSha = "FA1B12A79D043104E98FE73A57F85D658B8913630802F8C7133A4ECDEAF7FEFA";
    public const string ContributionPixelTranslationSha = "8F84EBDE83AAD5EE55886B25D32CBD66DC4AB270C34C92139913E037456EB86D";

    public static Sky2NullTextureSpirv Adapt(ShaderSpirv pixel,
        TranslatedShaderBindings originalBindings, IReadOnlyList<Sky2SourceNull2D> explicitNulls)
    {
        ArgumentNullException.ThrowIfNull(pixel); ArgumentNullException.ThrowIfNull(originalBindings);
        ArgumentNullException.ThrowIfNull(explicitNulls);
        FiniteMoonProfile? finiteMoon = RequireFiniteMoonProfileIfSelected(pixel, originalBindings);
        bool contribution = pixel.SourceProgramSha256 == ContributionPixelSourceSha &&
            pixel.SpirvSha256 == ContributionPixelTranslationSha;
        bool atmosphere = pixel.SourceProgramSha256 == DefaultPixelSourceSha &&
            pixel.SpirvSha256 == DefaultPixelTranslationSha;
        if ((!atmosphere && !contribution && finiteMoon is null) ||
            Convert.ToHexString(SHA256.HashData(pixel.Bytes)) != pixel.SpirvSha256 ||
            originalBindings.Interface.Sha256 != pixel.SpirvSha256 ||
            originalBindings.Interface.EntryPoints.Single().ExecutionModel != 4 || explicitNulls.Count == 0)
            throw new InvalidDataException("Null2D adapter requires the exact original Sky2 PS3/PS4 or finite Moon PS5/PS11/PS6/PS14/PS15 and its complete translated interface.");
        foreach (var proof in explicitNulls) { ArgumentNullException.ThrowIfNull(proof); proof.Validate(); }
        uint[] slots = explicitNulls.Select(p => p.PixelRegister).Order().ToArray();
        if (slots.Distinct().Count() != slots.Length ||
            (finiteMoon is null ? slots.Any(s => s is not (3 or 4)) : slots.Any(s => s is not (4 or 5))))
            throw new InvalidDataException("Sky2 null2D specialization requires explicit source-null Earth/Milky for PS3/4 or Milky/Moon for finite PS5/11/6/14/15.");
        if (contribution && (slots.Length != 1 || slots[0] != 4 ||
            originalBindings.Bindings.Count != 5 ||
            !originalBindings.Bindings.Where(b => b.DescriptorKind == "Texture").Select(b => b.Source.BindPoint).Order().SequenceEqual(new uint[] {1,4}) ||
            !originalBindings.Bindings.Where(b => b.DescriptorKind == "Sampler").Select(b => b.Source.BindPoint).Order().SequenceEqual(new uint[] {1,4}) ||
            originalBindings.SourceDefinitions.Buffers.Count != 1 || originalBindings.SourceDefinitions.Buffers[0].ByteSize != 256))
            throw new InvalidDataException("Source Sky2 PS4 null specialization requires exactly Milky t4 and its unchanged Ttable t1/256B interface.");
        var resources = new HashSet<uint>(); var textures = new HashSet<uint>();
        foreach (uint slot in slots)
        {
            var image = originalBindings.Bindings.Single(b => b.DescriptorKind == "Texture" && b.Source.BindPoint == slot);
            var sampler = originalBindings.Bindings.Single(b => b.DescriptorKind == "Sampler" && b.Source.BindPoint == slot);
            if (image.Source.Dimension != 4 || image.Source.ReturnType != 5 || image.Device.DescriptorSet != 0 || sampler.Device.DescriptorSet != 0)
                throw new InvalidDataException("Null2D source declaration differs from floating Texture2D and its original sampler.");
            textures.Add(image.Device.VariableId); resources.Add(image.Device.VariableId); resources.Add(sampler.Device.VariableId);
        }
        var instructions = Parse(pixel.Bytes); var removed = new HashSet<uint>(resources);
        var imageLoads = new HashSet<uint>(); var sampledImages = new HashSet<uint>();
        var zeros = new Dictionary<uint, uint>(); uint next = originalBindings.Interface.IdBound;
        var body = new List<uint[]>(); int replaced = 0, retained = 0;
        foreach (uint[] w in instructions)
        {
            ushort op = (ushort)w[0];
            if (op == 61 && resources.Contains(w[3]))
            { removed.Add(w[2]); if (textures.Contains(w[3])) imageLoads.Add(w[2]); continue; }
            if (op == 86 && imageLoads.Contains(w[3]))
            {
                if (!removed.Contains(w[4])) throw new InvalidDataException("Source null image must use its exact paired null sampler.");
                sampledImages.Add(w[2]); removed.Add(w[2]); continue;
            }
            if (op is 87 or 88 && sampledImages.Contains(w[3]))
            {
                // The result's vec4<float32> type is verified from declarations,
                // independent of whether a stage variable uses that type.
                uint[] vector = instructions.Single(t => (ushort)t[0] == 23 && t[1] == w[1]);
                if (vector[3] != 4 || !instructions.Any(t => (ushort)t[0] == 22 && t[1] == vector[2] && t[2] == 32))
                    throw new InvalidDataException("Default Sky2 null sample has a different result type.");
                if (!zeros.TryGetValue(w[1], out uint zero)) zeros.Add(w[1], zero = next++);
                body.Add(Op(83, w[1], w[2], zero)); replaced++; continue;
            }
            body.Add(w); retained++;
        }
        if (replaced != slots.Length || imageLoads.Count != slots.Length || sampledImages.Count != slots.Length)
            throw new InvalidDataException("Default Sky2 null sample/load schedule differs from the pinned source.");
        var final = new List<uint>(); uint[] header = Words(pixel.Bytes).Take(5).ToArray(); header[3] = next; final.AddRange(header);
        bool inserted = false;
        foreach (uint[] w in body)
        {
            ushort op = (ushort)w[0];
            if (op is 5 or 71 && removed.Contains(w[1])) { retained--; continue; }
            if (op == 59 && resources.Contains(w[2])) { retained--; continue; }
            if (op == 54 && !inserted)
            { foreach (var zero in zeros) final.AddRange(Op(46, zero.Key, zero.Value)); inserted = true; }
            final.AddRange(w);
        }
        byte[] bytes = new byte[final.Count * 4];
        for (int i = 0; i < final.Count; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), final[i]);
        var result = SpirvShaderInterfaceReader.Read(bytes);
        if (result.Descriptors.Count != originalBindings.Bindings.Count - 2 * slots.Length ||
            result.StageVariables.Count != originalBindings.Interface.StageVariables.Count ||
            result.Descriptors.Any(d => resources.Contains(d.VariableId)))
            throw new InvalidDataException("Null2D adapter changed the remaining source interface.");
        return new(bytes, result.Sha256, pixel.SpirvSha256, Array.AsReadOnly(slots),
            Array.AsReadOnly(explicitNulls.Select(p => p.SourceEvidence).ToArray()), replaced, retained)
        {
            SourcePhysicalPixelProgramIndex = finiteMoon?.Index ?? (contribution ? 4 : 3),
            SourcePixelSha256 = pixel.SourceProgramSha256,
            SourceNullProofs = Array.AsReadOnly(explicitNulls.ToArray())
        };
    }

    private static uint[] Op(ushort code, params uint[] words)
        => new[] { (uint)((words.Length + 1) << 16) | code }.Concat(words).ToArray();
    private static uint[] Words(ReadOnlySpan<byte> bytes)
    { var words = new uint[bytes.Length / 4]; for (int i = 0; i < words.Length; i++) words[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(4 * i)..]); return words; }
    private static List<uint[]> Parse(ReadOnlySpan<byte> bytes)
    {
        var words = Words(bytes); var list = new List<uint[]>();
        for (int at = 5; at < words.Length;)
        { int count = (int)(words[at] >> 16); if (count == 0 || count > words.Length - at) throw new InvalidDataException("Truncated Sky2 SPIR-V."); list.Add(words.AsSpan(at, count).ToArray()); at += count; }
        return list;
    }
}


