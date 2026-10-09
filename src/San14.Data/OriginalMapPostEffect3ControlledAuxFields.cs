using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Only current Aux fields consumed by controlled Apply and its
/// FXAA caller. No unused bytes or complete460 record are synthesized.</summary>
public sealed class OriginalMapPostEffect3ControlledAuxFields
{
    internal OriginalMapPostEffect3ControlledAuxFields(long frame,string view,uint flags,int mode,int fxaa,
        int separate,int bloom,bool customPixelShader108Absent,string evidence)
        =>(FrameNumber,SelectedViewOwnerIdentity,Flags8,Mode3C0,Fxaa3A8,SeparateHdrMode440,BloomColorFix448,CustomPixelShader108Absent,SourceEvidence)=
            (frame,view,flags,mode,fxaa,separate,bloom,customPixelShader108Absent,evidence);
    public long FrameNumber{get;}
    public string SelectedViewOwnerIdentity{get;}
    public uint Flags8{get;}
    public int Mode3C0{get;}
    public int Fxaa3A8{get;}
    public int SeparateHdrMode440{get;}
    public int BloomColorFix448{get;}
    /// <summary>Source/current +108 pointer absence. A nonnull custom shader
    /// is not substituted by the type3 stock shader.</summary>
    public bool CustomPixelShader108Absent{get;}
    public string SourceEvidence{get;}
    public bool ProvesCompleteAuxRecord=>false;
    public void ValidateFrame(long frame)
    {
        if(frame!=FrameNumber||frame<=0||string.IsNullOrWhiteSpace(SelectedViewOwnerIdentity)||
            string.IsNullOrWhiteSpace(SourceEvidence)||Mode3C0!=1)
            throw new InvalidDataException("Controlled Apply Aux fields require this actual selected mode1 View and frame.");
    }
    public static OriginalMapPostEffect3ControlledAuxFields CaptureActualPrepared(ReadOnlySpan<byte> actual460,
        long frame,string selectedViewOwner,string actualWriterEvidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedViewOwner);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualWriterEvidence);
        if(actual460.Length!=0x460)throw new InvalidDataException("Explicit complete Aux input needs its actual460 bytes.");
        return new(frame,selectedViewOwner,BinaryPrimitives.ReadUInt32LittleEndian(actual460[8..]),I(actual460,0x3C0),I(actual460,0x3A8),
            I(actual460,0x440),I(actual460,0x448),BinaryPrimitives.ReadUInt64LittleEndian(actual460[0x108..])==0,actualWriterEvidence);

    }
    private static int I(ReadOnlySpan<byte> bytes,int offset)=>BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..]);
}

/// <summary>Pinned constructor,102-property loader and ordered current copy
/// for the owned fresh no-callback/no-overlay selected View. Later callbacks
/// or overlays need their own actual current field capture.</summary>
public sealed class LoadedOriginalMapPostEffect3ControlledAuxSource
{
    internal LoadedOriginalMapPostEffect3ControlledAuxSource(LoadedOriginalDeferredSamplerNativeSource native,string evidence)
        =>(Native,SourceEvidence)=(native,evidence);
    public LoadedOriginalDeferredSamplerNativeSource Native{get;}
    public string SourceEvidence{get;}
    public OriginalMapPostEffect3ControlledAuxFields CaptureFreshSelectedTemplate(LoadedObjects template,
        OriginalMapFreshCommonResourceSnapshot actualCommon,OriginalMapExposureViewAux actualSelectedViewAux)
    {
        ArgumentNullException.ThrowIfNull(actualCommon);ArgumentNullException.ThrowIfNull(actualSelectedViewAux);
        var canonical=OriginalMapFreshCommonResources.ReadSourceTemplate(template);
        if(actualCommon.Scope!=OriginalMapFreshCommonResourceScope.SourceCreatedNoCallbackBeforeAmbientPublication||
            !actualCommon.CommonViewResource240Present||actualCommon.CommonViewFlags8!=canonical.Flags8||
            actualCommon.Resource240Flags0C!=canonical.Flags0C||actualCommon.CurrentViewAux3F2!=canonical.ViewAux3F2)
            throw new NotSupportedException("Controlled Aux source fields require the actual fresh selected template with no later callback or overlay.");
        var selected=OriginalMapExposureViewAux.ReadTemplate(template,OriginalMapFreshCommonResources.SourceObjectId);
        if(actualSelectedViewAux!=selected)
            throw new InvalidDataException("Controlled Aux template differs from the Host's actual selected View settings.");
        var row=template.Document.Objects.Single(o=>o.ObjectId==OriginalMapFreshCommonResources.SourceObjectId);
        int Word(int ordinal,uint hash,uint type)
        {
            var p=row.Properties[ordinal];
            if(p.Index!=ordinal||p.NameHash!=hash||p.TypeCode!=type||p.ElementCount!=1||p.ElementSize!=4||p.RawValue.Length!=4)
                throw new InvalidDataException("Controlled Aux source field differs from its exact loader declaration.");
            return BinaryPrimitives.ReadInt32LittleEndian(p.RawValue);
        }
        var fields=new OriginalMapPostEffect3ControlledAuxFields(actualCommon.FrameNumber,actualCommon.SelectedViewOwnerIdentity,
            canonical.Flags8,Word(31,0x668912BD,4),Word(10,0xBB7C520E,4),Word(92,0x6B8FE283,5),Word(95,0x29DA6891,5),
            true,
            SourceEvidence+";"+canonical.SourceIdentity+";"+actualCommon.ProducerProvenance+
            "-Aux108-source-null/-no-writer/-F0-arena-current-copy;actual-selected-View;only-read-fields;not-a-synthesized460-record");
        fields.ValidateFrame(actualCommon.FrameNumber);return fields;
    }
}

public static class OriginalMapPostEffect3ControlledAuxSource
{
    public static LoadedOriginalMapPostEffect3ControlledAuxSource Load(OriginalGameRepository repository,string locale="TC")
        =>OriginalDeferredSamplerSources.ReadControlledAuxSource(OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale));
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapPostEffect3ControlledAuxSource ReadControlledAuxSource(LoadedOriginalDeferredSamplerNativeSource native)
    {

        return new(native,native.RenderRuleIdentity+
            $"{native.Locale}:paired-Aux-ctor/property-loader/current-copy;-ctor;-102-property-loader;:ordinal92->local88;:Aux440"+
            ":ordinal95->local90;:Aux448;:Aux3A8;:Aux3C0"+
            "-null-overlay->-current-copy;64C/883/89D-preserve-read-fields;-arenaAux");
    }
}
