using System.Numerics;
using System.Text;
using System.Text.Json;

namespace San14.Data;

/// <summary>Once-only native CPU animation import. Geometry, textures and
/// shader templates remain the existing category bank.</summary>
public static class OriginalArmyGraphicsAnimationNativeInputs
{
    public static void Write(OriginalGameRepository repository,OriginalArmyGraphicsActions actions,string directory)
    {
        Directory.CreateDirectory(directory);
        var models=Enumerable.Range(0,4).ToDictionary(category=>category,
            category=>repository.LoadModelTransforms("CharacterEditor",OriginalCeArmyModels.KnownSourceCategories[category].ModelFileId));
        var catalog=OriginalCeArmyModels.LoadCatalog(repository);
        var pool=OriginalArmyGraphicsPoolSource.Load(repository,"TC");
        File.WriteAllBytes(Path.Combine(directory,"pool-banks.bin"),pool.Encode());
        // Fresh WorldPQ defaults and its canonical ModelCPU OBB. No held demo
        // population/root, current pose or GPU resource supplies this prefix.
        using(var constructors=new BinaryWriter(File.Create(Path.Combine(directory,"constructor-worlds.bin")))){
            constructors.Write(Encoding.ASCII.GetBytes("ARMYCST1"));constructors.Write(4u);
            for(int category=0;category<4;category++){
                var known=OriginalCeArmyModels.KnownSourceCategories[category];
                var definition=catalog.Definitions.Single(row=>row.Definition.ObjectId==known.DefinitionId).Definition;
                var world=OriginalUnitWorldTransform.ConstructDefinition(catalog.Catalog,definition);
                var primitive=OriginalArmyPrimitivePublicationSource.ReadKnownCategory(catalog,category,models[category].Source);
                var bounds=OriginalMapModelNativeBoundsSources.BuildSingleNode(
                    OriginalArmyModelWorldVisibilitySource.Read(primitive).PreparedModelBounds,world,false,false);
                constructors.Write((uint)category);
                WriteVector(constructors,new(world.Scale34,1));
                var q=world.Quaternion24;WriteVector(constructors,new(q.X,q.Y,q.Z,q.W));
                WriteVector(constructors,new(world.Position18,1));
                foreach(var value in new[]{bounds.Center0,bounds.Basis10,bounds.Basis20,bounds.Basis30,bounds.HalfExtents40})
                    WriteVector(constructors,value);
            }
        }
        using(var metadata=new BinaryWriter(File.Create(Path.Combine(directory,"actions.bin")))){
            metadata.Write(Encoding.ASCII.GetBytes("ARMYANM1"));metadata.Write(actions.Actions.Count);
            foreach(var action in actions.Actions.Values.OrderBy(row=>row.Category).ThenBy(row=>row.RawActionId)){
                metadata.Write(action.Category);metadata.Write(action.RawActionId);metadata.Write(action.Animation.ActionIndex);
                metadata.Write(action.Animation.AnimationData[0].Source.FileId);metadata.Write(action.Category<2?1u:0u);
                metadata.Write(60f);metadata.Write(action.Duration);metadata.Write(action.LoopDeclaration);
                metadata.Write(action.StartFrame);metadata.Write(action.DeclaredLength);metadata.Write(action.EndOffset);metadata.Write(action.Interpolation);
                metadata.Write(action.ContinuationAction);metadata.Write(action.ContinuationObject);metadata.Write(action.PlayerFlags);metadata.Write(action.AdditionalAnimationCount);
                WritePose(models[action.Category].Transforms,action.Animation,Path.Combine(directory,$"category-{action.Category}",$"action-{action.RawActionId}"));
            }
        }
        File.WriteAllText(Path.Combine(directory,"source.json"),JsonSerializer.Serialize(new{
            scope="Actual ordinary body ActionID0/1/2 and independent flag0; single declared clips; no GPU resources duplicated",
            constructorWorlds=new{file="constructor-worlds.bin",count=4,recordBytes=132,
                source=catalog.Catalog.Source.ResourceUri,catalog.Catalog.Source.DecodedSha256,
                layout="ARMYCST1/count; category uint32, S/Q/P48, ModelCPU WorldPQ OBB80; fresh actual definitions"},
            poolBanks=new{file="pool-banks.bin",pool.Language,pool.ExecutableSha256,
                tableRva=$"{pool.TableRva:X8}",pool.TableSha256,pool.Banks},
            actions=actions.Actions.Values.Select(row=>new{row.Category,row.RawActionId,declaredOrdinal=row.Animation.ActionIndex,
                player=$"{row.Animation.ActionObject.ObjectId:X8}",clip=row.Animation.AnimationData[0].Source.ResourceUri,
                row.Animation.AnimationData[0].Source.DecodedSha256,row.Duration,row.StartFrame,row.DeclaredLength,
                row.EndOffset,row.Interpolation,row.LoopDeclaration,row.ContinuationAction,row.ContinuationObject,
                row.PlayerFlags,row.AdditionalAnimationCount}),gpu=false,originalProcess=false},new JsonSerializerOptions{WriteIndented=true}));
    }
    private static void WriteVector(BinaryWriter writer,Vector4 value)
    {writer.Write(value.X);writer.Write(value.Y);writer.Write(value.Z);writer.Write(value.W);}
    public static void WritePose(San14.Data.Formats.G1ModelTransforms skeleton,LoadedOriginalArmyAnimationAction action,string directory)
    {
        var clip=action.AnimationData.Single().Clip;
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,"profile.txt"),$"{skeleton.UnknownValue16} {skeleton.RawValue24} {skeleton.RawValue26} {OriginalArmyAnimationHierarchySources.ReadJointRuns(skeleton).Single()}\n");
        using var writer=new BinaryWriter(File.Create(Path.Combine(directory,"pose-source.bin")));
        writer.Write(Encoding.ASCII.GetBytes("CAPOSE01"));writer.Write(clip.FrameCount/clip.FrameRate);writer.Write(clip.FrameRate);writer.Write((uint)clip.FrameCount);
        foreach(uint value in new[]{(uint)skeleton.Joints.Count,(uint)skeleton.RawJointMap.Count,(uint)clip.PackedJointChannels.Count,
            (uint)clip.Channels.Count,(uint)clip.PackedCoefficients.Length,(uint)skeleton.ReferenceMatrices.Count,(uint)skeleton.JointPalettes.Count})writer.Write(value);
        foreach(var joint in skeleton.Joints)writer.Write(joint.RawBytes);
        foreach(var entry in skeleton.RawJointMap)writer.Write((int)unchecked((short)entry));
        foreach(var word in clip.PackedJointChannels)writer.Write(word);
        foreach(var channel in clip.Channels){writer.Write((uint)channel.DescriptorOffsetDwords);writer.Write((uint)channel.Type);
            writer.Write((uint)channel.KeyTimes.Count);writer.Write(channel.FirstCoefficientIndex);foreach(var time in channel.KeyTimes)writer.Write((uint)time);}
        writer.Write(clip.PackedCoefficients);foreach(var reference in skeleton.ReferenceMatrices)writer.Write(reference.RawBytes);
        foreach(var palette in skeleton.JointPalettes){writer.Write((uint)palette.Index);writer.Write((uint)palette.Entries.Count);
            foreach(var entry in palette.Entries){writer.Write(entry.JointIndex);writer.Write(entry.MatrixIndex);writer.Write(entry.RawValue4);}}
    }
}
