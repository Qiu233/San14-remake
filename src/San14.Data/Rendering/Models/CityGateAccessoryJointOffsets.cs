using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

public sealed record AccessoryJointWorld(int JointIndex, Vector4 Scale,
    Quaternion Rotation, Vector4 Translation, Matrix4x4 Matrix, byte[] Sqt48);

/// <summary>Finite source-owned temporary offset World. D9E890 starts1F0/150
/// null, the actual empty IsUnitBoneTranslation array supplies null1D0;
/// DA6C70 loads P/Q/S, DBFA40's Flag0 branch installs
/// World150, D9E630 binds pose28 to this World and BD8FA0 copies defaults to10.
/// This is not an animation update or an active original-process World.</summary>
public sealed class LoadedCityGateAccessoryJointOffsets
{
    private readonly byte[] pose10;
    public LoadedModelTransforms Model { get; }
    public uint DefinitionObjectId { get; }
    public UnitWorldTransformState RootWorld { get; }
    public string InstallationEvidence => "1C6890-kind2/1C3D00-B3A4A0/B3A6A0/B3A840-source-factory-v90/v58-DA6C70;"+
        "D9E890-1F0/150/1C0-null;DA7339/DA73AE-empty-IsUnitBoneTranslation-supplies-null1D0;"+
        "DA6C70-normalized-source-P/Q/S;factory-v78-DC3D00-DBFA40-Flag0-forces150;D9E630-pose10-and-root28=actualWorld;BD8FA0-default48-copy;"+
        "DAE2C0-World150-before1F0;BD9440-native-ancestor-matrix-getter";

    private LoadedCityGateAccessoryJointOffsets(LoadedModelTransforms model,
        uint definitionObjectId, UnitWorldTransformState rootWorld, byte[] defaults)
        => (Model, DefinitionObjectId, RootWorld, pose10)=(model,definitionObjectId,rootWorld,defaults);

    /// <summary>Called once for the six actual offset prototypes. SourceFlag0,
    /// no bound parent and fresh clone/no external setters are the finite scope.
    /// Root fields come from the original DA6C70 declarations and quaternion
    /// normalization, never an identity fallback.</summary>
    public static LoadedCityGateAccessoryJointOffsets CreateFreshSourceDefinition(
        LoadedObjects catalog, DokObject definition, SceneModelReference reference, LoadedModelTransforms actualModel)
    {
        var flags=MapModelNativeVisibilitySources.ReadWorldPqSourceFlags(catalog,definition.ObjectId);
        if(flags.Flag!=0 || flags.DynamicFlag!=0)
            throw new NotSupportedException("Temporary offset installation requires the actual source Flag0/Dynamic0 branch.");
        var translationBits=definition.Properties.Single(p=>p.NameHash==0xB5020F61);
        if(translationBits.TypeCode!=5 || translationBits.ElementCount!=0 || translationBits.RawValue.Length!=0)
            throw new NotSupportedException("Offset fresh getter requires this definition's actual empty IsUnitBoneTranslation declaration.");
        var target=definition.Properties.Single(p=>p.NameHash==0x0B6E1578);
        var modelFile=reference.ModelObject.Properties.Single(p=>p.NameHash==0x8AB68B3F);
        if(target.TypeCode!=5 || target.RawValue.Length!=4 || BinaryPrimitives.ReadUInt32LittleEndian(target.RawValue)!=reference.ModelObject.ObjectId ||
            modelFile.TypeCode!=5 || modelFile.RawValue.Length!=4 || BinaryPrimitives.ReadUInt32LittleEndian(modelFile.RawValue)!=actualModel.Source.FileId ||
            reference.ModelFileId!=actualModel.Source.FileId)
            throw new InvalidDataException("Offset definition/typed Model does not select the actual imported G1M.");
        //Reuse the source field reader; this factory's Q normalization is an
        //additional writer, unlike a raw-definition transform publication.
        var root=UnitWorldTransform.ConstructDefinition(catalog,definition);
        root=root with {Quaternion24=CityGateAccessoryJointOffsets.NormalizeFactoryQuaternion(root.Quaternion24),
            Basis="DA6C70 actual declaration P/Q/S ->normalizedQ ->D9E890 no-parent; DB1350/DB1570/DB1640 root getters"};
        byte[] defaults=actualModel.Transforms.Joints.SelectMany(j=>j.RawBytes).ToArray();
        return new(actualModel,definition.ObjectId,root,defaults);
    }

    /// <summary>Original World.vF8/DAE2C0 chooses installed150 and calls BD9440
    /// with pose10. World1D0 is fresh null, so source-translation override is false.
    /// D8A620's resolved palette is intentionally not consumed by this getter.</summary>
    public AccessoryJointWorld ReadJointWorld(int jointIndex)
        => CityGateAccessoryJointOffsets.CalculateBd9440(pose10,jointIndex,
            RootWorld.Scale34,RootWorld.Quaternion24,new(RootWorld.Position18,1));

    public IReadOnlyList<Vector4> CreateManagerOffsets(int count)
    {
        var offsets=new Vector4[count];
        for(int i=0;i<count;i++){
            var t=ReadJointWorld(i+2).Translation;
            offsets[i]=new(t.X,t.Y,t.Z,1); //1C6890's literal W store.
        }
        return Array.AsReadOnly(offsets);
    }
}

