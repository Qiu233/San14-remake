using System.Numerics;
using San14.Core;

namespace San14.Data;

public sealed record OriginalD3D11AreaCapitalAccessory(int Index,string Identity,int NativeAreaSlot,int Force,
    OriginalUnitWorldTransformState Root,OriginalMapModelNativeOrientedBounds Bounds,
    OriginalD3D11CityGateFlagMaterialKey? Material);
public sealed record OriginalD3D11AreaCapitalWorldFrame(OriginalD3D11AreaCapitalFrame Source,
    IReadOnlyList<OriginalD3D11AreaCapitalAccessory> Accessories,
    IReadOnlyList<OriginalD3D11CityGateRequiredAsset> RequiredAssets,
    IReadOnlyList<OriginalD3D11CityGateUnavailable> Unavailable,
    ReadOnlyMemory<byte> PrimaryPacket,ReadOnlyMemory<byte> AccessoryPacket)
{
    public bool PrimaryReady=>PrimaryPacket.Length!=0;
    public bool AccessorySourceReady=>AccessoryPacket.Length!=0;
    public bool AccessoryRequestsAreActualDraws=>false;
}

/// <summary>Independent source-owned Area/CUnitCapital roster and current
/// ownership. Borrow existing actual World queries only on a World change;
/// retain P/Q/LOCAL/bounds from the original creation writer. No Main append,
/// simulation owner or device objects are constructed by this adapter.</summary>
public sealed class OriginalD3D11AreaCapitalWorldSource
{
    readonly OriginalD3D11AreaCapitalSource source;
    readonly OriginalD3D11CityGateTemplateBank bank;
    readonly Func<GameState,OriginalEventInitializationContext?> capture;
    GameState? lastWorld;object? lastCities,lastForces,lastDistricts,lastOfficers,lastTerritory,lastAreas,lastParameters,lastNativeForces;
    uint lastMode;OriginalD3D11AreaCapitalWorldFrame? current;
    public OriginalD3D11AreaCapitalSource Source=>source;
    public OriginalD3D11AreaCapitalWorldSource(OriginalGameRepository repository,LoadedSceneResources scene,LoadedScenario scenario,
        OriginalD3D11CityGateTemplateBank preparedBank,
        Func<GameState,OriginalEventInitializationContext?> actualCurrentContextProducer)
    {
        source=new(repository,scene,scenario);bank=preparedBank;capture=actualCurrentContextProducer;
    }
    public OriginalD3D11AreaCapitalWorldFrame Capture(GameState world,uint commonFrustumMode)
    {
        bool sameWorld=ReferenceEquals(world,lastWorld)||ReferenceEquals(world.Cities,lastCities)&&
            ReferenceEquals(world.Forces,lastForces)&&ReferenceEquals(world.Districts,lastDistricts)&&
            ReferenceEquals(world.Officers,lastOfficers)&&ReferenceEquals(world.Territory,lastTerritory)&&
            ReferenceEquals(world.Areas,lastAreas)&&ReferenceEquals(world.NativeArmySimulation?.Parameters,lastParameters)&&
            ReferenceEquals(world.NativeArmySimulation?.Forces,lastNativeForces);
        if(sameWorld&&current is not null){
            lastWorld=world;if(commonFrustumMode==lastMode)return current;
            // Only the source Common culling convention changed. The current
            // Area/Hex/Force owner is already captured; do not query it again.
            current=current with{PrimaryPacket=current.PrimaryReady?EncodePrimary(current.Source,commonFrustumMode):ReadOnlyMemory<byte>.Empty};
            lastMode=commonFrustumMode;return current;
        }
        var queries=capture(world)??throw new InvalidDataException("Actual current Area/Hex/Force query owner unavailable.");
        var frame=source.Capture(queries);var missing=new List<OriginalD3D11CityGateUnavailable>(frame.Unavailable);
        var required=frame.Owners.Where(o=>bank.Bind(o.Primary.Asset,o.Category) is null).DistinctBy(o=>o.Primary.Asset.SourceKey)
            .Select(o=>new OriginalD3D11CityGateRequiredAsset(o.Category,o.Primary.Asset.SourceKey,o.Primary.Asset.Draws.Count)).ToArray();
        var accessories=new List<OriginalD3D11AreaCapitalAccessory>(frame.Owners.Count);
        for(int index=0;index<frame.Owners.Count;index++){
            var owner=frame.Owners[index];OriginalD3D11CityGateFlagMaterialKey? material=null;
            if(owner.CurrentForce is >=1 and <=51){
                if(owner.CurrentPaneName is not {} pane||pane.CurrentForceColor49 is not byte color||color>60)
                    missing.Add(new(owner.Identity,"flag-material","Actual current Force49/glyph source unavailable."));
                else material=new(owner.CurrentForce,color,pane.Glyph.TwoUtf16Units[0]);
            }
            accessories.Add(new(index,owner.Identity+"/accessory",owner.NativeAreaSlot,owner.CurrentForce,
                owner.AccessoryRoot,owner.AccessoryBounds,material));
        }
        byte[] primary=frame.Unavailable.Count==0&&required.Length==0?EncodePrimary(frame,commonFrustumMode):[];
        byte[] accessory=missing.Count==0?EncodeAccessories(frame,accessories):[];
        current=new(frame,accessories.AsReadOnly(),Array.AsReadOnly(required),missing.AsReadOnly(),primary,accessory);
        lastWorld=world;lastMode=commonFrustumMode;lastCities=world.Cities;lastForces=world.Forces;lastDistricts=world.Districts;
        lastOfficers=world.Officers;lastTerritory=world.Territory;lastAreas=world.Areas;
        lastParameters=world.NativeArmySimulation?.Parameters;lastNativeForces=world.NativeArmySimulation?.Forces;
        return current;
    }
    // Existing CPU1 schema, but a separate roster/index domain. This method
    // deliberately does not invoke the fixed61 City/Gate bank Encode method.
    byte[] EncodePrimary(OriginalD3D11AreaCapitalFrame frame,uint commonFrustumMode)
    {
        using var output=new MemoryStream();using var w=new BinaryWriter(output);w.Write(0x31555043u);w.Write(frame.Owners.Count);
        for(int index=0;index<frame.Owners.Count;index++){
            var owner=frame.Owners[index];var profile=bank.Bind(owner.Primary.Asset,owner.Category)??throw new InvalidDataException("Actual capital source asset import absent.");
            w.Write(index);w.Write(profile.Index);w.Write(1u);w.Write(owner.Caller.Mask328);w.Write(owner.Caller.Mask32C);w.Write(owner.Caller.Mask330);
            w.Write(owner.Caller.ActualEligibilityV408?1u:0u);w.Write(owner.Caller.Exclusion78);w.Write(owner.Caller.WorldCasterFlags10C);w.Write(owner.Opacity);
            w.Write(OriginalMapModelNativeVisibilitySources.ResolveFrustumCullingType(owner.Prefix.ConstructorFrustumCullingType33C,commonFrustumMode));
            foreach(var v in new[]{owner.Bounds.Center0,owner.Bounds.Basis10,owner.Bounds.Basis20,owner.Bounds.Basis30,owner.Bounds.HalfExtents40})Vector(w,v);
            w.Write(profile.DrawIndices.Count);foreach(int draw in profile.DrawIndices){w.Write(draw);var t=owner.Primary.DrawTransforms[draw];foreach(float f in t.NormalRows)w.Write(f);foreach(float f in t.PositionRows)w.Write(f);}
        }
        return output.ToArray();
    }
    // Existing source accessory unit+placement grammar. Count1 is a source
    // request; camera, owner and actual player/phase publication decide draws.
    static byte[] EncodeAccessories(OriginalD3D11AreaCapitalFrame frame,IReadOnlyList<OriginalD3D11AreaCapitalAccessory> accessories)
    {
        using var output=new MemoryStream();using var w=new BinaryWriter(output);w.Write(accessories.Count);w.Write(accessories.Count);
        foreach(var a in accessories){var p=frame.Owners[a.Index].Primary.SourceWorldState!.Position18;
            w.Write(a.Index);w.Write(a.Force);w.Write(1);w.Write(p.X);w.Write(p.Y);w.Write(p.Z);}
        foreach(var a in accessories){w.Write(a.Index);w.Write(0);Vector(w,new(a.Root.Scale34,0));var q=a.Root.Quaternion24;
            Vector(w,new(q.X,q.Y,q.Z,q.W));Vector(w,new(a.Root.Position18,1));
            foreach(var v in new[]{a.Bounds.Center0,a.Bounds.Basis10,a.Bounds.Basis20,a.Bounds.Basis30,a.Bounds.HalfExtents40})Vector(w,v);}
        return output.ToArray();
    }
    static void Vector(BinaryWriter w,Vector4 v){w.Write(v.X);w.Write(v.Y);w.Write(v.Z);w.Write(v.W);}
}
