using System.Numerics;
using San14.Core;

namespace San14.Data;

public sealed record FacilitySceneConnection(MapObjectReceiverId ReceiverId, MapObjectReceiverId AdjacentReceiverId);
public sealed record FacilitySceneModel(string Identity, int ReceiverId, int Category, MapStaticModelInstance Instance);
public sealed record FacilitySceneModelFrame(IReadOnlyList<FacilitySceneModel> Rows,
    IReadOnlyList<MapStaticModelInstanceFailure> Missing)
{
    public IReadOnlyList<int> KnownReceivers { get; init; } = Array.Empty<int>();
}

/// <summary>CUnitObject and CUnitSubObject model selection and finite transform
/// writers. Connections retain the two actual Object receivers in caller order.</summary>
public static class FacilitySceneModelTransforms
{
    public static int Category(int definition, bool completed) => CeFacilityModels.CategoryFor(definition,completed);

    public static int ConnectionCategory(int definition) => definition switch
        { 9 => 86, 10 => 87, 16 => 88, _ => throw new InvalidDataException("facility.connection.definition_category") };

    public static UnitWorldTransformState Connection(LoadedModelTextureResources resources,
        int fromHex, int toHex, UnitGeometry geometry, Func<float,float,float?> height)
    {
        var a = geometry.NativeHexPosition187A40(fromHex);
        var b = geometry.NativeHexPosition187A40(toHex);
        float x = (a.X + b.X) * .5f, z = (a.Z + b.Z) * .5f;
        float centerY = Ground(x,z), ay = Ground(a.X,a.Z), by = Ground(b.X,b.Z);
        float yaw = ArmyDirectionSource.YawRadians2AED40(ArmyDirectionSource.Direction2AEBC0(fromHex,toHex));
        var half = NativeTrigonometry.SinCos014590(yaw * .5f);
        var yawQuaternion = new Quaternion(0,half.Sin,0,half.Cos);
        float dx = a.X-b.X, dz = a.Z-b.Z;
        int distance = IntegerRoot(unchecked((int)(dx*dx + dz*dz)));
        float slope = MathF.Atan2(ay-by,distance);
        float axisYaw = yaw + 1.5707964f;
        if (axisYaw >= 6.2831855f) axisYaw -= 6.2831855f;
        var axis = NativeTrigonometry.SinCos014590(axisYaw);
        var tilt = NativeTrigonometry.SinCos014590(slope);
        //1C67D0 builds the signed half-angle from the supplied sine/cosine.
        float w = MathF.Sqrt(tilt.Cos*.5f + .5f);
        float sinHalf = MathF.Sqrt(.5f - tilt.Cos*.5f);
        if (tilt.Sin < 0) sinHalf = -sinHalf;
        var q = Multiply(new(axis.Sin*sinHalf,0,axis.Cos*sinHalf,w),yawQuaternion);
        float inverse = 1f / MathF.Sqrt(q.X*q.X+q.Y*q.Y+q.Z*q.Z+q.W*q.W);
        q = new(q.X*inverse,q.Y*inverse,q.Z*inverse,q.W*inverse);
        return UnitWorldTransform.ConstructDefinition(resources).PublishPosition(new(x,centerY,z))
            .PublishQuaternion(q).PublishScale(new(1.5f)) with
            { Basis = "CUnitSubObject ordered endpoint midpoint, clamped ground, direction and slope quaternion" };

        float Ground(float px,float pz)
        {
            float value = height(px,pz) ?? throw new InvalidDataException("facility.connection.ground");
            if (!float.IsFinite(value)) throw new InvalidDataException("facility.connection.nonfinite_ground");
            return Math.Max(445f,value);
        }
    }

    private static int IntegerRoot(int value)
    {
        if (value <= 0) return 0;
        int root = 1, bound = value;
        while (root < bound) { root *= 2; bound /= 2; }
        int previous;
        do { previous=root; root=(value/previous+previous)/2; } while(root<previous);
        return previous;
    }