/// <summary>BD9440 exact getter order: ancestor chain, parent-scale times local
/// translation, updated scale, A11C80(localQ/scaledT),17B070(local,current),
/// translation extraction,12A70 then quaternion normalization after each ancestor.</summary>
public static class CityGateAccessoryJointOffsets
{
    public static AccessoryJointWorld CalculateBd9440(ReadOnlySpan<byte> actualPose10,
        int jointIndex,Vector3 actualRootScale90,Quaternion actualRootQuaternion88,Vector4 actualRootPosition80)
    {
        if(actualPose10.Length%48!=0 || (uint)jointIndex>=actualPose10.Length/48)
            throw new ArgumentOutOfRangeException(nameof(jointIndex),"Actual installed pose has no such joint.");
        Span<int> chain=stackalloc int[128];int depth=0,current=jointIndex;
        while(current>=0){
            if((uint)current>=actualPose10.Length/48 || depth==chain.Length)
                throw new InvalidDataException("Actual BD9440 ancestor chain exceeds its native128-entry extent.");
            chain[depth++]=current;current=BinaryPrimitives.ReadInt32LittleEndian(actualPose10.Slice(current*48+12,4));
        }
        Vector3 scale=actualRootScale90;Vector4 translation=actualRootPosition80;
        Quaternion rotation=actualRootQuaternion88;
        Matrix4x4 matrix=UnitWorldTransformMath.QuaternionPositionA12C80(rotation,new(translation.X,translation.Y,translation.Z));
        for(int i=depth-1;i>=0;i--){
            var raw=actualPose10.Slice(chain[i]*48,48);
            Vector4 localScale=V(raw,0),localTranslation=V(raw,32);Quaternion localRotation=new(F(raw,16),F(raw,20),F(raw,24),F(raw,28));
            //Translation uses parent scale before this ancestor's scale update.
            translation=new(M(scale.X,localTranslation.X),M(localTranslation.Y,scale.Y),M(localTranslation.Z,scale.Z),localTranslation.W);
            scale=new(M(localScale.X,scale.X),M(localScale.Y,scale.Y),M(localScale.Z,scale.Z));
            var localMatrix=UnitWorldTransformMath.QuaternionPositionA12C80(localRotation,new(translation.X,translation.Y,translation.Z));
            matrix=UnitWorldTransformMath.ComposeAffine17ADD0(localMatrix,matrix);
            translation=new(matrix.M41,matrix.M42,matrix.M43,translation.W);
            rotation=MapCameraController.QuaternionFromBasis12A70(matrix);
            float length=MathF.Sqrt(A(A(A(M(rotation.X,rotation.X),M(rotation.Y,rotation.Y)),M(rotation.Z,rotation.Z)),M(rotation.W,rotation.W)));
            float reciprocal=D(1,length);rotation=new(M(reciprocal,rotation.X),M(reciprocal,rotation.Y),M(reciprocal,rotation.Z),M(reciprocal,rotation.W));
        }
        //Root v90 has no W store. The final localScaleW is the requested
        //joint's raw parent word; every admitted getter has at least one ancestor.
        float scaleW=F(actualPose10,jointIndex*48+12);Vector4 completeScale=new(scale,scaleW);
        byte[] sqt=new byte[48];Write(sqt,0,completeScale);Write(sqt,16,new(rotation.X,rotation.Y,rotation.Z,rotation.W));Write(sqt,32,translation);
        return new(jointIndex,completeScale,rotation,translation,matrix,sqt);
    }

    public static Quaternion NormalizeFactoryQuaternion(Quaternion source)
    {
        //DA6C70 6177..6189: Y^2+X^2 then Z^2 then W^2, sqrt,
        //reciprocal and component stores. It has no identity fallback.
        float length=MathF.Sqrt(A(A(A(M(source.Y,source.Y),M(source.X,source.X)),M(source.Z,source.Z)),M(source.W,source.W)));
        float reciprocal=D(1,length);
        return new(M(source.X,reciprocal),M(source.Y,reciprocal),M(source.Z,reciprocal),M(source.W,reciprocal));
    }

    static Vector4 V(ReadOnlySpan<byte> b,int offset)=>new(F(b,offset),F(b,offset+4),F(b,offset+8),F(b,offset+12));
    static float F(ReadOnlySpan<byte> b,int offset)=>BinaryPrimitives.ReadSingleLittleEndian(b.Slice(offset,4));
    static void Write(Span<byte> b,int offset,Vector4 v){BinaryPrimitives.WriteSingleLittleEndian(b.Slice(offset,4),v.X);BinaryPrimitives.WriteSingleLittleEndian(b.Slice(offset+4,4),v.Y);BinaryPrimitives.WriteSingleLittleEndian(b.Slice(offset+8,4),v.Z);BinaryPrimitives.WriteSingleLittleEndian(b.Slice(offset+12,4),v.W);}
    [MethodImpl(MethodImplOptions.NoInlining)] static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)] static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)] static float D(float a,float b)=>a/b;
}
