namespace San14.Data;

/// <summary>An owned native descriptor, distinct from its unchanged archive
/// declaration. C0D330 admits an explicit native pixel code when cloning a
/// descriptor; C0C770/C0BDE0/C0BE90 resolve a proxy before returning its storage.
/// Choosing when to create or install that proxy belongs to the caller.</summary>
public sealed class RenderTextureRuntimeDescriptor
{
    private RenderTextureRuntimeDescriptor(RenderTextureDefinition source,
        byte pixelFormat,byte colorSpace,byte outputColorSpace,string identity,string provenance)
    {
        RenderTextureResolutionFrame.ValidateDeclaration(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        // Validate the actual pair of native views, rather than accepting an
        // arbitrary device format or inferring it from a render target name.
        _=RenderTextureFormatMath.Resolve(pixelFormat,colorSpace,outputColorSpace);
        Declaration=source;PixelFormat=pixelFormat;ColorSpace=colorSpace;
        OutputColorSpace=outputColorSpace;OwnerIdentity=identity;SourceProvenance=provenance;
    }
    public RenderTextureDefinition Declaration{get;}
    public string OwnerIdentity{get;}
    public string SourceProvenance{get;}
    public byte PixelFormat{get;}
    public byte ColorSpace{get;}
    public byte OutputColorSpace{get;}
    public RenderTextureKind Kind=>Declaration.Kind;
    public int Width=>Declaration.Width;
    public int Height=>Declaration.Height;
    public int? ArrayDepth=>Declaration.ArrayDepth;
    public float MipLodBias=>Declaration.MipLodBias;
    public byte LevelNum=>Declaration.LevelNum;
    public byte MinMipLevel=>Declaration.MinMipLevel;
    public byte MultiSample=>Declaration.MultiSample;
    public byte AddressU=>Declaration.AddressU;
    public byte AddressV=>Declaration.AddressV;
    public byte MagFilter=>Declaration.MagFilter;
    public byte MinFilter=>Declaration.MinFilter;
    public byte MipFilter=>Declaration.MipFilter;
    public byte AnisoSampleNum=>Declaration.AnisoSampleNum;
    public byte LocationHint=>Declaration.LocationHint;
    public byte NeverInternalCopy=>Declaration.NeverInternalCopy;
    public byte? NeedSliceTargets=>Declaration.NeedSliceTargets;
    public byte? NeedAllTargets=>Declaration.NeedAllTargets;
    public uint? ArrayFlag=>Declaration.ArrayFlag;

    public static RenderTextureRuntimeDescriptor ConstructDeclared(
        RenderTextureDefinition source,string ownerIdentity)=>
        new(source,source.PixelFormat,source.ColorSpace,source.OutputColorSpace,ownerIdentity,
            "owned-source-declaration;"+source.Source.Source.ResourceUri);

    /// <summary>Explicit owned clone with a native format and separately chosen
    /// view color spaces. C0D330 proves the native-code override (7D retains
    /// the source code at C0D363..C0D372); its unchanged view flags are not a
    /// source for these caller-supplied color-space choices. This operation
    /// neither installs a Game proxy nor alters the archive declaration.</summary>
    public static RenderTextureRuntimeDescriptor CloneWithNativeFormat(
        RenderTextureRuntimeDescriptor source,byte explicitNativeCode,
        byte explicitColorSpace,byte explicitOutputColorSpace,string ownerIdentity,string callerProvenance)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(callerProvenance);
        byte code=explicitNativeCode==0x7D?source.PixelFormat:explicitNativeCode;
        return new(source.Declaration,code,explicitColorSpace,explicitOutputColorSpace,ownerIdentity,
            "C0D330-native-code-rule;explicit-owned-view-color-spaces;source-owner="+source.OwnerIdentity+";"+callerProvenance);
    }
}

public sealed record RenderTextureResolution(RenderTextureDefinition Declared,
    RenderTextureRuntimeDescriptor Effective)
{
    public string EffectiveOwnerIdentity=>Effective.OwnerIdentity;
    /// <summary>True only for an explicitly supplied terminal proxy. A fallback
    /// declaration still permits the existing independent backend depth mirror;
    /// it is not a request to merge allocations across two frame owners.</summary>
    public bool IsExplicitTerminalProxy{get;init;}
}

