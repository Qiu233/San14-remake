using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Specialized bytes for one audited selected original program. The
/// original RDEF and source-null owner proofs are retained. No image is made.</summary>
public sealed class DeferredSelectedSourceNullSpirv
{
    private readonly byte[] bytes;
    internal DeferredSelectedSourceNullSpirv(DeferredF103SelectedSource selected,
        byte[] encoded, string originalHash, uint[] textures, uint[] samplers,
        DeferredSourceNull2D[] pairs, DeferredSourceNullIes? ies,
        DeferredSourceLocalAmbient? las, Dictionary<string,int> counts)
    {
        SelectedSource=selected; bytes=encoded.ToArray(); Sha256=Convert.ToHexString(SHA256.HashData(bytes));
        UnspecializedTranslationSha256=originalHash; RemovedTextureRegisters=Array.AsReadOnly(textures);
        RemovedSamplerRegisters=Array.AsReadOnly(samplers); ProofPairs=Array.AsReadOnly(pairs);
        SourceNullIes=ies; SourceLocalAmbient=las; ReplacedReads=new ReadOnlyDictionary<string,int>(counts);
    }
    public DeferredF103SelectedSource SelectedSource { get; }
    public byte[] Bytes=>bytes.ToArray();
    public string Sha256 { get; }
    public string UnspecializedTranslationSha256 { get; }
    public IReadOnlyList<uint> RemovedTextureRegisters { get; }
    public IReadOnlyList<uint> RemovedSamplerRegisters { get; }
    public IReadOnlyList<DeferredSourceNull2D> ProofPairs { get; }
    public DeferredSourceNullIes? SourceNullIes { get; }
    public DeferredSourceLocalAmbient? SourceLocalAmbient { get; }
    public IReadOnlyDictionary<string,int> ReplacedReads { get; }
}

/// <summary>Role-based specialization of audited original combinations128,
/// 510,640 and1022. Their source registers differ from PS1. Every read keeps its
/// original result type and ID; other instructions and descriptors survive.</summary>
public static class DeferredSelectedSourceNullAdapter
{
    private sealed record Role(string Name,uint Dimension,uint ReturnType,string? Sampler,bool RemoveSampler,bool Fetch);
    private static readonly Role Hatching=new("sHatchingShadow",4,5,"__smpsHatchingShadow",true,false);
    private static readonly Role Ramp=new("sRamp",4,5,"__smpsRamp",true,false);
    private static readonly Role Ies=new("sIES",5,5,"__smpsIES",true,false);
    private static readonly Role LocalCube=new("sLclAmbSpc",10,5,"__smpsAmbSpc",false,false);
    private static readonly Role LocalData=new("tLasData",1,5,null,false,true);
    private static readonly Role LocalIndices=new("tLasIndices",1,4,null,false,true);

