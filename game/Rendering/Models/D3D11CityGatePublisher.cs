using San14.Core;
using San14.Data;

/// <summary>Client-owned current City/Gate publication. All bytes are source
/// values for one immutable World; native resource loading stays separate.</summary>
public sealed record D3D11CityGatePublication(ulong Revision,D3D11CityGateWorldFrame Source,
    IReadOnlyList<D3D11CityGateFlagMaterialKey> RequiredFlagMaterials,
    D3D11FacilityPublication? Facilities=null)
{
    public byte[] NativeBytes { get; } = Encode(Revision, Source, Facilities);
    public ReadOnlyMemory<byte> PrimaryPacket=>Source.PrimaryPacket;
    public ReadOnlyMemory<byte> AccessoryPacket=>Source.AccessoryPacket;
    public bool PrimaryReady=>Source.PrimaryReady;
    public bool AccessoryGpuInputsReady=>Source.AccessorySourceReady&&RequiredFlagMaterials.Count==0;
    static byte[] Encode(ulong revision,D3D11CityGateWorldFrame source,D3D11FacilityPublication? facilities)
    {
        if(facilities is { IsReady:false })
            throw new InvalidDataException("Current facility primary publication unavailable: "+
                string.Join("; ",facilities.Missing.Select(item=>item.InstanceKey+": "+item.Reason)));
        if(!source.PrimaryReady||!source.AccessorySourceReady)
            throw new InvalidDataException("Current City/Gate visual inputs unavailable: "+
                string.Join("; ",source.Unavailable.Select(m=>$"{m.Identity}/{m.Field}: {m.Reason}")
                    .Concat(source.RequiredAssets.Select(m=>$"category{m.Category}: original asset import required ({m.SourceKey})"))));
        var keys=source.Accessories.Where(u=>u.Material is not null).Select(u=>u.Material!.Value).Distinct().ToArray();
        using var output=new MemoryStream();using var w=new BinaryWriter(output);
        w.Write(facilities is null?0x31574743u:0x32574743u);w.Write(revision);w.Write(source.PrimaryPacket.Length);w.Write(source.AccessoryPacket.Length);w.Write(keys.Length);
        if(facilities is not null)w.Write(facilities.Packet.Length);
        w.Write(source.PrimaryPacket.Span);w.Write(source.AccessoryPacket.Span);
        foreach(var key in keys){w.Write(key.Force);w.Write(key.Color49);w.Write((uint)key.Glyph);}
        if(facilities is not null)w.Write(facilities.Packet.Span);
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
        //The completion set can be replayed by every accepted Area frame.
        //Only an actual pending-material removal changes this publication;
        //otherwise Facility.Combine must keep borrowing the same encoded owner.
        if(current is not {RequiredFlagMaterials.Count:>0})return;
        var remaining=current.RequiredFlagMaterials.Where(key=>!completedFlags.Contains(key)).ToArray();
        if(remaining.Length==current.RequiredFlagMaterials.Count)return;
        current=current with{RequiredFlagMaterials=Array.AsReadOnly(remaining)};
    }
    public D3D11CityGatePublication Capture(GameState world,uint commonFrustumMode,
        IReadOnlyDictionary<string,MapStaticModelPublication>? actualNodes=null,
        IReadOnlyDictionary<int,D3D11GateDefinition>? actualGateDefinitions=null)
    {
        var frame=source.Capture(world,commonFrustumMode,actualNodes,actualGateDefinitions);
        return Publish(frame);
    }
    public D3D11CityGatePublication CaptureScenarioPreview(GameState world,uint commonFrustumMode,
        IReadOnlyDictionary<string,MapStaticModelPublication>? actualNodes=null,
        IReadOnlyDictionary<int,D3D11GateDefinition>? actualGateDefinitions=null)
        => Publish(source.CaptureScenarioPreview(world,commonFrustumMode,actualNodes,actualGateDefinitions));

    private D3D11CityGatePublication Publish(D3D11CityGateWorldFrame frame)
    {
        if(current is not null&&ReferenceEquals(frame,current.Source))return current;
        var needed=frame.Accessories.Where(u=>u.Material is not null).Select(u=>u.Material!.Value).Distinct().Where(key=>!completedFlags.Contains(key)).ToArray();
        return current=new(++revision,frame,Array.AsReadOnly(needed));
    }
}