/// <summary>Immutable allocation-generation snapshot of explicitly selected
/// owned terminal RT descriptors. It preserves logical names and declaration
/// bytes. It neither discovers native objects nor infers a proxy from a format.
/// Resize may reuse this snapshot: caller extents remain a separate input.</summary>
public sealed class RenderTextureResolutionFrame
{
    private readonly Dictionary<uint,RenderTextureResolution> entries;
    private RenderTextureResolutionFrame(string generation,
        Dictionary<uint,RenderTextureResolution> entries)
    {GenerationIdentity=generation;this.entries=entries;}
    public string GenerationIdentity{get;}
    public static RenderTextureResolutionFrame CreateExplicitOwned(
        IReadOnlyList<RenderTextureDefinition> declarations,
        IReadOnlyDictionary<uint,RenderTextureRuntimeDescriptor> selectedTerminalProxies,
        string generationIdentity)
    {
        ArgumentNullException.ThrowIfNull(declarations);ArgumentNullException.ThrowIfNull(selectedTerminalProxies);
        ArgumentException.ThrowIfNullOrWhiteSpace(generationIdentity);
        var result=new Dictionary<uint,RenderTextureResolution>();
        foreach(var declared in declarations)
        {
            ValidateDeclaration(declared);
            uint key=declared.Object.ObjectId;
            bool selected=selectedTerminalProxies.TryGetValue(key,out var proxy);
            var terminal=selected
                ?proxy??throw new InvalidDataException("A nonnull proxy cannot fall back to the owned declaration when its CPU is absent.")
                :RenderTextureRuntimeDescriptor.ConstructDeclared(declared,generationIdentity+"/declared/"+key.ToString("X8"));
            if(!result.TryAdd(key,new(declared,terminal){IsExplicitTerminalProxy=selected}))
                throw new InvalidDataException("Resolution snapshot has duplicate logical texture names.");
        }
        if(result.Count==0||selectedTerminalProxies.Keys.Any(key=>!result.ContainsKey(key)))
            throw new InvalidDataException("Resolution snapshot contains no declarations or an unknown proxy source name.");
        // A shared terminal must mean one concrete descriptor, not two callers
        // assigning the same identity to different allocations.
        foreach(var group in result.Values.GroupBy(r=>r.EffectiveOwnerIdentity))
            if(group.Any(r=>!ReferenceEquals(r.Effective,group.First().Effective)))
                throw new InvalidDataException("One terminal identity names distinct owned descriptors.");
        return new(generationIdentity,result);
    }
    public RenderTextureResolution Resolve(RenderTextureDefinition declared)
    {
        ValidateDeclaration(declared);
        if(!entries.TryGetValue(declared.Object.ObjectId,out var entry)||!SameDeclaration(declared,entry.Declared))
            throw new InvalidDataException("Texture is absent from this exact source resolution generation.");
        ValidateDeclaration(entry.Effective.Declaration);
        return new(declared,entry.Effective){IsExplicitTerminalProxy=entry.IsExplicitTerminalProxy};
    }
    public void ValidateAgainstLoadedTargets(IReadOnlyList<RenderTextureDefinition> declarations)
    {
        ArgumentNullException.ThrowIfNull(declarations);
        if(declarations.Count==0||declarations.Select(d=>d.Object.ObjectId).Distinct().Count()!=declarations.Count)
            throw new InvalidDataException("Target validation requires a nonempty unique source subset.");
        foreach(var declaration in declarations)_=Resolve(declaration);
    }
    internal static void ValidateDeclaration(RenderTextureDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(RenderTextureResources.Read(source.Source,source.Object.ObjectId)!=source)
            throw new InvalidDataException("Runtime target selection cannot rewrite its archive declaration.");
    }
    private static bool SameDeclaration(RenderTextureDefinition a,RenderTextureDefinition b)=>
        a.Source.Source.ResourceUri==b.Source.Source.ResourceUri&&
        a.Source.Source.DecodedSha256==b.Source.Source.DecodedSha256&&
        a.Object.ObjectId==b.Object.ObjectId&&a.Object.ClassId==b.Object.ClassId&&
        (a with{Source=b.Source,Object=b.Object})==b;
}