    public static DeferredSelectedSourceNullSpirv Adapt(ShaderSpirv original,
        TranslatedShaderBindings sourceBindings,DeferredF103SelectedSource selection,
        IReadOnlyList<DeferredSourceNull2D> pairs,DeferredSourceNullIes? ies,
        DeferredSourceLocalAmbient? las)
    {
        ValidateSource(original,sourceBindings,selection);
        ArgumentNullException.ThrowIfNull(pairs);
        if(pairs.Count==0&&ies is null&&las is null)throw Error("requires at least one explicit source owner proof");
        var roles=new List<Role>(); var ownedPairs=pairs.ToArray();
        if(ownedPairs.Select(p=>p.PixelRegister).Distinct().Count()!=ownedPairs.Length)throw Error("duplicate source 2D owner proof");
        foreach(var pair in ownedPairs)
        {
            ArgumentNullException.ThrowIfNull(pair); pair.ValidateForSelection(selection);
            roles.Add(pair.PixelRegister switch {6=>Hatching,10=>Ramp,_=>throw Error("unknown source 2D role")});
        }
        ValidateProofOwners(selection,ownedPairs,ies,las);
        if(ies is not null)roles.Add(Ies);
        if(las is not null)roles.AddRange([LocalCube,LocalData,LocalIndices]);
        var resources=new HashSet<uint>(); var imageRoles=new Dictionary<uint,Role>();
        var roleSamplers=new Dictionary<string,uint>(); var removedTextures=new List<uint>(); var removedSamplers=new List<uint>();
        var counts=new Dictionary<string,int>();
        foreach(var role in roles)
        {
            var images=sourceBindings.Bindings.Where(b=>b.Source.InputType==2&&b.Source.Name.Text==role.Name).ToArray();
            if(images.Length==0)
            {
                if(selection.Combination.Index is not (510 or 1022)||role==Hatching)throw Error("proved role unexpectedly absent from audited program");
                continue; // This selected original variant has no such declaration or operation.
            }
            if(images.Length!=1)throw Error("ambiguous selected image role");
            var image=images[0];
            if(image.DescriptorKind!=(role.Fetch?"TextureBuffer":"Texture")||image.Source.Dimension!=role.Dimension||
                image.Source.ReturnType!=role.ReturnType||image.Source.BindCount!=1||image.Device.DescriptorSet!=0)
                throw Error("selected source image role changed its original kind/type");
            // LAS uses its named metadata/configuration instead of an ordinary combination row.
            if(!role.Fetch&&role!=LocalCube)
            {
                uint encoded=role==Hatching?36u:role==Ramp?16u:32u;
                var mappings=selection.ResourceBindings.Entries.Where(m=>m.PrimaryEncoded==encoded&&m.Name==role.Name).ToArray();
                if(mappings.Length!=1||mappings[0].SlotIndex!=(int)image.Source.BindPoint)
                    throw Error("selected role differs from its original encoded resource mapping");
            }
            imageRoles.Add(image.Device.VariableId,role); resources.Add(image.Device.VariableId);
            removedTextures.Add(image.Source.BindPoint); counts.Add(role.Name,0);
            if(role.Sampler is not null)
            {
                var samplers=sourceBindings.Bindings.Where(b=>b.DescriptorKind=="Sampler"&&b.Source.Name.Text==role.Sampler).ToArray();
                if(samplers.Length!=1||samplers[0].Source.BindCount!=1||samplers[0].Device.DescriptorSet!=0)
                    throw Error("selected role lacks its original named sampler");
                roleSamplers.Add(role.Name,samplers[0].Device.VariableId);
                if(role.RemoveSampler){resources.Add(samplers[0].Device.VariableId);removedSamplers.Add(samplers[0].Source.BindPoint);}
            }
        }
        if(imageRoles.Count==0)throw Error("proofs do not cover any declaration in the selected program");
        var instructions=Parse(original.Bytes); var removed=new HashSet<uint>(resources);
        var loaded=new Dictionary<uint,Role>(); var sampled=new Dictionary<uint,Role>();
        var samplerLoads=instructions.Where(w=>(ushort)w[0]==61&&w.Length==4).ToDictionary(w=>w[2],w=>w[3]);
        var zeros=new Dictionary<uint,uint>(); uint next=sourceBindings.Interface.IdBound; var body=new List<uint[]>();
        foreach(uint[] w in instructions)
        {
            ushort op=(ushort)w[0];
            if(op==61&&w.Length==4&&resources.Contains(w[3]))
            {removed.Add(w[2]);if(imageRoles.TryGetValue(w[3],out var role))loaded.Add(w[2],role);continue;}
            if(op==86&&loaded.TryGetValue(w[3],out var sampleRole))
            {
                if(sampleRole.Fetch||!samplerLoads.TryGetValue(w[4],out uint sampler)||sampler!=roleSamplers[sampleRole.Name])
                    throw Error("selected image operation uses a different sampler");
                sampled.Add(w[2],sampleRole);removed.Add(w[2]);continue;
            }
            Role? replacement=null;
            if(op is 87 or 88&&sampled.TryGetValue(w[3],out var sampledRole))replacement=sampledRole;
            if(op==95&&loaded.TryGetValue(w[3],out var fetchedRole))replacement=fetchedRole;
            if(replacement is not null)
            {
                if((op==95)!=replacement.Fetch)throw Error("selected null consumer operation differs from audited source role");
                var vector=instructions.Single(t=>(ushort)t[0]==23&&t[1]==w[1]);
                bool component=replacement.ReturnType==4
                    ? instructions.Any(t=>(ushort)t[0]==21&&t[1]==vector[2]&&t[2]==32&&t[3]==0)
                    : instructions.Any(t=>(ushort)t[0]==22&&t[1]==vector[2]&&t[2]==32);
                if(vector[3]!=4||!component)throw Error("selected source result differs from float4/uint4");
                if(!zeros.TryGetValue(w[1],out uint zero))zeros.Add(w[1],zero=next++);
                body.Add(Op(83,w[1],w[2],zero));counts[replacement.Name]++;continue;
            }
            body.Add(w);
        }
        foreach(var count in counts)
        {
            int expected=count.Key switch {"sHatchingShadow"=>1,"sRamp"=>7,"sIES"=>2,"sLclAmbSpc"=>3,"tLasData"=>12,"tLasIndices"=>12,_=>0};
            if(count.Value!=expected)throw Error("selected null read schedule differs from the pinned original program");
        }
        var final=new List<uint>(); uint[] header=Words(original.Bytes).Take(5).ToArray();header[3]=next;final.AddRange(header);
        bool inserted=false;
        foreach(uint[] w in body)
        {
            ushort op=(ushort)w[0];
            if(op is 5 or 71&&removed.Contains(w[1])||op==59&&resources.Contains(w[2]))continue;
            if(op==54&&!inserted){foreach(var zero in zeros)final.AddRange(Op(46,zero.Key,zero.Value));inserted=true;}
            final.AddRange(w);
        }
        byte[] bytes=new byte[final.Count*4];
        for(int i=0;i<final.Count;i++)BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i*4),final[i]);
        var result=SpirvShaderInterfaceReader.Read(bytes);
        if(!inserted||result.Descriptors.Count!=sourceBindings.Bindings.Count-resources.Count||
            result.StageVariables.Count!=sourceBindings.Interface.StageVariables.Count||result.Descriptors.Any(d=>resources.Contains(d.VariableId)))
            throw Error("specialization changed a remaining original interface");
        return new(selection,bytes,original.SpirvSha256,removedTextures.Order().ToArray(),removedSamplers.Order().ToArray(),
            ownedPairs,ies,las,counts);
    }

    public static TranslatedShaderBindings ReadDeviceBindings(TranslatedShaderBindings original,
        DeferredSelectedSourceNullSpirv specialized)
    {
        ArgumentNullException.ThrowIfNull(original);ArgumentNullException.ThrowIfNull(specialized);

        ValidateProofOwners(specialized.SelectedSource,specialized.ProofPairs,specialized.SourceNullIes,specialized.SourceLocalAmbient);
        foreach(var pair in specialized.ProofPairs)pair.ValidateForSelection(specialized.SelectedSource);
        byte[] bytes=specialized.Bytes;
        var profile=Profile(specialized.SelectedSource);
        if(original.Interface.Sha256!=profile.Spirv||specialized.UnspecializedTranslationSha256!=profile.Spirv||
            Convert.ToHexString(SHA256.HashData(bytes))!=specialized.Sha256)throw Error("specialized source identity changed");
        var textures=specialized.RemovedTextureRegisters.ToHashSet();var samplers=specialized.RemovedSamplerRegisters.ToHashSet();
        var subset=original.SourceDefinitions with {Bindings=Array.AsReadOnly(original.SourceDefinitions.Bindings.Where(b=>
            !(b.InputType==2&&textures.Contains(b.BindPoint))&&!(b.InputType==3&&samplers.Contains(b.BindPoint))).ToArray())};
        var device=TranslatedShaderBindingReader.Read(subset,bytes);
        return device with {SourceDefinitions=original.SourceDefinitions};
    }

    private static void ValidateProofOwners(DeferredF103SelectedSource selection,
        IReadOnlyList<DeferredSourceNull2D> pairs,DeferredSourceNullIes? ies,DeferredSourceLocalAmbient? las)
    {
        if(pairs.Select(p=>p.LightSourceProvenance).Distinct().Count()>1)
            throw Error("2D proofs belong to different source construction generations");
        if(ies is not null)
        {
            ies.Validate();
            if(ies.CameraSource.Camera.ObjectId!=selection.Camera.Source.Camera.ObjectId||
                ies.CameraSource.Resource.Source.ResourceUri!=selection.Camera.Source.Resource.Source.ResourceUri||
                ies.CameraSource.Resource.Source.DecodedSha256!=selection.Camera.Source.Resource.Source.DecodedSha256)
                throw Error("IES proof belongs to a different selected source Camera");
        }
        if(las is not null)
        {
            las.Validate();
            if(las.Shader.Source.ResourceUri!=selection.SourcePlan.Shader.Source.ResourceUri||
                las.Shader.Source.DecodedSha256!=selection.SourcePlan.Shader.Source.DecodedSha256)
                throw Error("LAS proof belongs to a different source shader/configuration");
        }
    }
    private static (string Pixel,string Spirv,int Descriptors) Profile(DeferredF103SelectedSource selection)
        =>selection.Combination.Index switch {
            128 when selection.PixelProgramIndex==97=>("FF1CE5BFD5F4A8B3397E789C59045BCC72186644AA6C7D22EE523D633CBB384F","8A1A883ED801A2C964D524378F38C0F6764A5AA4BAFE4420251EC38024A9C0DA",40),
            510 when selection.PixelProgramIndex==383=>("91C2A580A1D05CA6144FD7CDE4F0AC24CD7ED425F2CFB4BCAAA4E9A4F62A3AF1","F61E3F4B41A1DE5FCA83564AD9BBE9D82F55BE5F7F060E13C292BFF24DD62592",19),
            640 when selection.PixelProgramIndex==481=>("1FD258CD6212012BAF24E7C407FC01771C93755FDC836C2F4E050238638DAE1B","691A220806FAEC9D4AC21D7344B00EB2DAE7768F85BAB6403B000ADD3BE5AE1B",40),
            1022 when selection.PixelProgramIndex==767=>("62CEB4427347D0D7E37CA096138B2F39CAEB9342134356177CFE83027E879A4C","0BCC360B9769393C153758E762E1DA3DB97E719EF4DB5C647236B74227FE6A1B",19),
            _=>throw Error("selected program is outside the independently audited variant profiles")};
    private static void ValidateSource(ShaderSpirv original,TranslatedShaderBindings bindings,DeferredF103SelectedSource selection)
    {
        ArgumentNullException.ThrowIfNull(original);ArgumentNullException.ThrowIfNull(bindings);ArgumentNullException.ThrowIfNull(selection);
        var profile=Profile(selection);
        if(original.SourceProgramSha256!=profile.Pixel||selection.PixelProgramSha256!=profile.Pixel||
            original.SpirvSha256!=profile.Spirv||Convert.ToHexString(SHA256.HashData(original.Bytes))!=profile.Spirv||
            bindings.Interface.Sha256!=profile.Spirv||bindings.Bindings.Count!=profile.Descriptors||
            bindings.Interface.EntryPoints.Single().ExecutionModel!=4)throw Error("requires the complete audited selected original translation");
    }
    private static InvalidDataException Error(string message)=>new("Selected deferred source null: "+message+".");
    private static uint[] Op(ushort code,params uint[] words)=>new[]{(uint)((words.Length+1)<<16)|code}.Concat(words).ToArray();
    private static uint[] Words(ReadOnlySpan<byte> bytes)
    {var words=new uint[bytes.Length/4];for(int i=0;i<words.Length;i++)words[i]=BinaryPrimitives.ReadUInt32LittleEndian(bytes[(4*i)..]);return words;}
    private static List<uint[]> Parse(ReadOnlySpan<byte> bytes)
    {
        var words=Words(bytes);var result=new List<uint[]>();
        for(int at=5;at<words.Length;){int n=(int)(words[at]>>16);if(n==0||n>words.Length-at)throw Error("truncated SPIR-V");result.Add(words.AsSpan(at,n).ToArray());at+=n;}
        return result;
    }
}
