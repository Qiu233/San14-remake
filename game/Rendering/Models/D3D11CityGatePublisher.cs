using San14.Core;
using San14.Data;

/// <summary>Client-owned current City/Gate publication. All bytes are source
/// values for one immutable World; native resource loading stays separate.</summary>
public sealed record D3D11CityGatePublication(ulong Revision,D3D11CityGateWorldFrame Source,
    IReadOnlyList<D3D11CityGateFlagMaterialKey> RequiredFlagMaterials)
{
    public byte[] NativeBytes { get; } = Encode(Revision, Source);
    public ReadOnlyMemory<byte> PrimaryPacket=>Source.PrimaryPacket;
    public ReadOnlyMemory<byte> AccessoryPacket=>Source.AccessoryPacket;
    public bool PrimaryReady=>Source.PrimaryReady;
    public bool AccessoryGpuInputsReady=>Source.AccessorySourceReady&&RequiredFlagMaterials.Count==0;
    static byte[] Encode(ulong revision,D3D11CityGateWorldFrame source)
    {
        if(!source.PrimaryReady||!source.AccessorySourceReady)
            throw new InvalidDataException("Current City/Gate visual inputs unavailable: "+
                string.Join("; ",source.Unavailable.Select(m=>$"{m.Identity}/{m.Field}: {m.Reason}")
                    .Concat(source.RequiredAssets.Select(m=>$"category{m.Category}: original asset import required ({m.SourceKey})"))));
        var keys=source.Accessories.Where(u=>u.Material is not null).Select(u=>u.Material!.Value).Distinct().ToArray();
        using var output=new MemoryStream();using var w=new BinaryWriter(output);
        w.Write(0x31574743u);w.Write(revision);w.Write(source.PrimaryPacket.Length);w.Write(source.AccessoryPacket.Length);w.Write(keys.Length);
        w.Write(source.PrimaryPacket.Span);w.Write(source.AccessoryPacket.Span);
        foreach(var key in keys){w.Write(key.Force);w.Write(key.Color49);w.Write((uint)key.Glyph);}
        return output.ToArray();
    }
}

/// <summary>Publishes current City/Gate instances against their prepared
/// resources. AreaCapital has its own population publisher.</summary>
public sealed class D3D11CityGatePublisher
{
    readonly D3D11CityGateWorldSource source;
    readonly HashSet<D3D11CityGateFlagMaterialKey> completedFlags=[];
    D3D11CityGatePublication? current;ulong revision;
    public D3D11CityGateTemplateBank PreparedBank{get;}
    public D3D11CityGatePublisher(GameResourceRepository repository,LoadedSceneResources scene,LoadedScenario scenario,
        LoadedMap map,NewGameOptions options,Func<GameState,SourceWorldContext?> actualCurrentContextProducer,
        D3D11CityGateTemplateBank primaryTemplateBank,CityGateAccessoryResources accessories)
    {
        PreparedBank=primaryTemplateBank;
        source=new(repository,scene,scenario,map,options,PreparedBank,accessories,actualCurrentContextProducer);
    }
    /// <summary>Call at actual glyph/material creation or replacement completion,
    /// using its real Force/color49/oneUTF16 key. A force ID alone is insufficient.</summary>
    public void PublishCompletedFlagMaterials(IEnumerable<D3D11CityGateFlagMaterialKey> actualCompleted){
        foreach(var key in actualCompleted){if(key.Force is <1 or >51||key.Color49 is <0 or >60)
                throw new InvalidDataException("Completed flag material source identity out of range.");completedFlags.Add(key);}
        if(current is not null)current=current with{
            RequiredFlagMaterials=Array.AsReadOnly(current.RequiredFlagMaterials.Where(key=>!completedFlags.Contains(key)).ToArray())};
    }
    public D3D11CityGatePublication Capture(GameState world,uint commonFrustumMode,
        IReadOnlyDictionary<string,MapStaticModelPublication>? actualNodes=null,
        IReadOnlyDictionary<int,D3D11GateDefinition>? actualGateDefinitions=null)
    {
        var frame=source.Capture(world,commonFrustumMode,actualNodes,actualGateDefinitions);
        if(current is not null&&ReferenceEquals(frame,current.Source))return current;
        var needed=frame.Accessories.Where(u=>u.Material is not null).Select(u=>u.Material!.Value).Distinct().Where(key=>!completedFlags.Contains(key)).ToArray();
        return current=new(++revision,frame,Array.AsReadOnly(needed));
    }
}
