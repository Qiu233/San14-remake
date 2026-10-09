using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalAccessoryAnimationProfile(uint SettingId, uint ActionObjectId,
    uint RawActionId, uint AnimationFileId, float ControllerRate28, float Duration8,
    float StartFrame, float FrameLength, float EndOffset, float LoopStartDeclaration,
    IReadOnlyList<uint> FpsModeTable, string Evidence);

public sealed record OriginalAccessoryAnimationFrame(float InputFrame, float RequestedSeconds,
    float ReturnedSeconds, float ClipSeconds, float RoundedFrame, float NextFrame,
    bool WrappedBack, uint FpsMode, OriginalArmyAnimationLocalPose LocalPose,
    OriginalArmyAnimationHierarchyPose Hierarchy, IReadOnlyList<OriginalArmyNativePaletteRows> Palettes);

/// <summary>Once-loaded category2 single SequentialPlayer source. This finite
/// adapter closes controller creation, default action mapping, native local
/// pose/hierarchy/palettes. It supplies neither cloth simulation nor visibility,
/// material RT publication or the unknown D953F0 negative-override thunk.</summary>
public sealed class LoadedOriginalCityGateAccessoryAnimation
{
    public LoadedModelTransforms Model { get; }
    public LoadedOriginalArmyAnimationAction Action { get; }
    public OriginalAccessoryAnimationProfile Profile { get; }
    public OriginalUnitWorldTransformState DefinitionRoot { get; }
    internal byte[] Defaults { get; }

    private LoadedOriginalCityGateAccessoryAnimation(LoadedModelTransforms model,
        LoadedOriginalArmyAnimationAction action, OriginalAccessoryAnimationProfile profile,
        OriginalUnitWorldTransformState root)
        => (Model,Action,Profile,DefinitionRoot,Defaults)=(model,action,profile,root,
            model.Transforms.Joints.SelectMany(j=>j.RawBytes).ToArray());

    public static LoadedOriginalCityGateAccessoryAnimation Load(OriginalGameRepository repo)
    {
        var catalog=OriginalCeArmyModels.LoadCatalog(repo);
        var category=OriginalCeArmyModels.KnownSourceCategories[2];
        var definition=catalog.Definitions.Single(d=>d.Definition.ObjectId==category.DefinitionId);
        var action=OriginalArmyAnimationResources.LoadAction(repo,catalog,2,0);
        // Declaration05D92ACC is Flag; actual action-map key is the distinct
        // 7FB88531 ActionID (CPU+24). Existing resource carrier's RawActionId
        // names its legacy05D92ACC field, so do not use it as this map identity.
        uint actualActionId=unchecked((uint)I(action.ActionObject,0x7FB88531));
        if(actualActionId!=0 || action.AnimationData.Count!=1)
            throw new NotSupportedException("Current finite controller requires its actual default RawActionId0 and single AnimationData.");
        var obj=action.ActionObject;
        float start=F(obj,0x750E63CB),length=F(obj,0xF977F44D),end=F(obj,0xD3F46C71),interpolate=F(obj,0x4BEF4ECC);
        float loop=F(obj,0xC2AFEDCF);
        // D99B20 falls back to the underlying clip duration for length<=0.
        // This actual single-clip profile has no transition or continuation.
        if(start!=0 || length>0 || end!=0 || interpolate!=0 || loop>=0 ||
            I(obj,0xA26BA507)!=-1 || I(obj,0xA87D31E6)!=-1 || I(obj,0x05D92ACC)!=0)
            throw new NotSupportedException("Accessory player needs the actual zero-start, negative-length/loop, no-continuation profile.");
        var clip=action.AnimationData[0];
        var model=repo.LoadModelTransforms("CharacterEditor",category.ModelFileId);
        var flags=OriginalMapModelNativeVisibilitySources.ReadWorldPqSourceFlags(catalog.Catalog,definition.Definition.ObjectId);
        if(flags.Flag!=0 || flags.DynamicFlag!=0)
            throw new NotSupportedException("Finite source-owned flag installation requires original Flag0/Dynamic0.");
        // Actual factory-normalized declaration, then an explicit unit pose
        // publisher may install its current source scale/P/rawQ.
        var root=OriginalUnitWorldTransform.ConstructDefinition(catalog.Catalog,definition.Definition);
        root=root with {Quaternion24=OriginalCityGateAccessoryJointOffsets.NormalizeFactoryQuaternion(root.Quaternion24)};
        OriginalArmyAnimationHierarchySources.ReadJointRuns(model.Transforms);
        uint[] fps=OriginalD3D11TimingInputPreparation.CreateFpsModeTable();
        float duration=D(clip.Clip.FrameCount,clip.Clip.FrameRate);
        duration=A(duration,end);if(duration<1.1920929e-05f)duration=1.1920929e-05f;
        return new(model,action,new(action.Setting.ObjectId,obj.ObjectId,actualActionId,clip.Source.FileId,
            BitConverter.Int32BitsToSingle(0x42700000),duration,start,length,end,loop,Array.AsReadOnly(fps),
            "BCA490 controller1C=0/24=0/28=60;1C2620-ready->BCA350->BCA1A0 RawActionId-to-ordinal;"+
            "1C3D00 group24=-1;11496D0 action1C;1CEB90 frame/rate->D9FC00/D84A50;"+
            "D99B20 single-clip duration;D91E60 clip-epsilon;935110 mode0;9363B0 table30/60;"+
            "D84810->D8A620->A22170->native palette rows"),root);
    }

