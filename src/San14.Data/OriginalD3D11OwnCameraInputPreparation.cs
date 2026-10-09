using System.Buffers.Binary;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

public readonly record struct OriginalD3D11FieldCameraRegistryEntry(uint SceneInstanceIndex,uint ObjectId,
    uint Group,uint Type,uint ModelOrdinal,uint PrimitiveCount);

public sealed record OriginalD3D11OwnCameraPreparedInputs(int SceneInstanceCount,int ModelCount,int PrimitiveCount);

/// <summary>Initial camera inputs from the accepted remake frame and installed resources.
/// This load boundary creates a fresh, unparented FieldScene model bank; it never
/// reads earlier input packets, observations, executable data or render receipts.</summary>
public static class OriginalD3D11OwnCameraInputPreparation
{
    public static OriginalD3D11OwnCameraPreparedInputs WriteFreshNativeInputs(
        OriginalGameRepository repository,LoadedSceneResources scene,
        OriginalD3D11CameraContinuousInputs camera,OriginalD3D11CameraDirectProfileInputs direct,
        OriginalMapStaticModelGroundHeightSource currentField,string inputRoot,
        OriginalD3D11ModelInputPreparation? preparedModels = null)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(direct);
        ArgumentNullException.ThrowIfNull(currentField);ArgumentException.ThrowIfNullOrWhiteSpace(inputRoot);
        var main=camera.InitialMain;
        if(currentField.Scene.Scene.Source.ResourceUri!=scene.Scene.Source.ResourceUri||
            currentField.Scene.Scene.Source.DecodedSha256!=scene.Scene.Source.DecodedSha256)
            throw new InvalidDataException("Camera grid and scene inputs belong to different loaded resource generations.");
        var terrain=OriginalD3D11TerrainInputPreparation.Load(repository,scene,main.Camera.ExecutableLocale);
        var preparation=OriginalD3D11CameraInputPreparation.Load(repository,main.Camera.ExecutableLocale,terrain);
        var models=preparedModels ?? OriginalD3D11ModelInputPreparation.Load(repository,scene,OriginalScenePlacements.Read(scene),main.Camera,
            placement=>OriginalMapStaticModelPublication.FreshUnparented($"camera-init-field/{placement.Source.Index}"));
        if(models.BoundaryCount!=0)
            throw new NotSupportedException("Fresh FieldScene model camera bindings are unresolved: "+
                string.Join("; ",models.Boundaries.Select(boundary=>$"{boundary.InstanceIndex}: {boundary.Reason}")));
        var registry=ReadFieldRegistry(scene,models.SourceModelMappings);
        preparation.WriteContinuousInputs(Path.Combine(inputRoot,"camera-continuous-input"),camera);
        preparation.WriteRenderModePrefixInputs(Path.Combine(inputRoot,"camera-continuous-input"),direct);
        preparation.WriteMotionInputs(Path.Combine(inputRoot,"camera-motion-input"));
        OriginalD3D11CameraInputPreparation.WriteGroundHeightInputs(
            Path.Combine(inputRoot,"camera-continuous-input","ground-height-current"),currentField);
        WriteFarMainInputs(Path.Combine(inputRoot,"far-main-source-input"),direct,preparation.DirectSource,
            OriginalMapSeasonMainCameraPhaseSource.CreateDefaults(main.Camera.ExecutableLocale),registry);
        return new(scene.Instances.Count,models.InstanceCount,models.PrimitiveCount);
    }

    /// <summary>Join declared VisibilitySet ordering to this actual prepared Model bank.
    /// Effect entries need their declaration identity and group, not a saved particle observation.</summary>
    public static IReadOnlyList<OriginalD3D11FieldCameraRegistryEntry> ReadFieldRegistry(
        LoadedSceneResources scene,IReadOnlyList<OriginalD3D11SourceModelMapping> models)
    {
        ArgumentNullException.ThrowIfNull(scene);ArgumentNullException.ThrowIfNull(models);
        uint[] counts=Words(scene.InstanceListObject,0x209638F1);
        uint[] names=Words(scene.InstanceListObject,0xE21FF09F);
        if(counts.Sum(count=>(long)count)!=scene.Instances.Count||names.Length!=scene.Instances.Count)
            throw new InvalidDataException("FieldScene groups do not cover the loaded ordered registry.");
        var modelBySource=models.ToDictionary(model=>model.SceneInstanceIndex);
        var entries=new OriginalD3D11FieldCameraRegistryEntry[scene.Instances.Count];
        int cursor=0,usedModels=0;
        for(int group=0;group<counts.Length;group++)
            for(int offset=0;offset<counts[group];offset++,cursor++)
            {
                var instance=scene.Instances[cursor];
                if(instance.Index!=cursor||instance.Object.ObjectId!=names[cursor])
                    throw new InvalidDataException("FieldScene registry differs from its declared source ordering.");
                if(instance.Definition.ClassId==OriginalScenePlacements.ModelWorldPqClass)
                {
                    if(!modelBySource.TryGetValue(cursor,out var model)||model.ModelOrdinal<0||model.PrimitiveCount<0)
                        throw new InvalidDataException("FieldScene Model lacks its actual prepared bank mapping.");
                    entries[cursor]=new((uint)cursor,instance.Object.ObjectId,(uint)group,1,
                        (uint)model.ModelOrdinal,(uint)model.PrimitiveCount);
                    usedModels++;
                }
                else if(instance.Definition.ClassId==OriginalScenePlacements.EffectWorldPqClass)
                    entries[cursor]=new((uint)cursor,instance.Object.ObjectId,(uint)group,0x100,uint.MaxValue,0);
                else throw new NotSupportedException("FieldScene camera registry contains an unsupported World class.");
            }
        if(usedModels!=models.Count)
            throw new InvalidDataException("Prepared Model bank contains a mapping outside the ordered FieldScene registry.");
        return Array.AsReadOnly(entries);
    }

    /// <summary>Encode only static Far Main behavior and registry identity. This
    /// is neither the complete prepared Main18C0 nor a selected LightData10C0 seed.</summary>
    public static void WriteFarMainInputs(string directory,OriginalD3D11CameraDirectProfileInputs current,
        LoadedOriginalMapMainCameraPostSelectionSource source,OriginalMapSeasonMainCameraPhaseSource phase,
        IReadOnlyList<OriginalD3D11FieldCameraRegistryEntry> registry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(phase);ArgumentNullException.ThrowIfNull(registry);
        Directory.CreateDirectory(directory);
        using var writer=new BinaryWriter(File.Create(Path.Combine(directory,"profile.bin")));
        writer.Write(Encoding.ASCII.GetBytes("FARMAIN1"));
        writer.Write(current.Quality);writer.Write(current.AppFlags);writer.Write(current.SelectedCloudy430Bits);
        writer.Write(current.PriorCloudy430Bits);writer.Write(BitConverter.SingleToUInt32Bits(.001f));
        writer.Write(phase.StartPhaseNumerator);writer.Write(phase.EndPhaseNumerator);writer.Write(phase.Denominator);
        for(int mode=0;mode<5;mode++)
        {
            var words=source.ProjectDirectWrites(current.Quality,mode,current.AppFlags,
                current.PriorCloudy430Bits,current.SelectedCloudy430Bits);
            writer.Write(words.Count);
            foreach(var pair in words){writer.Write(pair.Key);writer.Write(pair.Value);}
        }
        writer.Write(registry.Count);
        foreach(var entry in registry)
        {
            writer.Write(entry.SceneInstanceIndex);writer.Write(entry.ObjectId);writer.Write(entry.Group);
            writer.Write(entry.Type);writer.Write(entry.ModelOrdinal);writer.Write(entry.PrimitiveCount);
        }
    }

    private static uint[] Words(DokObject owner,uint hash)
    {
        var property=owner.Properties.Single(item=>item.NameHash==hash);
        if(property.TypeCode!=5||property.ElementSize!=4||property.RawValue.Length!=(long)property.ElementCount*4)
            throw new InvalidDataException("FieldScene camera registry requires its complete UInt32 array.");
        return Enumerable.Range(0,property.ElementCount).Select(index=>
            BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue.AsSpan(index*4,4))).ToArray();
    }
}
