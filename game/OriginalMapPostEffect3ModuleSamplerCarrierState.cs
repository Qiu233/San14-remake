using System.Buffers.Binary;
using San14.Data;

/// <summary>The two actual owned manager sampler recipes are the source of
/// module30/38. These logical wrappers carry sampler48; no pixel texture or
/// Camera LUT identity is inferred from them. Their samplers already belong
/// to the borrowed GPU manager. Before writes are retained in this state.</summary>
public sealed class OriginalMapPostEffect3ModuleSamplerCarrierState
{
    public OriginalMapPostEffect3SourceSamplerOwner Manager { get; }
    private readonly OriginalMapPostEffect3WorldStockGraphicsSourceOwner world;
    private readonly OriginalMapPostEffect3ApplyView linear, point;
    private ushort linearWord;
    private readonly ushort pointWord;
    private OriginalMapPostEffect3ModuleSamplerCarrierState(
        OriginalMapPostEffect3WorldStockGraphicsSourceOwner actualWorld,
        OriginalMapPostEffect3SourceSamplerOwner actualManager,
        OriginalMapPostEffect3ModuleSamplerCarrierState? prior=null)
    {
        world=actualWorld; Manager=actualManager;
        if(prior is null)
        {
            string identity=world.Bank.Source.ContextOwnerIdentity+"/owned-native-manager-carriers/"+Guid.NewGuid().ToString("N");
            linear=new(world.Context.FrameNumber,identity+"/linear30");
            point=new(world.Context.FrameNumber,identity+"/point38");
            linearWord=BinaryPrimitives.ReadUInt16LittleEndian(Manager.Source.LinearKey);
            pointWord=BinaryPrimitives.ReadUInt16LittleEndian(Manager.Source.PointKey);
        }
        else
        {
            //New current-frame loans of the same module wrappers. Preserve
            //sampler48 mutations; keep no reference to the old World/frame.
            linear=new(world.Context.FrameNumber,prior.linear.OwnerIdentity);
            point=new(world.Context.FrameNumber,prior.point.OwnerIdentity);
            linearWord=prior.linearWord;pointWord=prior.pointWord;
        }
    }
    public static OriginalMapPostEffect3ModuleSamplerCarrierState CaptureOwnedSource(
        OriginalMapPostEffect3WorldStockGraphicsSourceOwner actualWorld,
        OriginalMapPostEffect3SourceSamplerOwner actualManager)
    {
        ArgumentNullException.ThrowIfNull(actualWorld);ArgumentNullException.ThrowIfNull(actualManager);
        actualWorld.RequireCurrent();
        if(!actualManager.Source.LinearKey.AsSpan().SequenceEqual(Convert.FromHexString("1554300000000000"))||
            !actualManager.Source.PointKey.AsSpan().SequenceEqual(Convert.FromHexString("1540300000000000")))
            throw new InvalidDataException("Actual module carriers need the original11443A0/1145960 full manager keys.");
        var result=new OriginalMapPostEffect3ModuleSamplerCarrierState(actualWorld,actualManager);
        result.RequireCurrent();return result;
    }
    internal static OriginalMapPostEffect3ModuleSamplerCarrierState CaptureRetainedSource(
        OriginalMapPostEffect3WorldStockGraphicsSourceOwner actualWorld,
        OriginalMapPostEffect3SourceSamplerOwner actualManager,
        OriginalMapPostEffect3ModuleSamplerCarrierState prior)
    {
        actualWorld.RequireCurrent();
        if(!ReferenceEquals(actualWorld.Bank,prior.world.Bank)||!ReferenceEquals(actualManager,prior.Manager)||
            actualWorld.Context.FrameNumber!=prior.world.Context.FrameNumber+1)
            throw new InvalidDataException("Retained sampler carriers need the same actual module manager/Bank and adjacent World generation.");
        var result=new OriginalMapPostEffect3ModuleSamplerCarrierState(actualWorld,actualManager,prior);
        result.RequireCurrent();return result;
    }
    public OriginalMapPostEffect3Composite2ModuleLuts CaptureCurrent()
    {
        RequireCurrent();return new(world.Context.FrameNumber,world.Bank.Source.ContextOwnerIdentity,
            linear,point,linearWord,Manager.Source.SourceEvidence+
            ";owned-original-manager-carriers30/38;8x8/M1;actual-sampler48="+linearWord.ToString("X4")+
            ";point48="+pointWord.ToString("X4")+";not-Camera-LUT-assets81/83");
    }
    public void RequireCurrent()
    {
        world.RequireCurrent();
        // LeaseSelected positively checks that the real manager is live and
        // belongs to this module context. It does not install a shader slot.
        Manager.LeaseSelected(new CarrierLifetime(this),2).RequireCurrent();
    }
    internal void ApplyBefore(OriginalMapPostEffect3Composite2BeforeBindings before)
    {
        before.RequireCurrent();var current=CaptureCurrent();
        if(before.Frame.FrameNumber!=current.FrameNumber||before.Frame.OwnerIdentity!=current.ContextOwnerIdentity||
            !ReferenceEquals(before.ModuleSamplerWrappers.ActualLinearWrapper30,linear)||
            !ReferenceEquals(before.ModuleSamplerWrappers.ActualPointWrapper38,point)||
            before.ModuleSamplerWrappers.ActualLinearSampler48!=linearWord)
            throw new InvalidDataException("Composite2 Before refers to another current module carrier snapshot.");
        if(before.NativeLinearSampler48AfterWrite is{} written)linearWord=written;
    }
    private sealed record CarrierLifetime(OriginalMapPostEffect3ModuleSamplerCarrierState Owner)
        : IOriginalMapPostEffect3ManagerSamplerSelection
    {
        public long FrameNumber=>Owner.world.Context.FrameNumber;
        public string ContextOwnerIdentity=>Owner.world.Bank.Source.ContextOwnerIdentity;
        public int NativeStageOrdinal=>2;public uint SourceRegister=>7;
        public OriginalMapPostEffect3ManagerSamplerKind Kind=>OriginalMapPostEffect3ManagerSamplerKind.Linear;
        public string SourceEvidence=>Owner.Manager.Source.SourceEvidence+";actual-owned-module-carrier-lifetime";
        public void RequireCurrent()=>Owner.world.RequireCurrent();
    }
}