    /// <summary>The caller performs one successful source request creation and
    /// supplies the actual unit pose setter. One owner is one created group;
    /// declared nullable request counts do not instantiate this API.</summary>
    public OriginalCityGateAccessoryAnimationOwner CreateCurrentGroup(
        Func<OriginalUnitWorldTransformState,OriginalUnitWorldTransformState> actualUnitPosePublisher)
        => new(this,actualUnitPosePublisher(DefinitionRoot));

    static float F(DokObject o,uint key)=>BinaryPrimitives.ReadSingleLittleEndian(o.Properties.Single(p=>p.NameHash==key).RawValue);
    static int I(DokObject o,uint key)=>BinaryPrimitives.ReadInt32LittleEndian(o.Properties.Single(p=>p.NameHash==key).RawValue);
    [MethodImpl(MethodImplOptions.NoInlining)] internal static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)] internal static float D(float a,float b)=>a/b;
}

public sealed class OriginalCityGateAccessoryAnimationOwner
{
    readonly LoadedOriginalCityGateAccessoryAnimation source;
    byte[] local10;
    public OriginalUnitWorldTransformState CurrentRoot { get; private set; }
    public int ControllerAction1C { get; private set; }=0;
    public int GroupAction24 { get; private set; }=-1;
    public float ControllerFrame24 { get; private set; }=0;
    public float ControllerRate28=>source.Profile.ControllerRate28;
    internal OriginalCityGateAccessoryAnimationOwner(LoadedOriginalCityGateAccessoryAnimation source,
        OriginalUnitWorldTransformState actualRoot)
        =>(this.source,CurrentRoot,local10)=(source,actualRoot,source.Defaults.ToArray());

    public void PublishCurrentRoot(OriginalUnitWorldTransformState actualRoot)=>CurrentRoot=actualRoot;

    /// <summary>Normal 1CEB90(-1,...,-1) uses constructor action0 then retained
    /// frame24. Below player duration, D95330 leaves time unchanged and the
    /// absent D953F0 default thunk cannot affect it. At/after duration the caller
    /// must supply an independently source-resolved returned player phase.
    /// The optional value is NOT a rest-pose or fixed-loop fallback.</summary>
    public OriginalAccessoryAnimationFrame Update(uint actualFpsMode=0,float inputFrame=-1,
        float? actualReturnedPlayerPhase=null)
    {
        if(actualFpsMode>=source.Profile.FpsModeTable.Count)throw new ArgumentOutOfRangeException(nameof(actualFpsMode));
        float prior=ControllerFrame24;
        if(GroupAction24!=ControllerAction1C){inputFrame=0;prior=0;}
        float frame=inputFrame<0?prior:inputFrame;
        float requested=LoadedOriginalCityGateAccessoryAnimation.D(frame,ControllerRate28);
        float phase=requested;
        if(actualReturnedPlayerPhase is {} explicitPhase)phase=explicitPhase;
        else if(requested>=source.Profile.Duration8)
            throw new NotSupportedException("D953F0 negative loop-override thunk is not present in the saved source; provide its actual returned player phase to continue this boundary.");
        if(!float.IsFinite(phase)||phase<0||phase>source.Profile.Duration8)
            throw new ArgumentOutOfRangeException(nameof(actualReturnedPlayerPhase));
        float clipTime=MathF.Min(phase,source.Profile.Duration8-1.1920929e-05f);
        var sample=OriginalArmyAnimationSampler.Sample(source.Action.AnimationData[0].Clip,clipTime);
        var local=OriginalArmyAnimationLocalPoseWriter.Build(source.Model.Transforms,source.Action.AnimationData[0].Clip,sample,local10);
        var hierarchy=OriginalArmyAnimationHierarchy.Build(source.Model.Transforms,local,
            new(new(CurrentRoot.Scale34,0),CurrentRoot.Quaternion24,new(CurrentRoot.Position18,1)));
        var matrices=hierarchy.BuildMatrices();
        var palettes=source.Model.Transforms.JointPalettes.Select(p=>OriginalArmyNativePaletteWriter.Build(source.Model.Transforms,
            p.Index,matrices,matrices.Count,new Vector4[384],OriginalArmyNativePoseBufferKind.Current)).ToArray();
        // Every source palette entry is written. Zeros beyond the active extent
        // are scratch only: consumers receive/patch active rows, never those tails.
        if(palettes.Any(p=>p.WrittenEntryCount!=p.Entries.Count))throw new NotSupportedException("Actual palette has unwritten entries requiring a retained source template.");
        float rounded=MathF.Floor(Add(Mul(phase,ControllerRate28),.5f));
        bool wrapped=prior>rounded;
        float next=wrapped?rounded:Add(rounded,LoadedOriginalCityGateAccessoryAnimation.D(ControllerRate28,source.Profile.FpsModeTable[(int)actualFpsMode]));
        GroupAction24=ControllerAction1C;ControllerFrame24=next;local10=local.LocalPoseBytes;
        return new(frame,requested,phase,clipTime,rounded,next,wrapped,actualFpsMode,local,hierarchy,Array.AsReadOnly(palettes));
    }
    [MethodImpl(MethodImplOptions.NoInlining)] static float Mul(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)] static float Add(float a,float b)=>a+b;
}
