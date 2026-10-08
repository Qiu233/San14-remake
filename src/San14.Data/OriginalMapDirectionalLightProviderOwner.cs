using System.Security.Cryptography;

namespace San14.Data;

/// <summary>Explicit CB7A70 inputs after the caller has resolved its current
/// Aux/Camera controls. These are not inferred from a source declaration.</summary>
public sealed record OriginalDirectionalProviderControls(int FirstIndex, int PairFirstIndex,
    int PairSecondIndex, float ScalarFirst, float ScalarSecond, float LocationX, float LocationZ,
    bool TemporaryDirectionalAvailable, bool ExternalCameraOverridePresent);

public sealed record OriginalDirectionalProviderSelection(uint RegisteredRootObjectId,
    uint SelectedObjectId, string NativeBranch, byte? SpatialIndex);

/// <summary>Root registration and selected shader-data ownership remain distinct.
/// CE35F0 selects first; CE3870 copies the selected CPU+50 and then applies CBC770.</summary>
public sealed class OriginalDirectionalProviderCurrentFrame
{
    private readonly OriginalMapDirectionalLightProviderOwner owner;
    private readonly OriginalDirectionalProviderControls controls;
    private readonly byte[] camera;
    internal OriginalDirectionalProviderCurrentFrame(OriginalMapDirectionalLightProviderOwner source,
        OriginalDirectionalLightConstructionPacket root, OriginalDirectionalProviderControls input,
        OriginalDirectionalProviderSelection selection, OriginalDirectionalLightConstructionPacket selected,
        ReadOnlySpan<byte> preparedCamera)
    {
        owner=source;RegisteredRoot=root;controls=input;Selection=selection;SelectedConstruction=selected;
        camera=preparedCamera.ToArray();CurrentCamera=OriginalDirectionalLightConstruction.ApplyCurrentCamera(selected,camera);
    }
    public OriginalDirectionalLightConstructionPacket RegisteredRoot {get;}
    public OriginalDirectionalLightConstructionPacket SelectedConstruction {get;}
    public OriginalDirectionalProviderSelection Selection {get;}
    public OriginalDirectionalLightCurrentCameraPacket CurrentCamera {get;}
    public string ProviderSourceProvenance=>owner.SourceProvenance;
    public void ValidateCurrentSourceFrame()
    {
        var rebuilt=owner.Select(RegisteredRoot,controls);
        if(rebuilt!=Selection||owner.SelectedConstruction(RegisteredRoot,rebuilt).PreparedSha256!=SelectedConstruction.PreparedSha256)
            throw new InvalidDataException("Directional provider selection differs from its owned source and ready child generation.");
        CurrentCamera.ValidateCurrentSourceFrame();
        if(!ReferenceEquals(CurrentCamera.Construction,SelectedConstruction))
            throw new InvalidDataException("Directional provider Camera application uses a different selected constructor.");
    }
}

