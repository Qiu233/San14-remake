using System.Buffers.Binary;
using San14.Data;

/// <summary>Build startup shadow inputs from the first real same-Main season
/// request. Both native families share that current camera and registered lights.</summary>
public static class OriginalD3D11ShadowInputs
{
    public static void WriteNativeInputs(OriginalGameRepository repository,OriginalD3D11ModelInputPreparation field,
        string inputDirectory,OriginalD3D11CityGateTemplateBank primary,
        OriginalD3D11CurrentSeasonResources resources,OriginalD3D11CurrentSeasonFrame current)
    {
        string inputs=Path.GetDirectoryName(Path.GetFullPath(inputDirectory))!;
        string locale=resources.Source.Locale;
        var initial=OriginalD3D11MainCameraInitialNumericInputs.Create(repository,current.Mode.AcceptedMainFrame);
        int[] offsets=[0x9EC,0x9F0,0x9F4,0x9F8,0x9FC,0xA00,0xA04,0xA08,0xA10,0xA18,0xA20,
            0xA28,0xA2C,0xA38,0xA3C,0xA40,0xA84,0xA88];
        var words=offsets.ToDictionary(at=>at,initial.ReadWord);
        foreach(var word in current.PreparedWords)if(words.ContainsKey(word.Offset))words[word.Offset]=word.Bits;
        var metadata=OriginalMapSourceCreatedWorldKind1Publication.ReadSeasonSourceRecords(resources.Source.DirectionalDeclarations);
        var lights=current.Lights.Select(light=>new OriginalD3D11ShadowInitialLight(light.ObjectId,light.GroupMask,
            metadata.Single(record=>record.SourceObjectId==light.ObjectId).MaxPrimitiveNum,light.CompleteData)).ToArray();
        var commonObjects=repository.LoadObjects("KIDSSystemResource",OriginalMapFreshCommonResources.SourceFileId);
        var common=OriginalMapFreshCommonResources.PrepareSourceCreatedBeforeAmbientPublication(
            OriginalMapFreshCommonResources.ReadSourceTemplate(commonObjects),current.Mode.AcceptedMainFrame.Camera.FrameNumber,
            "d3d11-season/settings","d3d11-season/common","d3d11-season/view","fresh-source-before-ambient");
        var raster=commonObjects.Document.Objects.Single(value=>value.ObjectId==OriginalMapFreshCommonResources.SourceObjectId).Properties[26];
        uint commonRaster=BinaryPrimitives.ReadUInt32LittleEndian(raster.RawValue)&255;
        var post=current.Rgb.Inputs.MainPostSelection
            ?? throw new InvalidDataException("Startup shadow requires the same owned App post-selection.");
        var rules=OriginalMapMainCameraPostSelectionSource.CreateDefaults(locale);
        OriginalD3D11ShadowInitialFamily Family(int mode)
        {
            var family=new Dictionary<int,uint>(words);
            foreach(var word in rules.ProjectDirectWrites(post.QualityOption18,mode,post.AppFlags,
                post.PriorCloudy430Bits,post.SelectedCloudy430Bits))
                if(family.ContainsKey(word.Key))family[word.Key]=word.Value;
            return new(current.PreparedMain,family,lights,common,commonRaster);
        }
        OriginalD3D11ShadowInputPreparation.Load(repository,field,primary,locale)
            .WriteNativeInputs(inputs,Family(0),Family(1));
    }
}
