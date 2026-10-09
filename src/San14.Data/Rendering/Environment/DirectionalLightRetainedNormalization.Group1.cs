namespace San14.Data;

public sealed partial class DirectionalLightRetainedNormalization
{
    // Import boundary only. The full original shader and PS383 DXBC (including
    // its RDEF) are pinned by LoadKnownConsumers before this exact directory is
    // inspected. Runtime requests retain the loaded directory.
    private static void RequireLate383NamedLayout(LoadedShaderContainer shader)
    {
        (string Name,int Offset,int Bytes)[] expected = [
            ("litDir",0,64),("litCol",64,64),("litOcc",128,16),("ambLit",144,16),("nDirLit",160,4),
            ("vEye",176,16),("vOccRate",192,8),("vCameraPos",208,12),("vCameraDir",224,12),
            ("vCameraViewY",240,12),("vD2Z",256,8),("mRrW2T",272,48),("fog",320,16),("cFog",336,16),
            ("Scat",352,64),("mV2W",416,64),("mP2W",480,64),("fSdmFade",544,4),("hsRampPrm",560,16),
            ("hsAOPrm",576,16),("hsUVMat",592,28),("fAmbMapsDORate",620,4),("vAmbDifScl",624,16),
            ("vAmbSpcScl",640,16),("vTsbViewPort",656,16),("mP2V",672,64)];
        var definitions=shader.ReadResourceDefinitions(383);
        var globals=definitions.Buffers.SingleOrDefault(b=>b.Name.Text=="$Globals");
        if(globals is null || globals.ByteSize!=DeferredSelected383Constants.ByteSize ||
            globals.Variables.Count!=expected.Length || expected.Any(field=>
                globals.Variables.SingleOrDefault(v=>v.Name.Text==field.Name) is not { } variable ||
                variable.StartOffset!=field.Offset || variable.ByteSize!=field.Bytes))
            throw new InvalidDataException("Directional retained Group1 consumer differs from the pinned PS383 complete26-field736B named layout.");
    }

    private static bool IsSupportedLateModifier(DeferredF103SelectedSource selected,
        MapSourceCreatedLightingPacket actualInner)
        => actualInner.SourceIdentity.DeclaredGroupMask switch
        {
            2 or 8 => true,
            // The actual World publisher enables bit1 from its positive first
            // counter; Group1 is not fabricated from an arbitrary inner packet.
            1 => selected.OwnedFeatureSource is { } feature && (feature.CurrentView.ViewFlags19AC & 1u)!=0,
            _ => false
        };
}