    private static Quaternion Multiply(Quaternion a,Quaternion b) => new(
        (b.Z*a.Y-a.Z*b.Y)+a.X*b.W+b.X*a.W,
        (a.Z*b.X-b.Z*a.X)+a.Y*b.W+b.Y*a.W,
        (a.X*b.Y-b.X*a.Y)+a.Z*b.W+b.Z*a.W,
        b.W*a.W-(b.X*a.X+b.Y*a.Y+b.Z*a.Z));
}

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState? facilitySceneOwner;
    private SourceWorldContext? facilitySceneCurrent;
    private SourceWorldContext? facilityFrameCurrent;
    private MapStaticModelAssetCatalog? facilityFrameAssets;
    private UnitGeometry? facilityFrameGeometry;
    private MapStaticModelGroundHeightSource? facilityFrameGround;
    private IReadOnlyList<FacilitySceneConnection>? facilityFrameConnections;
    private FacilitySceneModelFrame? facilityFrame;

    /// <summary>Map-entry1C0870's ordered pair construction inputs. It uses
    /// the native Object registry,274B00 radius order and unordered-pair
    /// exclusion. Model creation belongs to the actual renderer afterwards.</summary>
    public IReadOnlyList<FacilitySceneConnection> QueryInitialFacilitySceneConnections(GameState world)
    {
        var current=FacilitySceneCurrent(world);
        var directory=current.CurrentMapObjectDirectory ?? throw new InvalidDataException("facility.connection.directory");
        var result=new List<FacilitySceneConnection>();
        var seen=new HashSet<(int,int)>();
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Object)
            ?? throw new InvalidDataException("facility.connection.registry"))
        {
            if (entry is not { Kind:RegistryKind.Object,PhysicalSlot:int at })
                throw new InvalidDataException("facility.connection.registry_pointer");
            var source=current.ReadPhysicalObject(at) ?? throw new InvalidDataException("facility.connection.source_fields");
            if (source.RawField12 is not (9 or 10 or 16)) continue;
            var definition=current.ReadObjectDefinitionReference(source.RawField12)
                ?? throw new InvalidDataException("facility.connection.definition");
            if (!(current.ReadObjectDefinitionValidity(definition)
                ?? throw new InvalidDataException("facility.connection.definition_validity"))) continue;
            foreach (int key in HexNeighborhood.RadiusOneCandidates(source.RawField10<48400?source.RawField10:0))
            {
                var hex=current.ReadHexTableReference(key) ?? throw new InvalidDataException("facility.connection.neighbor_Hex");
                if (hex.PhysicalSlot is null) continue;
                ushort link=current.ReadHexObjectLink(hex) ?? throw new InvalidDataException("facility.connection.neighbor_link");
                var neighbor=current.ReadObjectTableReference(link<=3000?link:0)
                    ?? throw new InvalidDataException("facility.connection.neighbor_Object");
                if (!(current.ReadObjectNativeValidity(neighbor)
                    ?? throw new InvalidDataException("facility.connection.neighbor_validity"))) continue;
                int other=neighbor.PhysicalSlot ?? throw new InvalidDataException("facility.connection.neighbor_identity");
                if ((current.ReadPhysicalObject(other)
                    ?? throw new InvalidDataException("facility.connection.neighbor_fields")).RawField12!=source.RawField12) continue;
                if (seen.Add((Math.Min(at,other),Math.Max(at,other))))
                    result.Add(new(directory.ReceiverAtPhysical(at),directory.ReceiverAtPhysical(other)));
            }
        }
        return result.AsReadOnly();
    }

    public FacilitySceneModelFrame QueryFacilitySceneModels(GameState world, MapStaticModelAssetCatalog assets,
        UnitGeometry geometry, MapStaticModelGroundHeightSource ground, IReadOnlyList<FacilitySceneConnection> connections)
    {
        ArgumentNullException.ThrowIfNull(connections);
        var current = FacilitySceneCurrent(world);
        if (facilityFrame is not null && ReferenceEquals(current,facilityFrameCurrent) &&
            ReferenceEquals(assets,facilityFrameAssets) && ReferenceEquals(geometry,facilityFrameGeometry) &&
            ReferenceEquals(ground,facilityFrameGround) && ReferenceEquals(connections,facilityFrameConnections)) return facilityFrame;
        var directory = current.CurrentMapObjectDirectory ?? throw new InvalidDataException("facility.model.directory");
        var rows = new List<FacilitySceneModel>();
        var known = new List<int>();
        var missing = new List<MapStaticModelInstanceFailure>();
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Object)
            ?? throw new InvalidDataException("facility.model.registry"))
        {
            if (entry is not { Kind:RegistryKind.Object, PhysicalSlot:int physical })
                throw new InvalidDataException("facility.model.registry_pointer");
            var obj = new ObjectReference(physical);
            string identity = "facility:" + directory.ReceiverAtPhysical(physical).Value;
            try
            {
                if (!Required(current.ReadObjectNativeValidity(obj),"validity")) continue;
                int kind = (current.ReadPhysicalObject(physical)
                    ?? throw new InvalidDataException("facility.model.source_fields")).RawField12;
                if (kind is < 3 or > 17) continue;
                known.Add(directory.ReceiverAtPhysical(physical).Value);
                if (!(ArmyOperationContext.IsNewGamePreview(world)
                    ? current.IsObjectUnitModelVisible(obj) : ConstructionModelIsRequired(world,current,obj))) continue;
                int hex = Required(current.ReadObjectHexIndex(obj),"Hex");
                bool completed = (Required(current.ReadMapObjectFields16(obj),"activation")&1)!=0;
                int category = FacilitySceneModelTransforms.Category(kind,completed);
                var asset = assets.LoadFacilityCategory(category);
                var pose = UnitWorldTransform.CreateFacilityPrimary(asset.TextureResources,hex,geometry,ground.Query);
                rows.Add(new(identity,directory.ReceiverAtPhysical(physical).Value,category,
                    MapStaticModelInstanceFactory.FromNativeWorldState(identity,asset,pose,identity)));
            }
            catch (InvalidDataException error) { missing.Add(new(identity,error.Message)); }
        }
        foreach (var connection in connections)
        {
            string identity = "facility-connection:"+connection.ReceiverId.Value+":"+connection.AdjacentReceiverId.Value;
            try
            {
                var from = new ObjectReference(directory.PhysicalOf(connection.ReceiverId));
                var to = new ObjectReference(directory.PhysicalOf(connection.AdjacentReceiverId));
                if (!Required(current.ReadObjectNativeValidity(from),"connection_from_validity") ||
                    !Required(current.ReadObjectNativeValidity(to),"connection_to_validity")) continue;
                int kind = (current.ReadPhysicalObject(from.PhysicalSlot!.Value)
                    ?? throw new InvalidDataException("facility.connection.from_fields")).RawField12;
                if ((current.ReadPhysicalObject(to.PhysicalSlot!.Value)
                    ?? throw new InvalidDataException("facility.connection.to_fields")).RawField12 != kind)
                    throw new InvalidDataException("facility.connection.same_definition");
                int category = FacilitySceneModelTransforms.ConnectionCategory(kind);
                var asset = assets.LoadFacilityCategory(category);
                var pose = FacilitySceneModelTransforms.Connection(asset.TextureResources,
                    Required(current.ReadObjectHexIndex(from),"connection_from_Hex"),
                    Required(current.ReadObjectHexIndex(to),"connection_to_Hex"),geometry,ground.Query);
                rows.Add(new(identity,connection.ReceiverId.Value,category,
                    MapStaticModelInstanceFactory.FromNativeWorldState(identity,asset,pose,identity)));
            }
            catch (InvalidDataException error) { missing.Add(new(identity,error.Message)); }
        }
        facilityFrameCurrent=current; facilityFrameAssets=assets; facilityFrameGeometry=geometry;
        facilityFrameGround=ground; facilityFrameConnections=connections;
        return facilityFrame=new(rows.AsReadOnly(),missing.AsReadOnly()) { KnownReceivers=known.AsReadOnly() };

        static T Required<T>(T? value,string field) where T:struct => value
            ?? throw new InvalidDataException("facility.model."+field);
    }

    private SourceWorldContext FacilitySceneCurrent(GameState world)
    {
        if (facilitySceneOwner is not { } old ||
            ArmyOperationContext.IsNewGamePreview(old)!=ArmyOperationContext.IsNewGamePreview(world) ||
            !ReferenceEquals(old.MapObjects,world.MapObjects) ||
            !ReferenceEquals(old.Structures,world.Structures) ||
            !ReferenceEquals(CampaignSourceKnowledge.From(old.ResourceState).MapObjectSource,
                CampaignSourceKnowledge.From(world.ResourceState).MapObjectSource) ||
            !ReferenceEquals(old.Forces,world.Forces) || OwnedCurrentPlayerForce.ReadRawForce3A(old)!=OwnedCurrentPlayerForce.ReadRawForce3A(world) ||
            !ReferenceEquals(old.NativeArmySimulation?.Forces,world.NativeArmySimulation?.Forces))
        {
            facilitySceneCurrent=ArmyOperationContext.IsNewGamePreview(world)
                ? CaptureNewGameStaticObjectVisualDefinitions(world) : CaptureOwnedArmyReturnWorld(world).Context;
            facilitySceneOwner=world;
        }
        return facilitySceneCurrent!;
    }
}