/// <summary>Actual seasonal provider ListContainer, typed selector, and raw
/// selector-map CPU owner. Handles identify this remake's owned CPU data; they
/// are neither original pointers nor GPU RIDs. No original runtime readiness is claimed.</summary>
public sealed partial class OriginalMapDirectionalLightProviderOwner
{
    public const string SelectorMapSha256="0FBBA07A833D4DCFC7024EAF313661A0BA8F80A05C6D29B8801C612E10E60DEE";
    private static long nextHandle;
    private readonly OriginalMapSeasonDirectionalLightSourceSet source;
    private readonly byte[] map;
    private readonly Dictionary<uint,OriginalDirectionalLightResourceBinding> bindings;
    private readonly Dictionary<uint,OriginalDirectionalLightConstructionPacket> children=[];
    private OriginalMapDirectionalLightProviderOwner(OriginalMapSeasonDirectionalLightSourceSet value,byte[] bytes)
    {
        source=value;map=bytes.ToArray();
        SourceProvenance=$"owned-Directional-provider-CPU:{Guid.NewGuid():N};{source.Source.ResourceUri}@{source.Source.DecodedSha256};raw-map={SelectorMapSha256};B47F34..B47F4B";
        bindings=source.Roots.ToDictionary(r=>r.Root.ObjectId,r=>new OriginalDirectionalLightResourceBinding(
            r.ProviderListObjectId,checked((ulong)Interlocked.Increment(ref nextHandle)),SourceProvenance,true,
            OriginalMapSeasonDirectionalLightSources.ListContainerClassId));
    }
    public string SourceProvenance {get;}
    public string SourceUri=>source.Source.ResourceUri;
    public string SourceSha256=>source.Source.DecodedSha256;
    public uint SelectorMapKey=>source.Roots[0].Selector.ObjectKey;
    public static OriginalMapDirectionalLightProviderOwner Load(OriginalGameRepository repository,
        OriginalMapSeasonDirectionalLightSourceSet season)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(season);
        var canonical=OriginalMapSeasonDirectionalLightSources.Read(season.Database);
        uint mapKey=canonical.Roots[0].Selector.ObjectKey;
        if(mapKey is not (0x7BDB0B4D or 0xD998C20E or 0x2349B8D4 or 0xF7F94922))
            throw new NotSupportedException("Directional provider raw selector map requires another exact source binding.");
        var asset=repository.LoadRdbAsset("RRPreview",mapKey);
        if(asset.ResourceUri!=GameResourceId.Rdb("RRPreview",mapKey).ToString()||asset.FileId!=mapKey||
            asset.Bytes.Length!=128*128||asset.DecodedSha256!=SelectorMapSha256||
            Convert.ToHexString(SHA256.HashData(asset.Bytes))!=SelectorMapSha256)
            throw new InvalidDataException("Directional selector raw grid differs from its exact source asset.");
        foreach(var r in canonical.Roots)
        {
            var s=r.Selector;
            if(s.ObjectKey!=mapKey||!s.FirstWords.SequenceEqual(new uint[]{128,128})||
                !s.SecondWords.SequenceEqual(new uint[]{640,640})||!s.Scalars.SequenceEqual(new float[]{0,0})||
                s.FirstAxis.Count!=0||s.SecondAxis.Count!=0||r.Children.Count!=4)
                throw new NotSupportedException("Directional provider needs another explicit selector source consumer.");
        }
        return new(canonical,asset.Bytes);
    }
    public OriginalDirectionalLightResourceBinding GetBinding(uint registeredRootObjectId)
        =>bindings.TryGetValue(registeredRootObjectId,out var b)?b:
            throw new InvalidDataException("Directional provider binding requires an actual seasonal root.");

    /// <summary>Only a fully owned actual child constructor is registered ready.
    /// Absence is an implementation boundary, not an assertion of native null CPU.</summary>
    public void RegisterReadyChild(OriginalDirectionalLightConstructionPacket child)
    {
        ArgumentNullException.ThrowIfNull(child);ValidateSource(child);
        if(!source.Roots.SelectMany(r=>r.Children).Any(c=>c.ObjectId==child.SourceObjectId)||child.Metadata.ProviderObjectKey!=0)
            throw new InvalidDataException("Ready Directional child does not belong to this actual provider graph.");
        if(children.ContainsKey(child.SourceObjectId))throw new InvalidDataException("Ready Directional child generation is already registered.");
        children.Add(child.SourceObjectId,child);
    }
    public OriginalDirectionalProviderSelection Select(OriginalDirectionalLightConstructionPacket root,
        OriginalDirectionalProviderControls input)
    {
        ArgumentNullException.ThrowIfNull(root);ArgumentNullException.ThrowIfNull(input);
        ValidateRootBinding(root);
        var provider=source.Roots.Single(r=>r.Root.ObjectId==root.SourceObjectId);
        OriginalDirectionalProviderSelection Root(string branch,byte? index=null)=>new(root.SourceObjectId,root.SourceObjectId,branch,index);
        OriginalDirectionalProviderSelection Child(int index,string branch,byte? spatial=null)
        {
            uint id=provider.Children[index-1].ObjectId;
            if(!children.ContainsKey(id))throw new NotSupportedException($"Selected Directional child {id:X8} needs its actual owned ready CPU constructor.");
            return new(root.SourceObjectId,id,branch,spatial);
        }
        // CB7B0E..34 precedes all selector/current Camera overrides.
        if(input.FirstIndex>0&&input.FirstIndex<5)return Child(input.FirstIndex,"CB7B18 direct ready child");
        if(input.FirstIndex==0||(input.PairFirstIndex==0&&input.PairSecondIndex<0))return Root("CB7B3C..71 selector bypass");
        if(input.ExternalCameraOverridePresent)
            throw new NotSupportedException("Current Camera v4C0 external provider override requires its own typed selection owner.");
        if(input.PairFirstIndex>=0&&input.PairSecondIndex>=0)
            throw new NotSupportedException("CDC980 explicit Directional pair interpolation requires its own current temporary CPU owner.");
        if(!float.IsFinite(input.ScalarFirst)||!float.IsFinite(input.ScalarSecond)||
            !float.IsFinite(input.LocationX)||!float.IsFinite(input.LocationZ))
            throw new InvalidDataException("Directional provider consumed controls must be finite.");
        if(input.ScalarFirst>=0&&input.ScalarFirst<=24&&input.ScalarSecond>=0&&input.ScalarSecond<=1&&input.TemporaryDirectionalAvailable)
            return Root("CB7E1F B479B0 empty first axis -> CB8382");
        byte spatial=input.PairFirstIndex>0?unchecked((byte)(input.PairFirstIndex-1)):
            map[Cell(input.LocationZ)*128+Cell(input.LocationX)];
        return spatial>=4?Root("CB8344..CB8370 spatial byte outside child count",spatial):
            Child(spatial+1,"CB8372..CB8391 spatial ready child",spatial);
    }
    private static int Cell(float location)
    {
        // B47870 uses cvttss2si then unsigned integer DIV, followed by signed
        // clamping. Reject unsupported indefinite cvtt results explicitly.
        if(location< int.MinValue||(double)location>=2147483648d)
            throw new NotSupportedException("Directional selector location exceeds the supported signed32 cvtt range.");
        uint quotient=unchecked((uint)(int)location)/640u;
        return Math.Clamp(unchecked((int)quotient),0,127);
    }
    public OriginalDirectionalProviderCurrentFrame PrepareCurrentFrame(OriginalDirectionalLightConstructionPacket root,
        OriginalDirectionalProviderControls input,ReadOnlySpan<byte> preparedCameraPrefix)
    {
        var selected=Select(root,input);
        return new(this,root,input,selected,SelectedConstruction(root,selected),preparedCameraPrefix);
    }
    internal OriginalDirectionalLightConstructionPacket SelectedConstruction(OriginalDirectionalLightConstructionPacket root,
        OriginalDirectionalProviderSelection selected)=>selected.SelectedObjectId==root.SourceObjectId?root:children[selected.SelectedObjectId];
    private void ValidateSource(OriginalDirectionalLightConstructionPacket value)
    {
        if(value.SourceUri!=SourceUri||value.SourceSha256!=SourceSha256)
            throw new InvalidDataException("Directional provider and selected constructor have different actual season sources.");
    }
    private void ValidateRootBinding(OriginalDirectionalLightConstructionPacket root)
    {
        ValidateSource(root);var expected=GetBinding(root.SourceObjectId);
        if(root.Metadata.ProviderObjectKey!=expected.SourceObjectKey||!root.ResourceBindings.Contains(expected))
            throw new InvalidDataException("Directional root constructor does not retain this owned ready provider Data.");
    }
}
