using System.Buffers.Binary;
using Godot;
using San14.Data;

public sealed partial class MapPostEffect3Composite2GraphicsOwner
{
    /// <summary>Original manager linear/point carriers selected by factory9
    /// Before binds these at PS7/8. They are independent of LUT texture81/83 and
    /// sampled-wrapper samplers installed by the later counted Bind.</summary>
    public static IReadOnlyList<IMapPostEffect3ComputeSamplerLease> LeaseSourceModuleCarrierSamplers(
        MapPostEffect3SourceSamplerOwner actualManager,MapPostEffect3Composite2BeforeBindings before,
        IReadOnlyList<MapPostEffect3Composite2SamplerRegister> actuallyDeclaredRegisters)
    {
        ArgumentNullException.ThrowIfNull(actualManager);before.RequireCurrent();
        var result=new List<IMapPostEffect3ComputeSamplerLease>();
        foreach(var register in actuallyDeclaredRegisters.Where(r=>r.NativeStageOrdinal==2&&r.SourceRegister is 7 or 8))
        {
            bool linear=register.SourceRegister==7;
            var key=linear?actualManager.Source.LinearKey:actualManager.Source.PointKey;
            ushort word=BinaryPrimitives.ReadUInt16LittleEndian(key);
            var wrapper=linear?before.ActualModuleLuts.ActualHdrWrapper30:before.ActualModuleLuts.ActualLdrWrapper38;
            if(wrapper is null||linear&&(before.NativeHdrSampler48AfterWrite??before.ActualModuleLuts.ActualHdrSampler48)!=word)
                throw new NotSupportedException("Composite2 mutated manager carrier word needs its original complete-key sampler; the immutable manager sampler cannot substitute another key.");
            var selection=new CarrierSelection(before,register.SourceRegister,linear?
                MapPostEffect3ManagerSamplerKind.Linear:MapPostEffect3ManagerSamplerKind.Point);
            var actual=actualManager.LeaseSelected(selection,2);
            result.Add(new CarrierLease(actual,before,wrapper,word));
        }
        return Array.AsReadOnly(result.ToArray());
    }
    private sealed record CarrierSelection(MapPostEffect3Composite2BeforeBindings Before,uint SourceRegister,
        MapPostEffect3ManagerSamplerKind Kind):IMapPostEffect3ManagerSamplerSelection
    {
        public long FrameNumber=>Before.Frame.FrameNumber;public string ContextOwnerIdentity=>Before.Frame.OwnerIdentity;
        public int NativeStageOrdinal=>2;
        public string SourceEvidence=>Before.SourceEvidence+";actual-module-manager-carrier-"+(SourceRegister==7?"30-linear":"38-point")+
            ";module-carrier-sampler-key;not-a-LUT-asset";
        public void RequireCurrent(){Before.RequireCurrent();if(SourceRegister is not(7 or 8)||!Before.OverridesActualHdrAndLdrSamplers)
            throw new InvalidDataException("Composite2 manager carrier sampler lacks its original Before setter.");}
    }
    private sealed record CarrierLease(IMapPostEffect3ComputeSamplerLease Actual,
        MapPostEffect3Composite2BeforeBindings Before,MapPostEffect3ApplyView ActualModuleWrapper,
        ushort Word):IMapPostEffect3Composite2ModuleLutSamplerLease,IMapPostEffect3SourceMipSamplerLease
    {
        public long FrameNumber=>Actual.FrameNumber;public string ContextOwnerIdentity=>Actual.ContextOwnerIdentity;
        public int NativeStageOrdinal=>Actual.NativeStageOrdinal;public uint SourceRegister=>Actual.SourceRegister;
        public Rid SamplerRid=>Actual.SamplerRid;public string SourceEvidence=>Actual.SourceEvidence;
        public ushort? ActualWrapperSampler48=>Word;
        public void RequireCurrent(){Before.RequireCurrent();Actual.RequireCurrent();}
        public void RequireSelectedBeforeSetter(MapPostEffect3Composite2BeforeBindings before)
        {RequireCurrent();if(!ReferenceEquals(before,Before))throw new InvalidDataException("Composite2 sampler carrier belongs to another actual Before.");}
        public void RequireSourceMipSampling(uint filter,uint bias,uint minimum,uint maximum)
        {(Actual as IMapPostEffect3SourceMipSamplerLease??throw new InvalidDataException("Original manager sampler has no exact mip recipe proof."))
            .RequireSourceMipSampling(filter,bias,minimum,maximum);}
    }
}
