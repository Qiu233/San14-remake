using San14.Core;
using San14.Data;

public sealed record OriginalD3D11AreaCapitalPublication(ulong Revision,OriginalD3D11AreaCapitalWorldFrame Source)
{
    public byte[] NativeBytes {get;}=Encode(Revision,Source);
    public ReadOnlyMemory<byte> PrimaryPacket=>Source.PrimaryPacket;
    public ReadOnlyMemory<byte> AccessoryPacket=>Source.AccessoryPacket;
    public IReadOnlyList<OriginalD3D11CityGateFlagMaterialKey> FlagMaterialKeys {get;} = Array.AsReadOnly(
        Source.Accessories.Where(a=>a.Material is not null).Select(a=>a.Material!.Value).Distinct().ToArray());
    public bool PrimaryReady=>Source.PrimaryReady;
    public bool AccessorySourceReady=>Source.AccessorySourceReady;
    public bool RequestsAreActualDraws=>false;
    static byte[] Encode(ulong revision,OriginalD3D11AreaCapitalWorldFrame source)
    {
        if(!source.PrimaryReady||!source.AccessorySourceReady)
            throw new InvalidDataException("Current port/prefecture inputs unavailable: "+
                string.Join("; ",source.Unavailable.Select(m=>$"{m.Identity}/{m.Field}: {m.Reason}")
                    .Concat(source.RequiredAssets.Select(m=>$"category{m.Category}: original import required ({m.SourceKey})"))));
        var keys=source.Accessories.Where(a=>a.Material is not null).Select(a=>a.Material!.Value).Distinct().ToArray();
        using var output=new MemoryStream();using var w=new BinaryWriter(output);
        w.Write(0x31574341u);w.Write(revision);w.Write(source.PrimaryPacket.Length);w.Write(source.AccessoryPacket.Length);w.Write(keys.Length);
        w.Write(source.Source.Owners.Count);w.Write(source.PrimaryPacket.Span);w.Write(source.AccessoryPacket.Span);
        foreach(var key in keys){w.Write(key.Force);w.Write(key.Color49);w.Write((uint)key.Glyph);}
        foreach(var owner in source.Source.Owners){byte[] name=System.Text.Encoding.UTF8.GetBytes(owner.Identity);w.Write(name.Length);w.Write(name);}
        return output.ToArray();
    }
}

/// <summary>Independent Area/CUnitCapital producer, not appended to City/Gate
/// or Main here. Share the already prepared19/281 bank and existing formal
/// currentWorldQueries provider; no new game/session/ABI owner is created.</summary>
public sealed class OriginalD3D11AreaCapitalPublisher
{
    readonly OriginalD3D11AreaCapitalWorldSource source;
    OriginalD3D11AreaCapitalPublication? current;ulong revision;
    public OriginalD3D11AreaCapitalPublisher(OriginalGameRepository repository,LoadedSceneResources scene,LoadedScenario scenario,
        OriginalD3D11CityGateTemplateBank preparedBank,
        Func<GameState,OriginalEventInitializationContext?> actualCurrentContextProducer)
    {
        source=new(repository,scene,scenario,preparedBank,actualCurrentContextProducer);
    }
    public OriginalD3D11AreaCapitalPublication Capture(GameState world,uint commonFrustumMode)
    {
        var frame=source.Capture(world,commonFrustumMode);
        if(current is not null&&ReferenceEquals(frame,current.Source))return current;
        return current=new(++revision,frame);
    }
}
