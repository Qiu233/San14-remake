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
            ";D02420-Aux108-source-null/D223C0-no-writer/D1ECE9-F0-arena-current-copy;actual-selected-View;only-read-fields;not-a-synthesized460-record");
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
        const string sourceScope="OriginalMapPostEffect3ControlledAuxFields.cs";
        byte[] exe=File.ReadAllBytes(native.PhysicalPath);VerifyHash(exe,native.ExecutableSha256,"controlled Aux executable");
        var image=new NativeImage(exe);byte[] text=image.DecodeText(out int start);
        VerifyHash(text,native.DecodedTextSha256,"controlled Aux decoded text");
        foreach(var (at,length,hash) in new (int,int,string)[]{
            (0xD02010,0x41B,"47A36EC67AFC898E468A7A8AD06E13881C400182918068CB535E6DC690D86273"),
            (0xD223C0,0x1017,"BDC9FB792C97082D61388427ED00F53EEF05FEACD18055B7EB313892B6B3F3E4"),
            (0xD1E410,0xCDD,"6BCDC7C8BBAEAE375183BFCCF8C4B3634DFB00BBAF7A3D1BD782D93963C270B3"),
            (0xF43360,0x95,"6FAE8931243D70AC42FB313C9AAEDE2AE913B8A3CED04C791C4968097C327B22"),
            (0xD12AF7,0x79,"661BE06F9BEBFD8A78F6E2905BCB06DCDAC175FBE49426BFB5A139FB0E05B54E")})
        {
            var actual=OriginalMapPostEffect3NativeSourceProfile.CodeWindow(native.Locale,sourceScope,at,length,hash);
            VerifyHash(text.AsSpan(actual.Rva-start,length),actual.Sha,"controlled Aux source window");
        }
        return new(native,native.ResourceUri+"@"+native.ExecutableSha256+
            $";{native.Locale}:paired-Aux-ctor/property-loader/current-copy;TC-reference-role:D02010-ctor;D223C0-102-property-loader;D22A97/D22A9E:ordinal92->local88;D23346:Aux440;"+
            "D22ABF/D22AC6:ordinal95->local90;D2335E:Aux448;D22FEE:Aux3A8;D23016:Aux3C0;"+
            "F43360-null-overlay->D1E410-current-copy;D1E5FE/64C/883/89D-preserve-read-fields;D12B69-arenaAux");
    }
}
